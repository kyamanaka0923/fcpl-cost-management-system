using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// (区分, 案件 or 費目) ごとの予実差異。
/// 差異は「実績 − 予算」で符号付き。有利/不利の向きは区分で異なる
/// (売上高は正 = 有利、コスト系区分は正 = 不利)。
/// </summary>
public sealed record VarianceLine(
    BudgetCategory Category,
    Guid? ProjectId,
    string? ElementCode,
    string? PeriodDetail,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance,
    bool IsUnplanned)
{
    /// <summary>有利差異かどうか(売上高は超過が有利、コストは未達が有利)。</summary>
    public bool IsFavorable =>
        Category == BudgetCategory.Revenue ? Variance > 0m : Variance < 0m;

    /// <summary>不利差異かどうか。</summary>
    public bool IsAdverse =>
        Category == BudgetCategory.Revenue ? Variance < 0m : Variance > 0m;
}

/// <summary>区分ごとの差異(明細 + 区分サブトータル)。</summary>
public sealed record CategoryVariance(
    BudgetCategory Category,
    IReadOnlyList<VarianceLine> Lines,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

/// <summary>課予算の予実差異分析の結果。コスト = 加工費 + 外注費 + 期間費用。</summary>
public sealed record VarianceReport(
    IReadOnlyList<CategoryVariance> Categories,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal RevenueVariance,
    decimal PlannedCost,
    decimal ActualCost,
    decimal CostVariance);

/// <summary>
/// 課予算の予実差異分析を行うドメインサービス。
/// 明細は半期一括の金額で管理されるため、差異 = 実績金額 − 予算金額 として
/// (区分, 案件 or 費目) の粒度で算出する。同一キーの実績は合算する。
/// </summary>
public sealed class BudgetVarianceAnalysisService
{
    private static readonly BudgetCategory[] AllCategories =
        [BudgetCategory.Revenue, BudgetCategory.Processing, BudgetCategory.Outsourcing, BudgetCategory.PeriodCost];

    /// <summary>予算と実績から (区分, 案件 or 費目) 粒度の差異を算出し、区分別・全体の集計を返す。</summary>
    public VarianceReport Analyze(DepartmentBudget budget, IReadOnlyCollection<ActualEntry> actuals)
    {
        var plannedByKey = budget.Lines
            .ToDictionary(l => (l.Category, ProjectId: l.ProjectId?.Value,
                    ElementCode: l.ElementCode?.Value, l.PeriodDetail),
                l => l.Amount.Value);

        // 同一 (区分, 案件 or 費目 × 明細名) の実績を合算する。
        var actualByKey = actuals
            .GroupBy(a => (a.Category, ProjectId: a.ProjectId?.Value,
                ElementCode: a.ElementCode?.Value, a.PeriodDetail))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount.Value));

        var allLines = plannedByKey.Keys.Union(actualByKey.Keys)
            .OrderBy(k => k.Category).ThenBy(k => k.ElementCode).ThenBy(k => k.PeriodDetail)
            .ThenBy(k => k.ProjectId)
            .Select(key =>
            {
                var hasPlan = plannedByKey.TryGetValue(key, out var plannedAmount);
                var actualAmount = actualByKey.GetValueOrDefault(key);
                return new VarianceLine(
                    key.Category, key.ProjectId, key.ElementCode, key.PeriodDetail,
                    plannedAmount, actualAmount, actualAmount - plannedAmount,
                    IsUnplanned: !hasPlan);
            })
            .ToList();

        var categories = AllCategories
            .Select(category =>
            {
                var lines = allLines.Where(l => l.Category == category).ToList();
                var planned = lines.Sum(l => l.PlannedAmount);
                var actual = lines.Sum(l => l.ActualAmount);
                return new CategoryVariance(category, lines, planned, actual, actual - planned);
            })
            .ToList();

        var revenue = categories.Single(c => c.Category == BudgetCategory.Revenue);
        var plannedCost = categories.Where(c => c.Category != BudgetCategory.Revenue).Sum(c => c.PlannedAmount);
        var actualCost = categories.Where(c => c.Category != BudgetCategory.Revenue).Sum(c => c.ActualAmount);

        return new VarianceReport(categories,
            revenue.PlannedAmount, revenue.ActualAmount, revenue.Variance,
            plannedCost, actualCost, actualCost - plannedCost);
    }
}

/// <summary>予算バージョン間の差分明細。</summary>
public sealed record BudgetComparisonLine(
    BudgetCategory Category,
    Guid? ProjectId,
    string? ElementCode,
    string? PeriodDetail,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>区分ごとの差分(明細 + 区分サブトータル)。</summary>
public sealed record CategoryComparison(
    BudgetCategory Category,
    IReadOnlyList<BudgetComparisonLine> Lines,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>予算バージョン間比較の結果。</summary>
public sealed record BudgetComparisonReport(
    int BaseVersion,
    string BaseLabel,
    int TargetVersion,
    string TargetLabel,
    IReadOnlyList<CategoryComparison> Categories);

/// <summary>課予算のバージョン間の変動を比較するドメインサービス。</summary>
public sealed class BudgetComparisonService
{
    private static readonly BudgetCategory[] AllCategories =
        [BudgetCategory.Revenue, BudgetCategory.Processing, BudgetCategory.Outsourcing, BudgetCategory.PeriodCost];

    /// <summary>2つの予算バージョンを (区分, 案件 or 費目) 粒度で突き合わせ、増減を算出する。同一課・半期のみ。</summary>
    public BudgetComparisonReport Compare(DepartmentBudget baseBudget, DepartmentBudget targetBudget)
    {
        if (baseBudget.DepartmentId != targetBudget.DepartmentId
            || baseBudget.FiscalHalf != targetBudget.FiscalHalf)
        {
            throw new DomainException("同一の課・半期の予算同士のみ比較できます。");
        }

        var baseByKey = baseBudget.Lines
            .ToDictionary(l => (l.Category, ProjectId: l.ProjectId?.Value,
                    ElementCode: l.ElementCode?.Value, l.PeriodDetail),
                l => l.Amount.Value);
        var targetByKey = targetBudget.Lines
            .ToDictionary(l => (l.Category, ProjectId: l.ProjectId?.Value,
                    ElementCode: l.ElementCode?.Value, l.PeriodDetail),
                l => l.Amount.Value);

        var allLines = baseByKey.Keys.Union(targetByKey.Keys)
            .OrderBy(k => k.Category).ThenBy(k => k.ElementCode).ThenBy(k => k.PeriodDetail)
            .ThenBy(k => k.ProjectId)
            .Select(key =>
            {
                var baseAmount = baseByKey.GetValueOrDefault(key);
                var targetAmount = targetByKey.GetValueOrDefault(key);
                return new BudgetComparisonLine(key.Category, key.ProjectId, key.ElementCode,
                    key.PeriodDetail, baseAmount, targetAmount, targetAmount - baseAmount);
            })
            .ToList();

        var categories = AllCategories
            .Select(category =>
            {
                var lines = allLines.Where(l => l.Category == category).ToList();
                var baseTotal = lines.Sum(l => l.BaseAmount);
                var targetTotal = lines.Sum(l => l.TargetAmount);
                return new CategoryComparison(category, lines, baseTotal, targetTotal, targetTotal - baseTotal);
            })
            .ToList();

        return new BudgetComparisonReport(
            baseBudget.Version, baseBudget.Label, targetBudget.Version, targetBudget.Label,
            categories);
    }
}

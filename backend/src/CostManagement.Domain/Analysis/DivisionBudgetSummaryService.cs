using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// 部集計の入力。課ごとの区分別合計(承認済み予算がある課のみ)。
/// 部の合計には区分別の予算・実績合計しか要らないため、明細を持たない軽量な形で受け取る。
/// </summary>
public sealed record DepartmentCategoryTotals(
    DepartmentId DepartmentId,
    IReadOnlyList<CategorySummary> Categories,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedCost,
    decimal ActualCost)
{
    private static readonly BudgetCategory[] AllCategories =
        [BudgetCategory.Revenue, BudgetCategory.Processing, BudgetCategory.Outsourcing, BudgetCategory.PeriodCost];

    /// <summary>
    /// (課, 区分) 別の予算合計・実績合計から課の集計を組み立てる。
    /// 金額のない区分は 0 として4区分すべてを埋め、コストは売上高以外の3区分の合計とする。
    /// </summary>
    public static DepartmentCategoryTotals FromCategoryAmounts(DepartmentId departmentId,
        IEnumerable<DepartmentCategoryAmount> planned,
        IEnumerable<DepartmentCategoryAmount> actual)
    {
        var plannedByCategory = planned.ToDictionary(a => a.Category, a => a.Amount.Value);
        var actualByCategory = actual.ToDictionary(a => a.Category, a => a.Amount.Value);

        var categories = AllCategories
            .Select(category =>
            {
                var p = plannedByCategory.GetValueOrDefault(category);
                var a = actualByCategory.GetValueOrDefault(category);
                return new CategorySummary(category, p, a, a - p);
            })
            .ToList();

        var costs = categories.Where(c => c.Category != BudgetCategory.Revenue).ToList();
        var revenue = categories.Single(c => c.Category == BudgetCategory.Revenue);
        return new DepartmentCategoryTotals(departmentId, categories,
            revenue.PlannedAmount, revenue.ActualAmount,
            costs.Sum(c => c.PlannedAmount), costs.Sum(c => c.ActualAmount));
    }
}

/// <summary>部の課別内訳(各課の売上・コスト・損益の予実)。</summary>
public sealed record DepartmentSummaryLine(
    Guid DepartmentId,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedCost,
    decimal ActualCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance);

/// <summary>区分ごとの部合計(予算・実績・差異)。</summary>
public sealed record CategorySummary(
    BudgetCategory Category,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

/// <summary>
/// 部の予実サマリ。配下課の予実を合計したもの。
/// 全体損益 = 売上高 −(加工費 + 外注費 + 期間費用)。
/// 「部合計 = 課別内訳の合計」が成り立つ。
/// </summary>
public sealed record DivisionSummaryReport(
    IReadOnlyList<CategorySummary> Categories,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal RevenueVariance,
    decimal PlannedCost,
    decimal ActualCost,
    decimal CostVariance,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    IReadOnlyList<DepartmentSummaryLine> DepartmentLines);

/// <summary>
/// 部の予実サマリを、配下課の予実差異分析結果を合計して算出するドメインサービス。
/// </summary>
public sealed class DivisionBudgetSummaryService
{
    private static readonly BudgetCategory[] AllCategories =
        [BudgetCategory.Revenue, BudgetCategory.Processing, BudgetCategory.Outsourcing, BudgetCategory.PeriodCost];

    /// <summary>区分ごとの合計を保持する配列の長さ(区分の総数)。</summary>
    private static readonly int CategorySlots = Enum.GetValues<BudgetCategory>().Length;

    /// <summary>配下課の区分別合計を合計し、区分別・全体・課別内訳の部サマリを返す。</summary>
    public DivisionSummaryReport Summarize(IReadOnlyCollection<DepartmentCategoryTotals> inputs)
    {
        // 区分ごとに課を線形探索せず、区分を添字にした配列へ1パスで積み上げる。
        var planned = new decimal[CategorySlots];
        var actual = new decimal[CategorySlots];
        foreach (var input in inputs)
        {
            foreach (var c in input.Categories)
            {
                planned[(int)c.Category] += c.PlannedAmount;
                actual[(int)c.Category] += c.ActualAmount;
            }
        }
        var categories = AllCategories
            .Select(category => new CategorySummary(category,
                planned[(int)category], actual[(int)category],
                actual[(int)category] - planned[(int)category]))
            .ToList();

        var plannedRevenue = inputs.Sum(i => i.PlannedRevenue);
        var actualRevenue = inputs.Sum(i => i.ActualRevenue);
        var plannedCost = inputs.Sum(i => i.PlannedCost);
        var actualCost = inputs.Sum(i => i.ActualCost);
        var plannedProfit = plannedRevenue - plannedCost;
        var actualProfit = actualRevenue - actualCost;

        var departmentLines = inputs
            .Select(i => new DepartmentSummaryLine(
                i.DepartmentId.Value,
                i.PlannedRevenue, i.ActualRevenue,
                i.PlannedCost, i.ActualCost,
                i.PlannedRevenue - i.PlannedCost,
                i.ActualRevenue - i.ActualCost,
                (i.ActualRevenue - i.ActualCost) - (i.PlannedRevenue - i.PlannedCost)))
            .ToList();

        return new DivisionSummaryReport(
            categories,
            plannedRevenue, actualRevenue, actualRevenue - plannedRevenue,
            plannedCost, actualCost, actualCost - plannedCost,
            plannedProfit, actualProfit, actualProfit - plannedProfit,
            departmentLines);
    }
}

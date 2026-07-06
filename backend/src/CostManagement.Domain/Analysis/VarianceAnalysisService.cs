using CostManagement.Domain.Actuals;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// (費目, 売上対応品目, 会計期間) ごとの原価予実差異。
/// 差異は「実績 − 予算」で符号付き(正 = 予算超過 = 不利差異)。
/// </summary>
public sealed record VarianceLine(
    string ElementCode,
    string? RevenueItem,
    AccountingPeriod Period,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal TotalVariance,
    bool IsUnplanned)
{
    /// <summary>予算超過(不利差異)かどうか。</summary>
    public bool IsAdverse => TotalVariance > 0m;
}

/// <summary>原価予実差異分析の結果。</summary>
public sealed record VarianceReport(
    IReadOnlyList<VarianceLine> Lines,
    decimal TotalPlannedAmount,
    decimal TotalActualAmount,
    decimal TotalVariance);

/// <summary>
/// 原価の予実差異分析(変動分析)を行うドメインサービス。
/// 明細は金額で管理されるため、差異 = 実績金額 − 予算金額 として
/// (費目, 売上対応品目, 会計期間) の粒度で算出する。
/// </summary>
public sealed class VarianceAnalysisService
{
    public VarianceReport Analyze(CostPlan plan, IReadOnlyCollection<ActualCost> actuals,
        AccountingPeriod? from = null, AccountingPeriod? to = null)
    {
        bool InRange(AccountingPeriod p) =>
            (from is null || p >= from.Value) && (to is null || p <= to.Value);

        var plannedByKey = plan.Lines
            .Where(l => InRange(l.Period))
            .ToDictionary(l => (l.ElementCode.Value, l.RevenueItem, l.Period),
                l => l.Amount.Value);

        // 同一 (費目, 売上対応品目, 期間) の実績を合算する。
        var actualByKey = actuals
            .Where(a => InRange(a.Period))
            .GroupBy(a => (a.ElementCode.Value, a.RevenueItem, a.Period))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount.Value));

        var lines = plannedByKey.Keys.Union(actualByKey.Keys)
            .OrderBy(k => k.Period).ThenBy(k => k.Value).ThenBy(k => k.RevenueItem)
            .Select(key =>
            {
                var hasPlan = plannedByKey.TryGetValue(key, out var plannedAmount);
                var actualAmount = actualByKey.GetValueOrDefault(key);
                return new VarianceLine(
                    key.Value, key.RevenueItem, key.Period,
                    plannedAmount, actualAmount, actualAmount - plannedAmount,
                    IsUnplanned: !hasPlan);
            })
            .ToList();

        var totalPlanned = lines.Sum(l => l.PlannedAmount);
        var totalActual = lines.Sum(l => l.ActualAmount);
        return new VarianceReport(lines, totalPlanned, totalActual, totalActual - totalPlanned);
    }
}

/// <summary>予算バージョン間の差分明細。</summary>
public sealed record PlanComparisonLine(
    string ElementCode,
    string? RevenueItem,
    AccountingPeriod Period,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>予算バージョン間比較の結果。</summary>
public sealed record PlanComparisonReport(
    int BaseVersion,
    string BaseLabel,
    int TargetVersion,
    string TargetLabel,
    IReadOnlyList<PlanComparisonLine> Lines,
    decimal BaseTotalAmount,
    decimal TargetTotalAmount,
    decimal TotalDifference);

/// <summary>原価予算バージョン間の変動を比較するドメインサービス。</summary>
public sealed class PlanComparisonService
{
    public PlanComparisonReport Compare(CostPlan basePlan, CostPlan targetPlan)
    {
        if (basePlan.ProjectId != targetPlan.ProjectId)
            throw new DomainException("同一プロジェクトの予算同士のみ比較できます。");

        var baseByKey = basePlan.Lines
            .ToDictionary(l => (l.ElementCode.Value, l.RevenueItem, l.Period), l => l.Amount.Value);
        var targetByKey = targetPlan.Lines
            .ToDictionary(l => (l.ElementCode.Value, l.RevenueItem, l.Period), l => l.Amount.Value);

        var lines = baseByKey.Keys.Union(targetByKey.Keys)
            .OrderBy(k => k.Period).ThenBy(k => k.Value).ThenBy(k => k.RevenueItem)
            .Select(key =>
            {
                var baseAmount = baseByKey.GetValueOrDefault(key);
                var targetAmount = targetByKey.GetValueOrDefault(key);
                return new PlanComparisonLine(key.Value, key.RevenueItem, key.Period,
                    baseAmount, targetAmount, targetAmount - baseAmount);
            })
            .ToList();

        var baseTotal = basePlan.TotalAmount.Value;
        var targetTotal = targetPlan.TotalAmount.Value;
        return new PlanComparisonReport(
            basePlan.Version, basePlan.Label, targetPlan.Version, targetPlan.Label,
            lines, baseTotal, targetTotal, targetTotal - baseTotal);
    }
}

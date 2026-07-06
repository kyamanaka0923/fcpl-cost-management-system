using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// (品目, 会計期間) ごとの売上予実差異。
/// 差異は「実績 − 予算」で符号付き。売上は原価と逆で、正 = 予算超過 = 有利差異。
/// </summary>
public sealed record RevenueVarianceLine(
    string ItemName,
    AccountingPeriod Period,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal TotalVariance,
    bool IsUnplanned)
{
    /// <summary>有利差異(売上が予算を上回った)かどうか。</summary>
    public bool IsFavorable => TotalVariance > 0m;
}

/// <summary>売上予実差異分析の結果。</summary>
public sealed record RevenueVarianceReport(
    IReadOnlyList<RevenueVarianceLine> Lines,
    decimal TotalPlannedAmount,
    decimal TotalActualAmount,
    decimal TotalVariance);

/// <summary>
/// 売上の予実差異分析を行うドメインサービス。
/// 明細は金額で管理されるため、差異 = 実績金額 − 予算金額 として (品目, 会計期間) の粒度で算出する。
/// </summary>
public sealed class RevenueVarianceAnalysisService
{
    public RevenueVarianceReport Analyze(RevenuePlan plan,
        IReadOnlyCollection<ActualRevenue> actuals,
        AccountingPeriod? from = null, AccountingPeriod? to = null)
    {
        bool InRange(AccountingPeriod p) =>
            (from is null || p >= from.Value) && (to is null || p <= to.Value);

        var plannedByKey = plan.Lines
            .Where(l => InRange(l.Period))
            .ToDictionary(l => (l.ItemName, l.Period), l => l.Amount.Value);

        // 同一 (品目, 期間) の実績を合算する。
        var actualByKey = actuals
            .Where(a => InRange(a.Period))
            .GroupBy(a => (a.ItemName, a.Period))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount.Value));

        var lines = plannedByKey.Keys.Union(actualByKey.Keys)
            .OrderBy(k => k.Period).ThenBy(k => k.ItemName)
            .Select(key =>
            {
                var hasPlan = plannedByKey.TryGetValue(key, out var plannedAmount);
                var actualAmount = actualByKey.GetValueOrDefault(key);
                return new RevenueVarianceLine(
                    key.ItemName, key.Period,
                    plannedAmount, actualAmount, actualAmount - plannedAmount,
                    IsUnplanned: !hasPlan);
            })
            .ToList();

        var totalPlanned = lines.Sum(l => l.PlannedAmount);
        var totalActual = lines.Sum(l => l.ActualAmount);
        return new RevenueVarianceReport(lines, totalPlanned, totalActual,
            totalActual - totalPlanned);
    }
}

/// <summary>売上予算バージョン間の差分明細。</summary>
public sealed record RevenuePlanComparisonLine(
    string ItemName,
    AccountingPeriod Period,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>売上予算バージョン間比較の結果。</summary>
public sealed record RevenuePlanComparisonReport(
    int BaseVersion,
    string BaseLabel,
    int TargetVersion,
    string TargetLabel,
    IReadOnlyList<RevenuePlanComparisonLine> Lines,
    decimal BaseTotalAmount,
    decimal TargetTotalAmount,
    decimal TotalDifference);

/// <summary>売上予算バージョン間の変動を比較するドメインサービス。</summary>
public sealed class RevenuePlanComparisonService
{
    public RevenuePlanComparisonReport Compare(RevenuePlan basePlan, RevenuePlan targetPlan)
    {
        if (basePlan.ProjectId != targetPlan.ProjectId)
            throw new DomainException("同一プロジェクトの予算同士のみ比較できます。");

        var baseByKey = basePlan.Lines.ToDictionary(l => (l.ItemName, l.Period), l => l.Amount.Value);
        var targetByKey = targetPlan.Lines.ToDictionary(l => (l.ItemName, l.Period), l => l.Amount.Value);

        var lines = baseByKey.Keys.Union(targetByKey.Keys)
            .OrderBy(k => k.Period).ThenBy(k => k.ItemName)
            .Select(key =>
            {
                var baseAmount = baseByKey.GetValueOrDefault(key);
                var targetAmount = targetByKey.GetValueOrDefault(key);
                return new RevenuePlanComparisonLine(key.ItemName, key.Period, baseAmount,
                    targetAmount, targetAmount - baseAmount);
            })
            .ToList();

        var baseTotal = basePlan.TotalAmount.Value;
        var targetTotal = targetPlan.TotalAmount.Value;
        return new RevenuePlanComparisonReport(
            basePlan.Version, basePlan.Label, targetPlan.Version, targetPlan.Label,
            lines, baseTotal, targetTotal, targetTotal - baseTotal);
    }
}

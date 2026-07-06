using CostManagement.Domain.Actuals;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// (費目, 会計期間) ごとの予実差異。
/// 差異は「実績 − 予算」で符号付き(正 = 予算超過 = 不利差異)。
/// </summary>
public sealed record VarianceLine(
    string ElementCode,
    AccountingPeriod Period,
    decimal PlannedQuantity,
    decimal PlannedUnitPrice,
    decimal PlannedAmount,
    decimal ActualQuantity,
    decimal ActualUnitPrice,
    decimal ActualAmount,
    decimal TotalVariance,
    decimal? PriceVariance,
    decimal? QuantityVariance,
    bool IsUnplanned)
{
    /// <summary>予算超過(不利差異)かどうか。</summary>
    public bool IsAdverse => TotalVariance > 0m;
}

/// <summary>予実差異分析の結果。</summary>
public sealed record VarianceReport(
    IReadOnlyList<VarianceLine> Lines,
    decimal TotalPlannedAmount,
    decimal TotalActualAmount,
    decimal TotalVariance);

/// <summary>
/// 予実差異分析(変動分析)を行うドメインサービス。
/// 総差異を価格差異と数量差異に分解する:
///   価格差異 = (実際単価 − 予定単価) × 実際数量
///   数量差異 = (実際数量 − 予定数量) × 予定単価
///   総差異   = 価格差異 + 数量差異 = 実績金額 − 予算金額
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
            .ToDictionary(l => (l.ElementCode.Value, l.Period));

        // 同一 (費目, 期間) の実績を合算し、実際単価は加重平均で求める。
        var actualByKey = actuals
            .Where(a => InRange(a.Period))
            .GroupBy(a => (a.ElementCode.Value, a.Period))
            .ToDictionary(
                g => g.Key,
                g => (Quantity: g.Sum(a => a.Quantity), Amount: g.Sum(a => a.Amount.Value)));

        var keys = plannedByKey.Keys.Union(actualByKey.Keys)
            .OrderBy(k => k.Item2).ThenBy(k => k.Value);

        var lines = new List<VarianceLine>();
        foreach (var key in keys)
        {
            var planned = plannedByKey.GetValueOrDefault(key);
            var (actualQty, actualAmount) = actualByKey.GetValueOrDefault(key);

            var plannedQty = planned?.Quantity ?? 0m;
            var plannedPrice = planned?.UnitPrice.Value ?? 0m;
            var plannedAmount = planned?.Amount.Value ?? 0m;
            var actualPrice = actualQty != 0m ? actualAmount / actualQty : 0m;

            var totalVariance = actualAmount - plannedAmount;

            // 差異の分解は予算明細が存在する場合のみ意味を持つ。
            decimal? priceVariance = null;
            decimal? quantityVariance = null;
            var isUnplanned = planned is null;
            if (!isUnplanned)
            {
                priceVariance = (actualPrice - plannedPrice) * actualQty;
                quantityVariance = (actualQty - plannedQty) * plannedPrice;
            }

            lines.Add(new VarianceLine(
                key.Value, key.Item2,
                plannedQty, plannedPrice, plannedAmount,
                actualQty, actualPrice, actualAmount,
                totalVariance, priceVariance, quantityVariance, isUnplanned));
        }

        var totalPlanned = lines.Sum(l => l.PlannedAmount);
        var totalActual = lines.Sum(l => l.ActualAmount);
        return new VarianceReport(lines, totalPlanned, totalActual, totalActual - totalPlanned);
    }
}

/// <summary>予算バージョン間の差分明細。</summary>
public sealed record PlanComparisonLine(
    string ElementCode,
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

/// <summary>予算バージョン間の変動を比較するドメインサービス。</summary>
public sealed class PlanComparisonService
{
    public PlanComparisonReport Compare(CostPlan basePlan, CostPlan targetPlan)
    {
        if (basePlan.ProjectId != targetPlan.ProjectId)
            throw new DomainException("同一プロジェクトの予算同士のみ比較できます。");

        var baseByKey = basePlan.Lines.ToDictionary(l => (l.ElementCode.Value, l.Period));
        var targetByKey = targetPlan.Lines.ToDictionary(l => (l.ElementCode.Value, l.Period));

        var lines = baseByKey.Keys.Union(targetByKey.Keys)
            .OrderBy(k => k.Item2).ThenBy(k => k.Value)
            .Select(key =>
            {
                var baseAmount = baseByKey.GetValueOrDefault(key)?.Amount.Value ?? 0m;
                var targetAmount = targetByKey.GetValueOrDefault(key)?.Amount.Value ?? 0m;
                return new PlanComparisonLine(key.Value, key.Item2, baseAmount, targetAmount,
                    targetAmount - baseAmount);
            })
            .ToList();

        var baseTotal = basePlan.TotalAmount.Value;
        var targetTotal = targetPlan.TotalAmount.Value;
        return new PlanComparisonReport(
            basePlan.Version, basePlan.Label, targetPlan.Version, targetPlan.Label,
            lines, baseTotal, targetTotal, targetTotal - baseTotal);
    }
}

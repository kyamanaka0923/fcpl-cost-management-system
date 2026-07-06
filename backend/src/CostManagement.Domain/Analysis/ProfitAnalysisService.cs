using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Analysis;

/// <summary>品目別の損益。ItemName が null の行は売上対応品目のない原価(共通費)。</summary>
public sealed record ProfitItemLine(
    string? ItemName,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedCost,
    decimal ActualCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance);

/// <summary>月別の損益。</summary>
public sealed record ProfitPeriodLine(
    AccountingPeriod Period,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedCost,
    decimal ActualCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance);

/// <summary>損益(粗利)予実分析の結果。</summary>
public sealed record ProfitReport(
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal RevenueVariance,
    decimal PlannedCost,
    decimal ActualCost,
    decimal CostVariance,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    decimal? PlannedMarginRate,
    decimal? ActualMarginRate,
    IReadOnlyList<ProfitItemLine> ItemLines,
    IReadOnlyList<ProfitPeriodLine> PeriodLines);

/// <summary>
/// 損益(粗利)の予実分析を行うドメインサービス。
/// 売上と、売上対応品目で紐付けられた原価を品目単位で突き合わせ、
/// 品目別・月別の粗利を算出する。対応品目のない原価は「共通費」として扱う。
/// </summary>
public sealed class ProfitAnalysisService
{
    public ProfitReport Analyze(RevenueVarianceReport revenue, VarianceReport cost)
    {
        // ---- 品目別 ----
        var revenueByItem = revenue.Lines
            .GroupBy(l => l.ItemName)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));
        var costByItem = cost.Lines
            .Where(l => l.RevenueItem is not null)
            .GroupBy(l => l.RevenueItem!)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));

        var itemLines = revenueByItem.Keys.Union(costByItem.Keys)
            .OrderBy(k => k)
            .Select(item =>
            {
                var rev = revenueByItem.GetValueOrDefault(item);
                var c = costByItem.GetValueOrDefault(item);
                return BuildItemLine(item, rev, c);
            })
            .ToList();

        // 売上対応品目のない原価は共通費として末尾に置く。
        var commonCosts = cost.Lines.Where(l => l.RevenueItem is null).ToList();
        if (commonCosts.Count > 0)
        {
            itemLines.Add(BuildItemLine(null, (0m, 0m),
                (commonCosts.Sum(l => l.PlannedAmount), commonCosts.Sum(l => l.ActualAmount))));
        }

        // ---- 月別 ----
        var revenueByPeriod = revenue.Lines
            .GroupBy(l => l.Period)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));
        var costByPeriod = cost.Lines
            .GroupBy(l => l.Period)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));

        var periodLines = revenueByPeriod.Keys.Union(costByPeriod.Keys)
            .OrderBy(p => p)
            .Select(p =>
            {
                var rev = revenueByPeriod.GetValueOrDefault(p);
                var c = costByPeriod.GetValueOrDefault(p);
                var plannedProfit = rev.Planned - c.Planned;
                var actualProfit = rev.Actual - c.Actual;
                return new ProfitPeriodLine(p,
                    rev.Planned, rev.Actual, c.Planned, c.Actual,
                    plannedProfit, actualProfit, actualProfit - plannedProfit);
            })
            .ToList();

        // ---- 合計 ----
        var totalPlannedProfit = revenue.TotalPlannedAmount - cost.TotalPlannedAmount;
        var totalActualProfit = revenue.TotalActualAmount - cost.TotalActualAmount;

        return new ProfitReport(
            revenue.TotalPlannedAmount, revenue.TotalActualAmount, revenue.TotalVariance,
            cost.TotalPlannedAmount, cost.TotalActualAmount, cost.TotalVariance,
            totalPlannedProfit, totalActualProfit, totalActualProfit - totalPlannedProfit,
            revenue.TotalPlannedAmount != 0m ? totalPlannedProfit / revenue.TotalPlannedAmount : null,
            revenue.TotalActualAmount != 0m ? totalActualProfit / revenue.TotalActualAmount : null,
            itemLines, periodLines);
    }

    private static ProfitItemLine BuildItemLine(string? item,
        (decimal Planned, decimal Actual) revenue, (decimal Planned, decimal Actual) cost)
    {
        var plannedProfit = revenue.Planned - cost.Planned;
        var actualProfit = revenue.Actual - cost.Actual;
        return new ProfitItemLine(item,
            revenue.Planned, revenue.Actual, cost.Planned, cost.Actual,
            plannedProfit, actualProfit, actualProfit - plannedProfit);
    }
}

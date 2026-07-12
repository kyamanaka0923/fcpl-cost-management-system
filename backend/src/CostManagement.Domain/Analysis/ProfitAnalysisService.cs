using CostManagement.Domain.Budgeting;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// 案件別の損益。案件損益 = 売上高 − 加工費 − 外注費(期間費用は課共通のため含めない)。
/// </summary>
public sealed record ProjectProfitLine(
    Guid ProjectId,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedProcessing,
    decimal ActualProcessing,
    decimal PlannedOutsourcing,
    decimal ActualOutsourcing,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance);

/// <summary>
/// 課の損益分析の結果。
/// 全体損益 = 売上高 −(加工費 + 外注費 + 期間費用)。
/// 粗利率は損益 ÷ 売上高(売上高が 0 のときは null)。
/// </summary>
public sealed record ProfitReport(
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedTotalCost,
    decimal ActualTotalCost,
    decimal PlannedPeriodCost,
    decimal ActualPeriodCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    decimal? PlannedMarginRate,
    decimal? ActualMarginRate,
    IReadOnlyList<ProjectProfitLine> ProjectLines);

/// <summary>
/// 課の損益(予算・実績)を分析するドメインサービス。
/// 予実差異分析の結果(VarianceReport)を入力とし、案件別損益と課全体の損益を算出する。
/// </summary>
public sealed class ProfitAnalysisService
{
    /// <summary>予実差異分析の結果から、案件別損益と課全体の損益(粗利率含む)を算出する。</summary>
    public ProfitReport Analyze(VarianceReport variance)
    {
        var projectLines = variance.Categories
            .Where(c => c.Category.IsProjectBased())
            .SelectMany(c => c.Lines)
            .GroupBy(l => l.ProjectId!.Value)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                decimal Planned(BudgetCategory category) =>
                    g.Where(l => l.Category == category).Sum(l => l.PlannedAmount);
                decimal Actual(BudgetCategory category) =>
                    g.Where(l => l.Category == category).Sum(l => l.ActualAmount);

                var plannedProfit = Planned(BudgetCategory.Revenue)
                    - Planned(BudgetCategory.Processing) - Planned(BudgetCategory.Outsourcing);
                var actualProfit = Actual(BudgetCategory.Revenue)
                    - Actual(BudgetCategory.Processing) - Actual(BudgetCategory.Outsourcing);

                return new ProjectProfitLine(g.Key,
                    Planned(BudgetCategory.Revenue), Actual(BudgetCategory.Revenue),
                    Planned(BudgetCategory.Processing), Actual(BudgetCategory.Processing),
                    Planned(BudgetCategory.Outsourcing), Actual(BudgetCategory.Outsourcing),
                    plannedProfit, actualProfit, actualProfit - plannedProfit);
            })
            .ToList();

        var periodCost = variance.Categories.Single(c => c.Category == BudgetCategory.PeriodCost);
        var plannedProfitTotal = variance.PlannedRevenue - variance.PlannedCost;
        var actualProfitTotal = variance.ActualRevenue - variance.ActualCost;

        return new ProfitReport(
            variance.PlannedRevenue, variance.ActualRevenue,
            variance.PlannedCost, variance.ActualCost,
            periodCost.PlannedAmount, periodCost.ActualAmount,
            plannedProfitTotal, actualProfitTotal, actualProfitTotal - plannedProfitTotal,
            variance.PlannedRevenue != 0m ? plannedProfitTotal / variance.PlannedRevenue : null,
            variance.ActualRevenue != 0m ? actualProfitTotal / variance.ActualRevenue : null,
            projectLines);
    }
}

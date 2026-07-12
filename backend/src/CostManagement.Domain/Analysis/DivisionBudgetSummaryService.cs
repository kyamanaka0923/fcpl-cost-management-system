using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;

namespace CostManagement.Domain.Analysis;

/// <summary>
/// 部集計の入力。課ごとの予実差異分析結果(承認済み予算がある課のみ)。
/// </summary>
public sealed record DepartmentVarianceInput(DepartmentId DepartmentId, VarianceReport Report);

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

    /// <summary>配下課の予実差異分析結果を合計し、区分別・全体・課別内訳の部サマリを返す。</summary>
    public DivisionSummaryReport Summarize(IReadOnlyCollection<DepartmentVarianceInput> inputs)
    {
        var categories = AllCategories
            .Select(category =>
            {
                decimal planned = 0m, actual = 0m;
                foreach (var input in inputs)
                {
                    var c = input.Report.Categories.Single(x => x.Category == category);
                    planned += c.PlannedAmount;
                    actual += c.ActualAmount;
                }
                return new CategorySummary(category, planned, actual, actual - planned);
            })
            .ToList();

        var plannedRevenue = inputs.Sum(i => i.Report.PlannedRevenue);
        var actualRevenue = inputs.Sum(i => i.Report.ActualRevenue);
        var plannedCost = inputs.Sum(i => i.Report.PlannedCost);
        var actualCost = inputs.Sum(i => i.Report.ActualCost);
        var plannedProfit = plannedRevenue - plannedCost;
        var actualProfit = actualRevenue - actualCost;

        var departmentLines = inputs
            .Select(i => new DepartmentSummaryLine(
                i.DepartmentId.Value,
                i.Report.PlannedRevenue, i.Report.ActualRevenue,
                i.Report.PlannedCost, i.Report.ActualCost,
                i.Report.PlannedRevenue - i.Report.PlannedCost,
                i.Report.ActualRevenue - i.Report.ActualCost,
                (i.Report.ActualRevenue - i.Report.ActualCost)
                    - (i.Report.PlannedRevenue - i.Report.PlannedCost)))
            .ToList();

        return new DivisionSummaryReport(
            categories,
            plannedRevenue, actualRevenue, actualRevenue - plannedRevenue,
            plannedCost, actualCost, actualCost - plannedCost,
            plannedProfit, actualProfit, actualProfit - plannedProfit,
            departmentLines);
    }
}

using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class ProfitAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId ProjectA = ProjectId.New();
    private static readonly ProjectId ProjectB = ProjectId.New();
    private static readonly CostElementCode Personnel = new("PERSONNEL");

    private static readonly BudgetVarianceAnalysisService VarianceService = new();
    private static readonly ProfitAnalysisService Service = new();

    private static ProfitReport Analyze(DepartmentBudget budget, params ActualEntry[] actuals) =>
        Service.Analyze(VarianceService.Analyze(budget, actuals));

    private static DepartmentBudget StandardBudget()
    {
        // 案件A: 売上300万 加工費100万 外注費50万 → 損益150万
        // 案件B: 売上200万 加工費80万 → 損益120万
        // 期間費用: 人件費70万
        var budget = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(3_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectB, new Money(2_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Processing, ProjectA, new Money(1_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Processing, ProjectB, new Money(800_000m));
        budget.UpsertProjectLine(BudgetCategory.Outsourcing, ProjectA, new Money(500_000m));
        budget.UpsertPeriodCostLine(Personnel, new Money(700_000m));
        budget.Approve(Now);
        return budget;
    }

    private static ActualEntry Actual(BudgetCategory category, ProjectId? project,
        CostElementCode? element, decimal amount) =>
        ActualEntry.Record(Dept, Half, category, project, element, null, new Money(amount), null, Now);

    [Fact]
    public void 全体損益は売上高から総コストを引いた額になる()
    {
        var report = Analyze(StandardBudget(),
            Actual(BudgetCategory.Revenue, ProjectA, null, 3_200_000m),
            Actual(BudgetCategory.Processing, ProjectA, null, 1_100_000m),
            Actual(BudgetCategory.PeriodCost, null, Personnel, 650_000m));

        Assert.Equal(5_000_000m, report.PlannedRevenue);
        Assert.Equal(3_000_000m, report.PlannedTotalCost);
        Assert.Equal(2_000_000m, report.PlannedProfit);

        Assert.Equal(3_200_000m, report.ActualRevenue);
        Assert.Equal(1_750_000m, report.ActualTotalCost);
        Assert.Equal(1_450_000m, report.ActualProfit);
        Assert.Equal(-550_000m, report.ProfitVariance);
    }

    [Fact]
    public void 案件別損益に期間費用は含めない()
    {
        var report = Analyze(StandardBudget());

        var lineA = report.ProjectLines.Single(l => l.ProjectId == ProjectA.Value);
        Assert.Equal(3_000_000m, lineA.PlannedRevenue);
        Assert.Equal(1_000_000m, lineA.PlannedProcessing);
        Assert.Equal(500_000m, lineA.PlannedOutsourcing);
        Assert.Equal(1_500_000m, lineA.PlannedProfit);

        var lineB = report.ProjectLines.Single(l => l.ProjectId == ProjectB.Value);
        Assert.Equal(1_200_000m, lineB.PlannedProfit);

        // 案件別損益の合計 − 期間費用 = 全体の損益
        Assert.Equal(report.PlannedProfit,
            report.ProjectLines.Sum(l => l.PlannedProfit) - report.PlannedPeriodCost);
    }

    [Fact]
    public void 期間費用は課共通として全体にのみ計上される()
    {
        var report = Analyze(StandardBudget(),
            Actual(BudgetCategory.PeriodCost, null, Personnel, 750_000m));

        Assert.Equal(700_000m, report.PlannedPeriodCost);
        Assert.Equal(750_000m, report.ActualPeriodCost);
        // 案件行に期間費用は現れない
        Assert.All(report.ProjectLines, l =>
            Assert.Equal(l.PlannedRevenue - l.PlannedProcessing - l.PlannedOutsourcing, l.PlannedProfit));
    }

    [Fact]
    public void 粗利率は損益を売上高で割った値になる()
    {
        var report = Analyze(StandardBudget(),
            Actual(BudgetCategory.Revenue, ProjectA, null, 2_000_000m),
            Actual(BudgetCategory.Processing, ProjectA, null, 500_000m));

        Assert.Equal(0.4m, report.PlannedMarginRate);           // 200万 ÷ 500万
        Assert.Equal(0.75m, report.ActualMarginRate);            // (200万−50万) ÷ 200万
    }

    [Fact]
    public void 売上高が0のとき粗利率はnullになる()
    {
        var budget = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);
        budget.UpsertPeriodCostLine(Personnel, new Money(700_000m));
        budget.Approve(Now);

        var report = Analyze(budget);

        Assert.Null(report.PlannedMarginRate);
        Assert.Null(report.ActualMarginRate);
        Assert.Equal(-700_000m, report.PlannedProfit);
    }
}

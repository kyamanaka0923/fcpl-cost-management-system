using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class DivisionBudgetSummaryTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly CostElementCode Personnel = new("PERSONNEL");

    private static readonly BudgetVarianceAnalysisService VarianceService = new();
    private static readonly DivisionBudgetSummaryService Service = new();

    // 課の予実差異分析結果を、売上高・加工費・期間費用の予算/実績から組み立てるヘルパ。
    private static DepartmentVarianceInput 課の予実(
        decimal 売上予算, decimal 売上実績,
        decimal 加工費予算, decimal 加工費実績,
        decimal 期間費用予算, decimal 期間費用実績)
    {
        var dept = DepartmentId.New();
        var project = ProjectId.New();
        var budget = DepartmentBudget.CreateInitial(dept, Half, "当初予算", Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, project, new Money(売上予算));
        budget.UpsertProjectLine(BudgetCategory.Processing, project, new Money(加工費予算));
        budget.UpsertPeriodCostLine(Personnel, new Money(期間費用予算));
        budget.Approve(Now);

        var actuals = new[]
        {
            ActualEntry.Record(dept, Half, BudgetCategory.Revenue, project, null, new Money(売上実績), null, Now),
            ActualEntry.Record(dept, Half, BudgetCategory.Processing, project, null, new Money(加工費実績), null, Now),
            ActualEntry.Record(dept, Half, BudgetCategory.PeriodCost, null, Personnel, new Money(期間費用実績), null, Now),
        };

        return new DepartmentVarianceInput(dept, VarianceService.Analyze(budget, actuals));
    }

    [Fact]
    public void 配下課の予実を区分別に合計する()
    {
        var 課1 = 課の予実(3_000_000m, 3_200_000m, 1_000_000m, 1_100_000m, 300_000m, 250_000m);
        var 課2 = 課の予実(2_000_000m, 1_800_000m, 800_000m, 850_000m, 200_000m, 210_000m);

        var report = Service.Summarize([課1, 課2]);

        var revenue = report.Categories.Single(c => c.Category == BudgetCategory.Revenue);
        Assert.Equal(5_000_000m, revenue.PlannedAmount);
        Assert.Equal(5_000_000m, revenue.ActualAmount);

        var processing = report.Categories.Single(c => c.Category == BudgetCategory.Processing);
        Assert.Equal(1_800_000m, processing.PlannedAmount);
        Assert.Equal(1_950_000m, processing.ActualAmount);

        var periodCost = report.Categories.Single(c => c.Category == BudgetCategory.PeriodCost);
        Assert.Equal(500_000m, periodCost.PlannedAmount);
        Assert.Equal(460_000m, periodCost.ActualAmount);

        Assert.Equal(5_000_000m, report.PlannedRevenue);
        Assert.Equal(2_300_000m, report.PlannedCost);   // (100+30) + (80+20) 万
        Assert.Equal(2_410_000m, report.ActualCost);    // (110+25) + (85+21) 万
    }

    [Fact]
    public void 部合計は課別内訳の合計と一致する()
    {
        var 課1 = 課の予実(3_000_000m, 3_200_000m, 1_000_000m, 1_100_000m, 300_000m, 250_000m);
        var 課2 = 課の予実(2_000_000m, 1_800_000m, 800_000m, 850_000m, 200_000m, 210_000m);

        var report = Service.Summarize([課1, 課2]);

        Assert.Equal(2, report.DepartmentLines.Count);
        Assert.Equal(report.PlannedRevenue, report.DepartmentLines.Sum(l => l.PlannedRevenue));
        Assert.Equal(report.ActualCost, report.DepartmentLines.Sum(l => l.ActualCost));
        Assert.Equal(report.PlannedProfit, report.DepartmentLines.Sum(l => l.PlannedProfit));
        Assert.Equal(report.ActualProfit, report.DepartmentLines.Sum(l => l.ActualProfit));
    }

    [Fact]
    public void 損益は売上からコストを引いた合計になる()
    {
        var 課1 = 課の予実(3_000_000m, 3_200_000m, 1_000_000m, 1_100_000m, 300_000m, 250_000m);

        var report = Service.Summarize([課1]);

        // 売上300万 − コスト(加工100万 + 期間30万)= 170万(計画)
        Assert.Equal(1_700_000m, report.PlannedProfit);
        // 売上320万 − コスト(加工110万 + 期間25万)= 185万(実績)
        Assert.Equal(1_850_000m, report.ActualProfit);
        Assert.Equal(150_000m, report.ProfitVariance);
    }

    [Fact]
    public void 配下課がなければ全て0になる()
    {
        var report = Service.Summarize([]);

        Assert.Empty(report.DepartmentLines);
        Assert.Equal(0m, report.PlannedRevenue);
        Assert.Equal(0m, report.ActualProfit);
        Assert.All(report.Categories, c => Assert.Equal(0m, c.PlannedAmount));
    }
}

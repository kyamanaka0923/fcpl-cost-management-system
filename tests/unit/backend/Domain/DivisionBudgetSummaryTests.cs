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

    private static readonly DivisionBudgetSummaryService Service = new();

    // 課の区分別合計を、売上高・加工費・期間費用の予算/実績から組み立てるヘルパ。
    private static DepartmentCategoryTotals 課の予実(
        decimal 売上予算, decimal 売上実績,
        decimal 加工費予算, decimal 加工費実績,
        decimal 期間費用予算, decimal 期間費用実績)
    {
        var dept = DepartmentId.New();
        return DepartmentCategoryTotals.FromCategoryAmounts(dept,
            [
                new(dept, BudgetCategory.Revenue, new Money(売上予算)),
                new(dept, BudgetCategory.Processing, new Money(加工費予算)),
                new(dept, BudgetCategory.PeriodCost, new Money(期間費用予算)),
            ],
            [
                new(dept, BudgetCategory.Revenue, new Money(売上実績)),
                new(dept, BudgetCategory.Processing, new Money(加工費実績)),
                new(dept, BudgetCategory.PeriodCost, new Money(期間費用実績)),
            ]);
    }

    [Fact]
    public void 課の集計は外注費のように金額のない区分も0で埋める()
    {
        var dept = DepartmentId.New();

        var totals = DepartmentCategoryTotals.FromCategoryAmounts(dept,
            [new(dept, BudgetCategory.Revenue, new Money(3_000_000m))],
            []);

        Assert.Equal(4, totals.Categories.Count);
        var outsourcing = totals.Categories.Single(c => c.Category == BudgetCategory.Outsourcing);
        Assert.Equal(0m, outsourcing.PlannedAmount);
        Assert.Equal(0m, outsourcing.ActualAmount);
        Assert.Equal(3_000_000m, totals.PlannedRevenue);
        Assert.Equal(0m, totals.ActualRevenue);
        Assert.Equal(0m, totals.PlannedCost);
    }

    [Fact]
    public void 課のコストは売上高以外の3区分の合計になる()
    {
        var dept = DepartmentId.New();

        var totals = DepartmentCategoryTotals.FromCategoryAmounts(dept,
            [
                new(dept, BudgetCategory.Revenue, new Money(5_000_000m)),
                new(dept, BudgetCategory.Processing, new Money(1_000_000m)),
                new(dept, BudgetCategory.Outsourcing, new Money(800_000m)),
                new(dept, BudgetCategory.PeriodCost, new Money(200_000m)),
            ],
            [new(dept, BudgetCategory.Outsourcing, new Money(900_000m))]);

        Assert.Equal(2_000_000m, totals.PlannedCost);  // 100 + 80 + 20 万
        Assert.Equal(900_000m, totals.ActualCost);     // 予定外でなく実績のみの区分も含む
        Assert.Equal(5_000_000m, totals.PlannedRevenue);
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

using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class VarianceAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId ProjectA = ProjectId.New();
    private static readonly ProjectId ProjectB = ProjectId.New();
    private static readonly CostElementCode Personnel = new("PERSONNEL");

    private static DepartmentBudget ApprovedBudget(
        params (BudgetCategory Category, ProjectId? Project, CostElementCode? Element, decimal Amount)[] lines)
    {
        var budget = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);
        foreach (var (category, project, element, amount) in lines)
        {
            if (project is { } pid)
                budget.UpsertProjectLine(category, pid, new Money(amount));
            else
                budget.UpsertPeriodCostLine(element!.Value, new Money(amount));
        }
        budget.Approve(Now);
        return budget;
    }

    private static ActualEntry Actual(BudgetCategory category, ProjectId? project,
        CostElementCode? element, decimal amount) =>
        ActualEntry.Record(Dept, Half, category, project, element, null, new Money(amount), null, Now);

    private static readonly BudgetVarianceAnalysisService Service = new();

    [Fact]
    public void 差異は実績金額と予算金額の差として算出される()
    {
        var budget = ApprovedBudget((BudgetCategory.Processing, ProjectA, null, 1_000_000m));
        var report = Service.Analyze(budget, [Actual(BudgetCategory.Processing, ProjectA, null, 1_200_000m)]);

        var processing = report.Categories.Single(c => c.Category == BudgetCategory.Processing);
        var line = Assert.Single(processing.Lines);
        Assert.Equal(1_000_000m, line.PlannedAmount);
        Assert.Equal(1_200_000m, line.ActualAmount);
        Assert.Equal(200_000m, line.Variance);
        Assert.False(line.IsUnplanned);
    }

    [Fact]
    public void 同一キーの実績は合算される()
    {
        var budget = ApprovedBudget((BudgetCategory.PeriodCost, null, Personnel, 1_000_000m));
        var report = Service.Analyze(budget,
        [
            Actual(BudgetCategory.PeriodCost, null, Personnel, 400_000m),
            Actual(BudgetCategory.PeriodCost, null, Personnel, 500_000m),
        ]);

        var periodCost = report.Categories.Single(c => c.Category == BudgetCategory.PeriodCost);
        var line = Assert.Single(periodCost.Lines);
        Assert.Equal(900_000m, line.ActualAmount);
        Assert.Equal(-100_000m, line.Variance);
    }

    [Fact]
    public void 予算にない実績は予定外として区別される()
    {
        var budget = ApprovedBudget((BudgetCategory.Revenue, ProjectA, null, 3_000_000m));
        var report = Service.Analyze(budget, [Actual(BudgetCategory.Revenue, ProjectB, null, 500_000m)]);

        var revenue = report.Categories.Single(c => c.Category == BudgetCategory.Revenue);
        Assert.Equal(2, revenue.Lines.Count);
        var unplanned = revenue.Lines.Single(l => l.ProjectId == ProjectB.Value);
        Assert.True(unplanned.IsUnplanned);
        Assert.Equal(0m, unplanned.PlannedAmount);
    }

    [Fact]
    public void 有利差異の向きは売上とコストで逆になる()
    {
        var budget = ApprovedBudget(
            (BudgetCategory.Revenue, ProjectA, null, 3_000_000m),
            (BudgetCategory.Processing, ProjectA, null, 1_000_000m));
        var report = Service.Analyze(budget,
        [
            Actual(BudgetCategory.Revenue, ProjectA, null, 3_500_000m),    // 売上超過 = 有利
            Actual(BudgetCategory.Processing, ProjectA, null, 1_200_000m), // コスト超過 = 不利
        ]);

        var revenueLine = report.Categories.Single(c => c.Category == BudgetCategory.Revenue).Lines.Single();
        Assert.True(revenueLine.IsFavorable);
        Assert.False(revenueLine.IsAdverse);

        var processingLine = report.Categories.Single(c => c.Category == BudgetCategory.Processing).Lines.Single();
        Assert.False(processingLine.IsFavorable);
        Assert.True(processingLine.IsAdverse);
    }

    [Fact]
    public void 区分サブトータルの合計は全体の売上とコストに一致する()
    {
        var budget = ApprovedBudget(
            (BudgetCategory.Revenue, ProjectA, null, 3_000_000m),
            (BudgetCategory.Revenue, ProjectB, null, 2_000_000m),
            (BudgetCategory.Processing, ProjectA, null, 1_000_000m),
            (BudgetCategory.Outsourcing, ProjectB, null, 800_000m),
            (BudgetCategory.PeriodCost, null, Personnel, 700_000m));
        var report = Service.Analyze(budget,
        [
            Actual(BudgetCategory.Revenue, ProjectA, null, 3_100_000m),
            Actual(BudgetCategory.Processing, ProjectA, null, 900_000m),
            Actual(BudgetCategory.PeriodCost, null, Personnel, 750_000m),
        ]);

        Assert.Equal(5_000_000m, report.PlannedRevenue);
        Assert.Equal(3_100_000m, report.ActualRevenue);
        Assert.Equal(2_500_000m, report.PlannedCost);
        Assert.Equal(1_650_000m, report.ActualCost);
        // 区分サブトータルの合計 = 全体
        var costCategories = report.Categories.Where(c => c.Category != BudgetCategory.Revenue).ToList();
        Assert.Equal(report.PlannedCost, costCategories.Sum(c => c.PlannedAmount));
        Assert.Equal(report.ActualCost, costCategories.Sum(c => c.ActualAmount));
    }
}

public class BudgetComparisonTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId ProjectA = ProjectId.New();
    private static readonly CostElementCode Personnel = new("PERSONNEL");

    private static readonly BudgetComparisonService Service = new();

    [Fact]
    public void バージョン間の増減を区分ごとに算出できる()
    {
        var v1 = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);
        v1.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(3_000_000m));
        v1.UpsertPeriodCostLine(Personnel, new Money(1_000_000m));
        v1.Approve(Now);

        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", Now);
        v2.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(3_500_000m));

        var report = Service.Compare(v1, v2);

        Assert.Equal(1, report.BaseVersion);
        Assert.Equal(2, report.TargetVersion);
        var revenue = report.Categories.Single(c => c.Category == BudgetCategory.Revenue);
        Assert.Equal(500_000m, revenue.Difference);
        var periodCost = report.Categories.Single(c => c.Category == BudgetCategory.PeriodCost);
        Assert.Equal(0m, periodCost.Difference);
    }

    [Fact]
    public void 異なる課や半期の予算は比較できない()
    {
        var a = DepartmentBudget.CreateInitial(Dept, Half, "A", Now);
        var otherDept = DepartmentBudget.CreateInitial(DepartmentId.New(), Half, "B", Now);
        var otherHalf = DepartmentBudget.CreateInitial(Dept, new FiscalHalf(2026, HalfTerm.H2), "C", Now);

        Assert.Throws<DomainException>(() => Service.Compare(a, otherDept));
        Assert.Throws<DomainException>(() => Service.Compare(a, otherHalf));
    }
}

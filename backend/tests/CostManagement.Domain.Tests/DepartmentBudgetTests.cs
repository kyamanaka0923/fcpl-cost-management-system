using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class DepartmentBudgetTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId ProjectA = ProjectId.New();
    private static readonly ProjectId ProjectB = ProjectId.New();
    private static readonly CostElementCode Personnel = new("PERSONNEL");
    private static readonly CostElementCode License = new("LICENSE");

    private static DepartmentBudget NewDraft() =>
        DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);

    [Fact]
    public void 当初予算はバージョン1のドラフトとして作成される()
    {
        var budget = NewDraft();

        Assert.Equal(1, budget.Version);
        Assert.Equal(BudgetStatus.Draft, budget.Status);
        Assert.Equal(Half, budget.FiscalHalf);
        Assert.Empty(budget.Lines);
    }

    [Fact]
    public void 同一区分同一案件の明細は上書きされる()
    {
        var budget = NewDraft();
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_500_000m));

        var line = Assert.Single(budget.Lines);
        Assert.Equal(5_500_000m, line.Amount.Value);
    }

    [Fact]
    public void 同一費目の期間費用明細は上書きされる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(Personnel, new Money(1_000_000m));
        budget.UpsertPeriodCostLine(Personnel, new Money(1_200_000m));

        var line = Assert.Single(budget.Lines);
        Assert.Equal(1_200_000m, line.Amount.Value);
    }

    [Fact]
    public void 区分が異なれば同一案件でも別明細として管理される()
    {
        var budget = NewDraft();
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Processing, ProjectA, new Money(2_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Outsourcing, ProjectA, new Money(1_000_000m));

        Assert.Equal(3, budget.Lines.Count);
    }

    [Fact]
    public void 課の区分合計は案件明細の合計として導出される()
    {
        var budget = NewDraft();
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectB, new Money(3_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Processing, ProjectA, new Money(2_000_000m));
        budget.UpsertPeriodCostLine(Personnel, new Money(1_500_000m));
        budget.UpsertPeriodCostLine(License, new Money(500_000m));

        Assert.Equal(8_000_000m, budget.CategoryTotal(BudgetCategory.Revenue).Value);
        Assert.Equal(2_000_000m, budget.CategoryTotal(BudgetCategory.Processing).Value);
        Assert.Equal(0m, budget.CategoryTotal(BudgetCategory.Outsourcing).Value);
        Assert.Equal(2_000_000m, budget.CategoryTotal(BudgetCategory.PeriodCost).Value);
        Assert.Equal(4_000_000m, budget.TotalCost.Value);
        Assert.Equal(4_000_000m, budget.PlannedProfit.Value);
    }

    [Fact]
    public void 案件別区分の明細を費目指定のメソッドでは登録できない()
    {
        var budget = NewDraft();

        Assert.Throws<DomainException>(() =>
            budget.UpsertProjectLine(BudgetCategory.PeriodCost, ProjectA, new Money(100_000m)));
        Assert.Throws<DomainException>(() =>
            budget.RemoveProjectLine(BudgetCategory.PeriodCost, ProjectA));
    }

    [Fact]
    public void 明細のない予算は承認できない()
    {
        var budget = NewDraft();

        Assert.Throws<DomainException>(() => budget.Approve(Now));
    }

    [Fact]
    public void 承認済みの予算は編集できない()
    {
        var budget = NewDraft();
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));
        budget.Approve(Now);

        Assert.Equal(BudgetStatus.Approved, budget.Status);
        Assert.Throws<DomainException>(() =>
            budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(6_000_000m)));
        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLine(Personnel, new Money(100_000m)));
        Assert.Throws<DomainException>(() =>
            budget.RemoveProjectLine(BudgetCategory.Revenue, ProjectA));
    }

    [Fact]
    public void 改定版は明細を引き継いだ新バージョンのドラフトになる()
    {
        var baseBudget = NewDraft();
        baseBudget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));
        baseBudget.UpsertPeriodCostLine(Personnel, new Money(1_000_000m));
        baseBudget.Approve(Now);

        var revised = DepartmentBudget.ReviseFrom(baseBudget, 2, "下期見直し", Now);

        Assert.Equal(2, revised.Version);
        Assert.Equal(BudgetStatus.Draft, revised.Status);
        Assert.Equal(baseBudget.DepartmentId, revised.DepartmentId);
        Assert.Equal(baseBudget.FiscalHalf, revised.FiscalHalf);
        Assert.Equal(2, revised.Lines.Count);
        // 明細は複製であり、基の予算とは独立している。
        Assert.DoesNotContain(revised.Lines, l => baseBudget.Lines.Any(b => b.Id == l.Id));
    }

    [Fact]
    public void 改定版のバージョンは基より大きくなければならない()
    {
        var baseBudget = NewDraft();
        baseBudget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));

        Assert.Throws<DomainException>(() =>
            DepartmentBudget.ReviseFrom(baseBudget, 1, "改定", Now));
    }

    [Fact]
    public void 承認済みの予算のみ失効にできる()
    {
        var budget = NewDraft();
        budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(5_000_000m));

        Assert.Throws<DomainException>(budget.Supersede);

        budget.Approve(Now);
        budget.Supersede();
        Assert.Equal(BudgetStatus.Superseded, budget.Status);
    }

    [Fact]
    public void 負の金額は登録できない()
    {
        var budget = NewDraft();

        Assert.Throws<DomainException>(() =>
            budget.UpsertProjectLine(BudgetCategory.Revenue, ProjectA, new Money(-1m)));
        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLine(Personnel, new Money(-1m)));
    }

    [Fact]
    public void 存在しない明細は削除できない()
    {
        var budget = NewDraft();

        Assert.Throws<DomainException>(() =>
            budget.RemoveProjectLine(BudgetCategory.Revenue, ProjectA));
        Assert.Throws<DomainException>(() => budget.RemovePeriodCostLine(Personnel));
    }

    [Fact]
    public void ラベルは必須である()
    {
        Assert.Throws<DomainException>(() =>
            DepartmentBudget.CreateInitial(Dept, Half, " ", Now));
    }
}

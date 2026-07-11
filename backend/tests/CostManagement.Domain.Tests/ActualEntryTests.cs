using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class ActualEntryTests
{
    private static readonly DateTime Now = new(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId ProjectA = ProjectId.New();
    private static readonly CostElementCode Personnel = new("PERSONNEL");

    [Fact]
    public void 案件別区分の実績は案件を指定して計上できる()
    {
        var entry = ActualEntry.Record(Dept, Half, BudgetCategory.Processing, ProjectA, null,
            new Money(800_000m), "5月分", Now);

        Assert.Equal(BudgetCategory.Processing, entry.Category);
        Assert.Equal(ProjectA, entry.ProjectId);
        Assert.Null(entry.ElementCode);
        Assert.Equal(800_000m, entry.Amount.Value);
        Assert.Equal("5月分", entry.Note);
    }

    [Fact]
    public void 期間費用の実績は費目を指定して計上できる()
    {
        var entry = ActualEntry.Record(Dept, Half, BudgetCategory.PeriodCost, null, Personnel,
            new Money(500_000m), null, Now);

        Assert.Null(entry.ProjectId);
        Assert.Equal(Personnel, entry.ElementCode);
    }

    [Fact]
    public void 案件別区分の実績に案件がなければ計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, null, null,
                new Money(100_000m), null, Now));
    }

    [Fact]
    public void 案件別区分の実績に費目は指定できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, Personnel,
                new Money(100_000m), null, Now));
    }

    [Fact]
    public void 期間費用の実績に費目がなければ計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.PeriodCost, null, null,
                new Money(100_000m), null, Now));
    }

    [Fact]
    public void 期間費用の実績に案件は指定できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.PeriodCost, ProjectA, Personnel,
                new Money(100_000m), null, Now));
    }

    [Fact]
    public void 負の金額は計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, null,
                new Money(-1m), null, Now));
    }

    [Fact]
    public void 空白の備考はnullに正規化される()
    {
        var entry = ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, null,
            new Money(100_000m), "  ", Now);

        Assert.Null(entry.Note);
    }
}

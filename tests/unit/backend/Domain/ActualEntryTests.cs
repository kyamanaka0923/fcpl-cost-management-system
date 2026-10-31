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
            null, new Money(800_000m), "5月分", Now);

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
            null, new Money(500_000m), null, Now);

        Assert.Null(entry.ProjectId);
        Assert.Equal(Personnel, entry.ElementCode);
    }

    [Fact]
    public void 案件別区分の実績に案件がなければ計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, null, null,
                null, new Money(100_000m), null, Now));
    }

    [Fact]
    public void 案件別区分の実績に費目は指定できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, Personnel,
                null, new Money(100_000m), null, Now));
    }

    [Fact]
    public void 期間費用の実績に費目がなければ計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.PeriodCost, null, null,
                null, new Money(100_000m), null, Now));
    }

    [Fact]
    public void 期間費用の実績に案件は指定できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.PeriodCost, ProjectA, Personnel,
                null, new Money(100_000m), null, Now));
    }

    [Fact]
    public void 負の金額は計上できない()
    {
        Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, null,
                null, new Money(-1m), null, Now));
    }

    [Theory]
    [InlineData("0", null, 0)]
    [InlineData("1", null, 1)]
    [InlineData("2000000", 1, 2_000_000)]
    [InlineData("100.0", null, 100)]
    public void 計上金額が0以上の1円単位の整数の値なら半期一括でも月別でも計上できる(string text, int? month, int expected)
    {
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        var entry = ActualEntry.Record(Dept, Half, BudgetCategory.Processing, ProjectA, null,
            month, new Money(amount), null, Now);

        Assert.Equal((decimal)expected, entry.Amount.Value);
        Assert.Equal(month, entry.Month);
    }

    [Theory]
    [InlineData("-1", null, "金額は0以上で入力してください。")]
    [InlineData("-0.5", null, "金額は0以上で入力してください。")]
    [InlineData("0.1", null, "金額は1円単位の整数で入力してください。")]
    [InlineData("100.5", null, "金額は1円単位の整数で入力してください。")]
    [InlineData("100.5", 1, "金額は1円単位の整数で入力してください。")]
    public void 計上金額が0未満か1円未満の端数つきなら計上を拒否する(string text, int? month, string message)
    {
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        var error = Assert.Throws<DomainException>(() =>
            ActualEntry.Record(Dept, Half, BudgetCategory.Processing, ProjectA, null,
                month, new Money(amount), null, Now));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void 空白の備考はnullに正規化される()
    {
        var entry = ActualEntry.Record(Dept, Half, BudgetCategory.Revenue, ProjectA, null,
            null, new Money(100_000m), "  ", Now);

        Assert.Null(entry.Note);
    }
}

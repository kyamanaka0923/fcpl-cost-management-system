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
    public void 明細を月次金額で登録すると半期合計は月次の合計になる()
    {
        var budget = NewDraft();
        budget.UpsertProjectLineMonthly(BudgetCategory.Revenue, ProjectA,
            new Dictionary<int, Money> { [1] = new(1_000_000m), [2] = new(1_500_000m), [3] = new(500_000m) });

        var line = Assert.Single(budget.Lines);
        Assert.True(line.IsMonthly);
        Assert.Equal(3_000_000m, line.Amount.Value);
        Assert.Equal(3_000_000m, budget.CategoryTotal(BudgetCategory.Revenue).Value);
        Assert.Equal(3, line.MonthlyAmounts.Count);
    }

    [Fact]
    public void 月次明細を半期一括で上書きすると月次モードは解除される()
    {
        var budget = NewDraft();
        budget.UpsertProjectLineMonthly(BudgetCategory.Processing, ProjectA,
            new Dictionary<int, Money> { [1] = new(200_000m), [2] = new(300_000m) });
        budget.UpsertProjectLine(BudgetCategory.Processing, ProjectA, new Money(1_000_000m));

        var line = Assert.Single(budget.Lines);
        Assert.False(line.IsMonthly);
        Assert.Empty(line.MonthlyAmounts);
        Assert.Equal(1_000_000m, line.Amount.Value);
    }

    [Fact]
    public void 半期一括の期間費用明細を月次で上書きできる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(Personnel, new Money(600_000m));
        budget.UpsertPeriodCostLineMonthly(Personnel,
            new Dictionary<int, Money> { [4] = new(100_000m), [5] = new(100_000m) });

        var line = Assert.Single(budget.Lines);
        Assert.True(line.IsMonthly);
        Assert.Equal(200_000m, line.Amount.Value);
    }

    [Fact]
    public void 範囲外の月を指定した月次明細は登録できない()
    {
        var budget = NewDraft();
        Assert.Throws<DomainException>(() =>
            budget.UpsertProjectLineMonthly(BudgetCategory.Revenue, ProjectA,
                new Dictionary<int, Money> { [7] = new(100_000m) }));
    }

    [Fact]
    public void 不正な月次金額での上書きが拒否されても既存明細の金額と月次は変わらない()
    {
        // プロパティベーステストで検出: 検証前に月別金額を書き換えていたため、
        // 例外後に「半期合計 = 月次の合計」が崩れた明細が残っていた。
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");

        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLineMonthly(License,
                new Dictionary<int, Money> { [1] = new(100_000m), [7] = new(50_000m) }, "AWS"));
        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLineMonthly(License,
                new Dictionary<int, Money> { [1] = new(100_000m), [2] = new(-1m) }, "AWS"));

        var line = Assert.Single(budget.Lines);
        Assert.Equal(300_000m, line.Amount.Value);
        Assert.False(line.IsMonthly);
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

    [Fact]
    public void 期間費用は費目内で明細名ごとに複数登録でき費目合計は明細の合計になる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");
        budget.UpsertPeriodCostLine(License, new Money(200_000m), "GitHub");

        Assert.Equal(2, budget.Lines.Count(l => l.Category == BudgetCategory.PeriodCost));
        Assert.Equal(500_000m, budget.CategoryTotal(BudgetCategory.PeriodCost).Value);
        Assert.Contains(budget.Lines, l => l.PeriodDetail == "AWS" && l.Amount.Value == 300_000m);
    }

    [Fact]
    public void 同一費目同一明細名は上書きされる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");
        budget.UpsertPeriodCostLine(License, new Money(350_000m), "AWS");

        var line = Assert.Single(budget.Lines);
        Assert.Equal("AWS", line.PeriodDetail);
        Assert.Equal(350_000m, line.Amount.Value);
    }

    [Fact]
    public void 費目一括の費目に明細を足すことはできない()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(500_000m));

        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS"));
    }

    [Fact]
    public void 明細のある費目に費目一括を足すことはできない()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");

        Assert.Throws<DomainException>(() =>
            budget.UpsertPeriodCostLine(License, new Money(500_000m)));
    }

    [Fact]
    public void 費目一括と明細は費目が異なれば併存できる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(Personnel, new Money(1_000_000m));
        budget.UpsertPeriodCostLine(License, new Money(200_000m), "AWS");

        Assert.Equal(1_200_000m, budget.CategoryTotal(BudgetCategory.PeriodCost).Value);
    }

    [Fact]
    public void 期間費用の明細を月次で登録でき半期合計は月次の合計になる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLineMonthly(License,
            new Dictionary<int, Money> { [1] = new(100_000m), [2] = new(150_000m) }, "AWS");

        var line = Assert.Single(budget.Lines);
        Assert.True(line.IsMonthly);
        Assert.Equal("AWS", line.PeriodDetail);
        Assert.Equal(250_000m, line.Amount.Value);
    }

    [Fact]
    public void 期間費用の明細を明細名指定で削除できる()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");
        budget.UpsertPeriodCostLine(License, new Money(200_000m), "GitHub");

        budget.RemovePeriodCostLine(License, "AWS");

        var line = Assert.Single(budget.Lines);
        Assert.Equal("GitHub", line.PeriodDetail);
    }

    [Fact]
    public void 期間費用の明細名を変更でき金額と月次は保持される()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLineMonthly(License,
            new Dictionary<int, Money> { [1] = new(100_000m), [2] = new(150_000m) }, "AWS");

        budget.RenamePeriodCostDetail(License, "AWS", "AWS本番");

        var line = Assert.Single(budget.Lines);
        Assert.Equal("AWS本番", line.PeriodDetail);
        Assert.True(line.IsMonthly);
        Assert.Equal(250_000m, line.Amount.Value);
    }

    [Fact]
    public void 同一費目に既にある明細名へは変更できない()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");
        budget.UpsertPeriodCostLine(License, new Money(200_000m), "GitHub");

        Assert.Throws<DomainException>(() =>
            budget.RenamePeriodCostDetail(License, "AWS", "GitHub"));
    }

    [Fact]
    public void 存在しない明細名は変更できない()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");

        Assert.Throws<DomainException>(() =>
            budget.RenamePeriodCostDetail(License, "GitHub", "GCP"));
    }

    [Fact]
    public void 明細名を空へは変更できない()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");

        Assert.Throws<DomainException>(() =>
            budget.RenamePeriodCostDetail(License, "AWS", " "));
    }

    [Fact]
    public void 改定版は期間費用の明細名を引き継ぐ()
    {
        var budget = NewDraft();
        budget.UpsertPeriodCostLine(License, new Money(300_000m), "AWS");
        budget.Approve(Now);

        var revised = DepartmentBudget.ReviseFrom(budget, 2, "改定", Now);

        var line = Assert.Single(revised.Lines);
        Assert.Equal("AWS", line.PeriodDetail);
        Assert.Equal(300_000m, line.Amount.Value);
    }
}

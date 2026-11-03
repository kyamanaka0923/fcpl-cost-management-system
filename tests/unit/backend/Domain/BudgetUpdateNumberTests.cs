using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

/// <summary>
/// 予算の更新番号と楽観ロック(BR-BUD-008・SPEC-004)。
/// テスト名の「行 n」は SPEC-004 の具体例の行番号。予算 P は具体例の前提
/// (売上高 × PJ-A = 1,000,000、売上高 × PJ-B = 500,000、期間費用 × LICENSE × AWS = 300,000 のドラフト)。
/// </summary>
public class 予算の更新番号と楽観ロック
{
    private const string 衝突の文言 =
        "ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。";
    private const string 編集の状態の文言 = "承認済み・失効済みの予算は編集できません。改定版を作成してください。";
    private const string 承認の状態の文言 = "ドラフト状態の予算のみ承認できます。";

    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DepartmentId Dept = DepartmentId.New();
    private static readonly FiscalHalf Half = new(2026, HalfTerm.H1);
    private static readonly ProjectId PjA = ProjectId.New();
    private static readonly ProjectId PjB = ProjectId.New();
    private static readonly CostElementCode License = new("LICENSE");

    /// <summary>具体例の予算 P(Draft・明細 3 件)を作る。</summary>
    private static DepartmentBudget 予算P()
    {
        var p = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);
        p.UpsertProjectLine(p.UpdateNumber, BudgetCategory.Revenue, PjA, new Money(1_000_000m));
        p.UpsertProjectLine(p.UpdateNumber, BudgetCategory.Revenue, PjB, new Money(500_000m));
        p.UpsertPeriodCostLine(p.UpdateNumber, License, new Money(300_000m), "AWS");
        return p;
    }

    /// <summary>明細を (区分, 案件, 費目, 明細名, 金額) の並びにして比べられるようにする。</summary>
    private static List<(BudgetCategory, ProjectId?, CostElementCode?, string?, decimal)> 明細(DepartmentBudget b) =>
        b.Lines.Select(l => (l.Category, l.ProjectId, l.ElementCode, l.PeriodDetail, l.Amount.Value))
            .OrderBy(x => x.Category).ThenBy(x => x.Item5).ToList();

    /// <summary>操作が衝突として拒否され、明細・状態・更新番号が変わらないことを確かめる。</summary>
    private static void 衝突として拒否され何も変わらない(DepartmentBudget b, Action 操作)
    {
        var 明細の前 = 明細(b);
        var 状態の前 = b.Status;
        var 番号の前 = b.UpdateNumber;

        var ex = Assert.Throws<BudgetConflictException>(操作);

        Assert.Equal(衝突の文言, ex.Message);
        Assert.Equal(明細の前, 明細(b));
        Assert.Equal(状態の前, b.Status);
        Assert.Equal(番号の前, b.UpdateNumber);
    }

    /// <summary>操作が状態の拒否(DomainException)になり、明細・状態・更新番号が変わらないことを確かめる。</summary>
    private static void 状態の拒否になり何も変わらない(DepartmentBudget b, string 文言, Action 操作)
    {
        var 明細の前 = 明細(b);
        var 状態の前 = b.Status;
        var 番号の前 = b.UpdateNumber;

        var ex = Assert.Throws<DomainException>(操作);

        Assert.Equal(文言, ex.Message);
        Assert.Equal(明細の前, 明細(b));
        Assert.Equal(状態の前, b.Status);
        Assert.Equal(番号の前, b.UpdateNumber);
    }

    // ---- 更新番号の初期値と増え方(行 1〜7) ----

    [Fact]
    public void 行1_当初予算のドラフトを起票すると更新番号は1で始まる()
    {
        var b = DepartmentBudget.CreateInitial(Dept, Half, "当初予算", Now);

        Assert.Equal(1, b.UpdateNumber);
    }

    [Fact]
    public void 行2_改定版を起票すると改定版の更新番号は1で始まり引き継ぎ元の更新番号は変わらない()
    {
        var v1 = 予算P();
        v1.Approve(v1.UpdateNumber, Now);
        var m = v1.UpdateNumber;

        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", Now);

        Assert.Equal(1, v2.UpdateNumber);
        Assert.Equal(m, v1.UpdateNumber);
    }

    [Fact]
    public void 行3_一致する番号での明細の保存は受け付けて更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        Assert.Equal(n + 1, p.UpdateNumber);
        Assert.Equal(1_200_000m,
            p.Lines.Single(l => l.ProjectId == PjA).Amount.Value);
    }

    [Fact]
    public void 行4_金額0の明細の保存でも更新番号は1増える()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, Money.Zero);

        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 月次での明細の保存も更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.UpsertProjectLineMonthly(n, BudgetCategory.Revenue, PjA,
            new Dictionary<int, Money> { [1] = new(100m) });
        p.UpsertPeriodCostLineMonthly(n + 1, License,
            new Dictionary<int, Money> { [2] = new(200m) }, "AWS");

        Assert.Equal(n + 2, p.UpdateNumber);
    }

    [Fact]
    public void 行5_明細の削除は受け付けて更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.RemoveProjectLine(n, BudgetCategory.Revenue, PjB);

        Assert.Equal(n + 1, p.UpdateNumber);
        Assert.DoesNotContain(p.Lines, l => l.ProjectId == PjB);
    }

    [Fact]
    public void 期間費用の明細の削除も更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.RemovePeriodCostLine(n, License, "AWS");

        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 行6_明細名の変更は受け付けて更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.RenamePeriodCostDetail(n, License, "AWS", "AWS本番");

        Assert.Equal(n + 1, p.UpdateNumber);
        Assert.Contains(p.Lines, l => l.PeriodDetail == "AWS本番");
    }

    [Fact]
    public void 行7_承認は受け付けて更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.Approve(n, Now);

        Assert.Equal(BudgetStatus.Approved, p.Status);
        Assert.Equal(n + 1, p.UpdateNumber);
    }

    // ---- 衝突の拒否(行 8〜19) ----

    [Fact]
    public void 行8_ほかの保存のあとに古い番号で同じ明細を保存すると衝突として拒否される()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(900_000m)));
        Assert.Equal(1_200_000m, p.Lines.Single(l => l.ProjectId == PjA).Amount.Value);
        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 行9_ほかの保存のあとに古い番号で別の明細を保存しても衝突として拒否される()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjB, new Money(600_000m)));
        Assert.Equal(500_000m, p.Lines.Single(l => l.ProjectId == PjB).Amount.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void 行10と11_予算より大きい番号や負の番号で保存しても衝突として拒否される(int ずれ)
    {
        var p = 予算P();
        var 送る番号 = ずれ > 0 ? p.UpdateNumber + ずれ : ずれ;

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(送る番号, BudgetCategory.Revenue, PjA, new Money(900_000m)));
    }

    [Fact]
    public void 行12_ほかの保存のあとの古い番号での明細の削除は衝突として拒否され明細は残る()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p, () => p.RemoveProjectLine(n, BudgetCategory.Revenue, PjB));
        衝突として拒否され何も変わらない(p, () => p.RemovePeriodCostLine(n, License, "AWS"));
        Assert.Contains(p.Lines, l => l.ProjectId == PjB);
    }

    [Fact]
    public void 行13_ほかの保存のあとの古い番号での明細名の変更は衝突として拒否され明細名は変わらない()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p, () => p.RenamePeriodCostDetail(n, License, "AWS", "AWS本番"));
        Assert.Contains(p.Lines, l => l.PeriodDetail == "AWS");
    }

    [Fact]
    public void 行14_明細の削除のあとの古い番号での保存は衝突として拒否される()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.RemoveProjectLine(n, BudgetCategory.Revenue, PjB);

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(900_000m)));
        Assert.Equal(1_000_000m, p.Lines.Single(l => l.ProjectId == PjA).Amount.Value);
        Assert.DoesNotContain(p.Lines, l => l.ProjectId == PjB);
    }

    [Fact]
    public void 行15_明細名の変更のあとの古い番号での保存は衝突として拒否される()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.RenamePeriodCostDetail(n, License, "AWS", "AWS本番");

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(900_000m)));
        Assert.Contains(p.Lines, l => l.PeriodDetail == "AWS本番");
    }

    [Fact]
    public void 行16_ほかの保存のあとの古い番号での承認は衝突として拒否されドラフトのまま()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p, () => p.Approve(n, Now));
        Assert.Equal(BudgetStatus.Draft, p.Status);
        Assert.Null(p.ApprovedAt);
    }

    [Fact]
    public void 行17_明細をすべて削除したあとの古い番号での承認には明細のない予算の拒否ではなく衝突の拒否を返す()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.RemoveProjectLine(n, BudgetCategory.Revenue, PjA);
        p.RemoveProjectLine(n + 1, BudgetCategory.Revenue, PjB);
        p.RemovePeriodCostLine(n + 2, License, "AWS");
        Assert.Equal(n + 3, p.UpdateNumber);

        衝突として拒否され何も変わらない(p, () => p.Approve(n, Now));
    }

    [Fact]
    public void 行18_衝突と金額の拒否が重なった保存には衝突の拒否を返す()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjB, new Money(-1m)));
        Assert.Equal(500_000m, p.Lines.Single(l => l.ProjectId == PjB).Amount.Value);
    }

    [Fact]
    public void 行19_衝突と費目一括と明細名ありの併用の拒否が重なった保存には衝突の拒否を返す()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(1_200_000m));

        衝突として拒否され何も変わらない(p, () => p.UpsertPeriodCostLine(n, License, new Money(100m)));
    }

    // ---- 一致していてほかの拒否・状態の拒否(行 21〜24・26) ----

    [Fact]
    public void 行21_番号が一致していて金額の拒否に当たると金額の拒否を返し更新番号は変わらない()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        var ex = Assert.Throws<DomainException>(
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjB, new Money(-1m)));

        Assert.Equal("金額は0以上で入力してください。", ex.Message);
        Assert.Equal(n, p.UpdateNumber);
        Assert.Equal(500_000m, p.Lines.Single(l => l.ProjectId == PjB).Amount.Value);
    }

    [Fact]
    public void 一致していて明細がない削除や明細名の重複に当たると更新番号は変わらない()
    {
        var p = 予算P();
        p.UpsertPeriodCostLine(p.UpdateNumber, License, new Money(1m), "GitHub");
        var n = p.UpdateNumber;

        Assert.Throws<DomainException>(() => p.RemoveProjectLine(n, BudgetCategory.Processing, PjA));
        Assert.Throws<DomainException>(() => p.RenamePeriodCostDetail(n, License, "AWS", "GitHub"));

        Assert.Equal(n, p.UpdateNumber);
    }

    [Fact]
    public void 行22_承認済みの予算への一致する番号での明細の保存は状態の拒否になる()
    {
        var p = 予算P();
        p.Approve(p.UpdateNumber, Now);

        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.UpsertProjectLine(p.UpdateNumber, BudgetCategory.Revenue, PjA, new Money(900_000m)));
    }

    [Fact]
    public void 行23_承認済みの予算への古い番号での保存と削除と明細名の変更は衝突ではなく状態の拒否になる()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.Approve(n, Now);

        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.UpsertProjectLine(n, BudgetCategory.Revenue, PjA, new Money(900_000m)));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.UpsertProjectLineMonthly(n, BudgetCategory.Revenue, PjA,
                new Dictionary<int, Money> { [1] = new(1m) }));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.UpsertPeriodCostLine(n, License, new Money(1m), "AWS"));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.UpsertPeriodCostLineMonthly(n, License,
                new Dictionary<int, Money> { [1] = new(1m) }, "AWS"));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.RemoveProjectLine(n, BudgetCategory.Revenue, PjB));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.RemovePeriodCostLine(n, License, "AWS"));
        状態の拒否になり何も変わらない(p, 編集の状態の文言,
            () => p.RenamePeriodCostDetail(n, License, "AWS", "AWS本番"));
        状態の拒否になり何も変わらない(p, 編集の状態の文言, () => p.EnsureSavable(n));
        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 行24_承認済みの予算への古い番号での承認は衝突ではなくドラフトでない予算の承認として拒否される()
    {
        var p = 予算P();
        var n = p.UpdateNumber;
        p.Approve(n, Now);

        状態の拒否になり何も変わらない(p, 承認の状態の文言, () => p.Approve(n, Now));
        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 行26_失効した予算への古い番号での保存と承認は状態の拒否になる()
    {
        var v1 = 予算P();
        var 読んだ番号 = v1.UpdateNumber;
        v1.Approve(読んだ番号, Now);
        v1.Supersede();

        状態の拒否になり何も変わらない(v1, 編集の状態の文言,
            () => v1.UpsertProjectLine(読んだ番号, BudgetCategory.Revenue, PjA, new Money(900_000m)));
        状態の拒否になり何も変わらない(v1, 承認の状態の文言, () => v1.Approve(読んだ番号, Now));
        Assert.Equal(BudgetStatus.Superseded, v1.Status);
    }

    [Fact]
    public void 行27_失効は保存ではないので旧承認版の更新番号は変わらない()
    {
        var v1 = 予算P();
        v1.Approve(v1.UpdateNumber, Now);
        var m = v1.UpdateNumber;

        v1.Supersede();

        Assert.Equal(BudgetStatus.Superseded, v1.Status);
        Assert.Equal(m, v1.UpdateNumber);
    }

    [Fact]
    public void 同じ明細名への変更も保存として受け付けて更新番号を1増やす()
    {
        var p = 予算P();
        var n = p.UpdateNumber;

        p.RenamePeriodCostDetail(n, License, "AWS", "AWS");

        Assert.Equal(n + 1, p.UpdateNumber);
    }

    [Fact]
    public void 復元した予算は保存されている更新番号を持つ()
    {
        var restored = DepartmentBudget.Restore(Guid.NewGuid(), Dept.Value, "2026-H1", 1, "当初予算",
            BudgetStatus.Draft, Now, null, 42, []);

        Assert.Equal(42, restored.UpdateNumber);
        Assert.Throws<BudgetConflictException>(() => restored.EnsureSavable(41));
        restored.EnsureSavable(42);
    }

    [Fact]
    public void 衝突の例外は不変条件違反のDomainExceptionと区別できる()
    {
        Assert.False(typeof(DomainException).IsAssignableFrom(typeof(BudgetConflictException)));
    }
}

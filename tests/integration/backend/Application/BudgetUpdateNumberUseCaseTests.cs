using CostManagement.Application.Common;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>
/// ユースケース: 予算への保存の楽観ロック(BR-BUD-008・SPEC-004)。
/// テスト名の「行 n」は SPEC-004 の具体例の行番号。
/// </summary>
public class 予算への保存の楽観ロック : IDisposable
{
    private const string 衝突の文言 =
        "ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。";

    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    /// <summary>具体例の予算 P(Draft。売上高 × PJ-A・PJ-B と期間費用 × LICENSE × AWS)と課・案件を作る。</summary>
    private async Task<(DepartmentDto 課, ProjectDto PjA, BudgetDetailDto P)> 予算Pを作る()
    {
        var dept = await _fx.部と課を作成();
        var pjA = await _fx.案件を作成(dept.Id, "PJ-A", "案件A");
        var pjB = await _fx.案件を作成(dept.Id, "PJ-B", "案件B");
        var p = await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "当初予算"));
        p = await _fx.Budgets.UpsertLineAsync(p.Id, new UpsertBudgetLineRequest("Revenue", pjA.Id, null, 1_000_000m), p.UpdateNumber);
        p = await _fx.Budgets.UpsertLineAsync(p.Id, new UpsertBudgetLineRequest("Revenue", pjB.Id, null, 500_000m), p.UpdateNumber);
        p = await _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "LICENSE", 300_000m, null, "AWS"), p.UpdateNumber);
        return (dept, pjA, p);
    }

    /// <summary>明細と更新番号を比べられる形にする。</summary>
    private static (int, string) 内容(BudgetDetailDto b) =>
        (b.UpdateNumber, string.Join(" | ", b.Lines
            .OrderBy(l => l.Category, StringComparer.Ordinal).ThenBy(l => l.Amount)
            .Select(l => $"{l.Category}:{l.ProjectId}:{l.ElementCode}:{l.PeriodDetail}:{l.Amount}")));

    [Fact]
    public async Task 行1_起票した予算の更新番号は1である()
    {
        var dept = await _fx.部と課を作成();

        var budget = await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "当初予算"));

        Assert.Equal(1, budget.UpdateNumber);
        Assert.Equal(1, (await _fx.Budgets.GetAsync(budget.Id)).UpdateNumber);
        Assert.Equal(1, (await _fx.Budgets.ListAsync(dept.Id, "2026-H1")).Single().UpdateNumber);
    }

    [Fact]
    public async Task 行3_受け付けた保存の応答と読み直した予算と一覧の行は同じ1増えた更新番号を持つ()
    {
        var (dept, pjA, p) = await 予算Pを作る();
        var n = p.UpdateNumber;

        var res = await _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("Revenue", pjA.Id, null, 1_200_000m), n);

        Assert.Equal(n + 1, res.UpdateNumber);
        Assert.Equal(n + 1, (await _fx.Budgets.GetAsync(p.Id)).UpdateNumber);
        Assert.Equal(n + 1, (await _fx.Budgets.ListAsync(dept.Id, "2026-H1")).Single().UpdateNumber);
    }

    [Fact]
    public async Task 行2_改定版を起票しても引き継ぎ元の更新番号は変わらず改定版は1で始まる()
    {
        var (dept, _, p) = await 予算Pを作る();
        var v1 = await _fx.Budgets.ApproveAsync(p.Id, p.UpdateNumber);
        var m = v1.UpdateNumber;

        var v2 = await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "見直し"));

        Assert.Equal(1, v2.UpdateNumber);
        Assert.Equal(m, (await _fx.Budgets.GetAsync(v1.Id)).UpdateNumber);
    }

    [Fact]
    public async Task 行20_衝突と存在しない案件の拒否が重なった保存には衝突の拒否を返す()
    {
        var (_, pjA, p) = await 予算Pを作る();
        var n = p.UpdateNumber;
        await _fx.Budgets.UpsertLineAsync(p.Id, new UpsertBudgetLineRequest("Revenue", pjA.Id, null, 1_200_000m), n);
        var before = 内容(await _fx.Budgets.GetAsync(p.Id));

        var ex = await Assert.ThrowsAsync<BudgetConflictException>(() => _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("Revenue", Guid.NewGuid(), null, 100m), n));

        Assert.Equal(衝突の文言, ex.Message);
        Assert.Equal(before, 内容(await _fx.Budgets.GetAsync(p.Id)));
    }

    [Fact]
    public async Task 衝突と不正な区分や存在しない費目の拒否が重なった保存と削除には衝突の拒否を返す()
    {
        var (_, pjA, p) = await 予算Pを作る();
        var n = p.UpdateNumber;
        await _fx.Budgets.UpsertLineAsync(p.Id, new UpsertBudgetLineRequest("Revenue", pjA.Id, null, 1_200_000m), n);
        var before = 内容(await _fx.Budgets.GetAsync(p.Id));

        await Assert.ThrowsAsync<BudgetConflictException>(() => _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("Foo", pjA.Id, null, 100m), n));
        await Assert.ThrowsAsync<BudgetConflictException>(() => _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "NOPE", 100m), n));
        await Assert.ThrowsAsync<BudgetConflictException>(() =>
            _fx.Budgets.RemoveLineAsync(p.Id, "Foo", null, null, null, n));
        await Assert.ThrowsAsync<BudgetConflictException>(() => _fx.Budgets.RenamePeriodDetailAsync(p.Id,
            new RenamePeriodDetailRequest("", "AWS", "AWS本番"), n));

        Assert.Equal(before, 内容(await _fx.Budgets.GetAsync(p.Id)));
    }

    [Fact]
    public async Task 行25_承認済みの予算への不正な区分の保存には区分の拒否ではなく状態の拒否を返す()
    {
        var (_, _, p) = await 予算Pを作る();
        var n = p.UpdateNumber;
        await _fx.Budgets.ApproveAsync(p.Id, n);
        var before = 内容(await _fx.Budgets.GetAsync(p.Id));

        var ex = await Assert.ThrowsAsync<DomainException>(() => _fx.Budgets.UpsertLineAsync(p.Id,
            new UpsertBudgetLineRequest("Foo", null, null, 100m), n));
        var 削除 = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Budgets.RemoveLineAsync(p.Id, "Foo", null, null, null, n));

        Assert.Equal("承認済み・失効済みの予算は編集できません。改定版を作成してください。", ex.Message);
        Assert.Equal(ex.Message, 削除.Message);
        Assert.Equal(before, 内容(await _fx.Budgets.GetAsync(p.Id)));
    }

    [Fact]
    public async Task 行27_改定版の承認で旧承認版は失効するが旧承認版の更新番号は変わらない()
    {
        var (dept, _, p) = await 予算Pを作る();
        var v1 = await _fx.Budgets.ApproveAsync(p.Id, p.UpdateNumber);
        var m = v1.UpdateNumber;
        var v2 = await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "見直し"));
        var k = v2.UpdateNumber;

        var approved = await _fx.Budgets.ApproveAsync(v2.Id, k);

        Assert.Equal("Approved", approved.Status);
        Assert.Equal(k + 1, approved.UpdateNumber);
        var old = await _fx.Budgets.GetAsync(v1.Id);
        Assert.Equal("Superseded", old.Status);
        Assert.Equal(m, old.UpdateNumber);
    }

    [Fact]
    public async Task 行28_改定版の承認が衝突で拒否されると改定版はドラフトのまま旧承認版は失効しない()
    {
        var (dept, pjA, p) = await 予算Pを作る();
        var v1 = await _fx.Budgets.ApproveAsync(p.Id, p.UpdateNumber);
        var v2 = await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "見直し"));
        var k = v2.UpdateNumber;
        await _fx.Budgets.UpsertLineAsync(v2.Id, new UpsertBudgetLineRequest("Revenue", pjA.Id, null, 900_000m), k);

        await Assert.ThrowsAsync<BudgetConflictException>(() => _fx.Budgets.ApproveAsync(v2.Id, k));

        Assert.Equal("Draft", (await _fx.Budgets.GetAsync(v2.Id)).Status);
        var old = await _fx.Budgets.GetAsync(v1.Id);
        Assert.Equal("Approved", old.Status);
        Assert.Equal(v1.UpdateNumber, old.UpdateNumber);
    }

    [Fact]
    public async Task 行32_存在しない予算への保存は予算がない拒否になる()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _fx.Budgets.UpsertLineAsync(Guid.NewGuid(),
            new UpsertBudgetLineRequest("Revenue", Guid.NewGuid(), null, 1m), 1));

        Assert.Contains("予算が見つかりません", ex.Message);
    }
}

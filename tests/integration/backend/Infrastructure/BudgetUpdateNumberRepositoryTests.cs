using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Repositories;

namespace CostManagement.Infrastructure.Tests;

/// <summary>
/// 永続化アダプタ: 予算の更新番号と、読んでから書くまでの間の保存の衝突(SPEC-004 の「衝突を確実に見つける」
/// 「承認の書き込みを 1 つにまとめる」。具体例の行 29・30)。並列の実行には頼らず、同じ予算を 2 回読み込んだ
/// 2 つの集約を続けて書き込んで確かめる。
/// </summary>
public class 予算の更新番号の永続化 : IDisposable
{
    private readonly RepositoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    /// <summary>明細 1 件のドラフトを保存して返す。</summary>
    private async Task<(DepartmentBudget 予算, Project 案件, DepartmentBudgetRepository repo)> ドラフトを保存()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new DepartmentBudgetRepository(_fx.Factory);
        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLine(budget.UpdateNumber, BudgetCategory.Revenue, project.Id, new Money(1_000_000m));
        await repo.AddAsync(budget);
        return (budget, project, repo);
    }

    private static decimal 売上高(DepartmentBudget b) => b.CategoryTotal(BudgetCategory.Revenue).Value;

    [Fact]
    public async Task 更新番号は保存と復元で往復する()
    {
        var (budget, project, repo) = await ドラフトを保存();
        var 読んだ番号 = budget.UpdateNumber;
        budget.UpsertProjectLine(読んだ番号, BudgetCategory.Revenue, project.Id, new Money(2_000_000m));

        await repo.UpdateAsync(budget, 読んだ番号);

        var restored = await repo.FindByIdAsync(budget.Id);
        Assert.Equal(読んだ番号 + 1, restored!.UpdateNumber);
        Assert.Equal(読んだ番号 + 1, (await repo.ListAsync(budget.DepartmentId, _fx.Half)).Single().UpdateNumber);
    }

    [Fact]
    public async Task 行29_同じ番号を読んだ2つの保存は先に書いた方だけを受け付け後の方は何も書かずに衝突になる()
    {
        var (budget, project, repo) = await ドラフトを保存();
        var 画面A = (await repo.FindByIdAsync(budget.Id))!;
        var 画面B = (await repo.FindByIdAsync(budget.Id))!;
        var n = 画面A.UpdateNumber;
        // どちらも順 2(集約での番号の比較)を通る
        画面A.UpsertProjectLine(n, BudgetCategory.Revenue, project.Id, new Money(1_200_000m));
        画面B.UpsertProjectLine(n, BudgetCategory.Revenue, project.Id, new Money(900_000m));
        画面B.UpsertProjectLine(n + 1, BudgetCategory.Processing, project.Id, new Money(1m));

        await repo.UpdateAsync(画面A, n);
        var ex = await Assert.ThrowsAsync<BudgetConflictException>(() => repo.UpdateAsync(画面B, n));

        Assert.Equal("ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。",
            ex.Message);
        var saved = (await repo.FindByIdAsync(budget.Id))!;
        Assert.Equal(n + 1, saved.UpdateNumber);
        Assert.Equal(1_200_000m, 売上高(saved));
        Assert.Single(saved.Lines);
    }

    [Fact]
    public async Task 行30_承認する予算の条件つきの更新が0件なら承認も旧承認版の失効も書かない()
    {
        var (v1, project, repo) = await ドラフトを保存();
        v1.Approve(v1.UpdateNumber, _fx.Now);
        await repo.UpdateAsync(v1, v1.UpdateNumber - 1);
        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", _fx.Now);
        await repo.AddAsync(v2);

        var 画面A = (await repo.FindByIdAsync(v2.Id))!;
        var 画面B = (await repo.FindByIdAsync(v2.Id))!;
        var k = 画面A.UpdateNumber;
        画面B.UpsertProjectLine(k, BudgetCategory.Revenue, project.Id, new Money(900_000m));
        await repo.UpdateAsync(画面B, k);

        // 画面 A は読んだときの番号 k のまま、集約では承認を通る(読んでから書くまでの間に B が書いた)
        画面A.Approve(k, _fx.Now);
        var old = (await repo.FindByIdAsync(v1.Id))!;
        old.Supersede();
        await Assert.ThrowsAsync<BudgetConflictException>(() => repo.ApproveAsync(画面A, k, [old]));

        var savedV2 = (await repo.FindByIdAsync(v2.Id))!;
        Assert.Equal(BudgetStatus.Draft, savedV2.Status);
        Assert.Null(savedV2.ApprovedAt);
        Assert.Equal(900_000m, 売上高(savedV2));
        Assert.Equal(k + 1, savedV2.UpdateNumber);
        var savedV1 = (await repo.FindByIdAsync(v1.Id))!;
        Assert.Equal(BudgetStatus.Approved, savedV1.Status);
    }

    [Fact]
    public async Task 承認の書き込みは承認と旧承認版の失効をまとめて書き旧承認版の更新番号は変えない()
    {
        var (v1, _, repo) = await ドラフトを保存();
        v1.Approve(v1.UpdateNumber, _fx.Now);
        await repo.UpdateAsync(v1, v1.UpdateNumber - 1);
        var m = v1.UpdateNumber;
        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", _fx.Now);
        await repo.AddAsync(v2);

        var k = v2.UpdateNumber;
        v2.Approve(k, _fx.Now);
        v1.Supersede();
        await repo.ApproveAsync(v2, k, [v1]);

        var savedV2 = (await repo.FindByIdAsync(v2.Id))!;
        Assert.Equal(BudgetStatus.Approved, savedV2.Status);
        Assert.Equal(k + 1, savedV2.UpdateNumber);
        var savedV1 = (await repo.FindByIdAsync(v1.Id))!;
        Assert.Equal(BudgetStatus.Superseded, savedV1.Status);
        Assert.Equal(m, savedV1.UpdateNumber);
    }
}

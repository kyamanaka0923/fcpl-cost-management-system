using CostManagement.Application.Common;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 部の予算承認・取り消し。</summary>
public class 部の予算承認 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task<(Guid DivId, Guid Dept1, Guid Dept2)> 課2つの部を準備()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        return (div.Id, 課1.Id, 課2.Id);
    }

    private async Task 課の承認済み予算を作成(Guid deptId, string code)
    {
        var pj = await _fx.案件を作成(deptId, code, $"案件{code}");
        await _fx.承認済み予算を作成(deptId, "2026-H1", ("Revenue", pj.Id, null, 1_000_000m));
    }

    [Fact]
    public async Task 配下の全課が承認済みなら部を承認できる()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1");
        await 課の承認済み予算を作成(dept2, "PJ-2");

        await _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1");

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.True(summary.IsApproved);
        Assert.NotNull(summary.ApprovedAt);
    }

    [Fact]
    public async Task 未承認の課があると部を承認できない()
    {
        var (divId, dept1, _) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1"); // 課2 は未策定

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1"));
        Assert.Contains("未承認の予算がある課", ex.Message);

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.False(summary.IsApproved);
        Assert.False(summary.CanApprove); // 全課承認前は承認不可
    }

    [Fact]
    public async Task ドラフトのままの課があると部を承認できない()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1");
        // 課2 は明細まで入れたが承認していない。
        var pj = await _fx.案件を作成(dept2, "PJ-2", "案件2");
        var draft = await _fx.Budgets.CreateDraftAsync(dept2,
            new CreateBudgetRequest("2026-H1", "当初予算"));
        await _fx.Budgets.UpsertLineAsync(draft.Id,
            new UpsertBudgetLineRequest("Revenue", pj.Id, null, 1_000_000m));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1"));
        Assert.Contains("未承認の予算がある課", ex.Message);
    }

    [Fact]
    public async Task 別の半期だけ承認済みの課があると部を承認できない()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1");
        var pj = await _fx.案件を作成(dept2, "PJ-2", "案件2");
        await _fx.承認済み予算を作成(dept2, "2026-H2", ("Revenue", pj.Id, null, 1_000_000m));

        await Assert.ThrowsAsync<DomainException>(() =>
            _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1"));
    }

    [Fact]
    public async Task 課がない部は承認できない()
    {
        var div = await _fx.部を作成();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.DivisionApprovals.ApproveAsync(div.Id, "2026-H1"));
        Assert.Contains("承認する課がありません", ex.Message);
    }

    [Fact]
    public async Task 全課承認済みになると部承認が可能になる()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1");

        var before = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.False(before.CanApprove);

        await 課の承認済み予算を作成(dept2, "PJ-2");

        var after = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.True(after.CanApprove);
    }

    [Fact]
    public async Task 部承認は取り消せる()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        await 課の承認済み予算を作成(dept1, "PJ-1");
        await 課の承認済み予算を作成(dept2, "PJ-2");
        await _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1");

        await _fx.DivisionApprovals.RevokeAsync(divId, "2026-H1");

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.False(summary.IsApproved);
    }

    [Fact]
    public async Task 部承認後に課が改定しても部承認は残る()
    {
        var (divId, dept1, dept2) = await 課2つの部を準備();
        var pj1 = await _fx.案件を作成(dept1, "PJ-1", "案件1");
        await _fx.承認済み予算を作成(dept1, "2026-H1", ("Revenue", pj1.Id, null, 1_000_000m));
        await 課の承認済み予算を作成(dept2, "PJ-2");
        await _fx.DivisionApprovals.ApproveAsync(divId, "2026-H1");

        // 課1 を改定して再承認(課の承認フローは部承認と独立)
        var v2 = await _fx.Budgets.CreateDraftAsync(dept1,
            new CreateBudgetRequest("2026-H1", "上期見直し"));
        await _fx.Budgets.UpsertLineAsync(v2.Id,
            new UpsertBudgetLineRequest("Revenue", pj1.Id, null, 1_500_000m));
        await _fx.Budgets.ApproveAsync(v2.Id);

        // 部承認はそのまま残る
        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(divId, "2026-H1");
        Assert.True(summary.IsApproved);
        // 部サマリは最新承認版(v2)を反映
        Assert.Equal(2_500_000m, summary.PlannedRevenue); // 150万 + 100万
    }
}

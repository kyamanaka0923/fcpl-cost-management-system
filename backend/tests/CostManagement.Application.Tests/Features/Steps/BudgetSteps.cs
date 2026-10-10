using Reqnroll;
using static CostManagement.Application.Tests.Features.業務コンテキスト;

namespace CostManagement.Application.Tests.Features.Steps;

/// <summary>課予算の起票・明細登録・承認・改定のステップ。</summary>
[Binding]
public sealed class 予算のステップ(業務コンテキスト ctx)
{
    [StepDefinition("課 {string} の {string} の予算ドラフトを起票する")]
    public async Task 予算ドラフトを起票する(string department, string half)
    {
        ctx.現在の課 = department;
        ctx.現在の半期 = half;
        ctx.現在の予算 = await ctx.Fx.Budgets.CreateDraftAsync(ctx.課Id(department),
            new CreateBudgetRequest(half, "当初予算"));
    }

    [StepDefinition("次の明細を登録する:")]
    public async Task 次の明細を登録する(DataTable table)
    {
        foreach (var row in table.Rows)
            ctx.現在の予算 = await ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id, 明細リクエスト(row));
    }

    [StepDefinition("予算を承認する")]
    public async Task 予算を承認する() =>
        ctx.現在の予算 = await ctx.Fx.Budgets.ApproveAsync(ctx.予算().Id);

    [Given("課 {string} の {string} に次の明細の承認済み予算がある:")]
    public async Task 承認済み予算がある(string department, string half, DataTable table)
    {
        await 予算ドラフトを起票する(department, half);
        await 次の明細を登録する(table);
        await 予算を承認する();
    }

    [When("改定版 {string} を起票する")]
    public async Task 改定版を起票する(string label) =>
        ctx.現在の予算 = await ctx.Fx.Budgets.CreateDraftAsync(ctx.課Id(ctx.現在の課!),
            new CreateBudgetRequest(ctx.現在の半期!, label));

    [When("区分 {string} の案件 {string} の金額を {long} に変更しようとする")]
    public Task 案件明細の金額を変更しようとする(string category, string project, long amount) =>
        ctx.試行(() => ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id,
            new UpsertBudgetLineRequest(区分(category), ctx.案件Id(ctx.現在の課!, project), null, amount)));

    [StepDefinition("期間費用 {string} を明細名 {string} で {long} 登録する")]
    public async Task 期間費用を登録する(string element, string detail, long amount) =>
        ctx.現在の予算 = await ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id,
            new UpsertBudgetLineRequest("PeriodCost", null, element, amount, null, 空ならnull(detail)));

    [When("期間費用 {string} を明細名 {string} で {long} 登録しようとする")]
    public Task 期間費用を登録しようとする(string element, string detail, long amount) =>
        ctx.試行(() => 期間費用を登録する(element, detail, amount));

    [Then("予算の状態は {string} である")]
    public void 予算の状態は(string status) => Assert.Equal(status, 状態(ctx.予算().Status));

    [Then("予算の区分合計は次のとおりである:")]
    public void 区分合計は(DataTable table)
    {
        var budget = ctx.予算();
        foreach (var row in table.Rows)
        {
            var actual = 区分(row["区分"]) switch
            {
                "Revenue" => budget.RevenueTotal,
                "Processing" => budget.ProcessingTotal,
                "Outsourcing" => budget.OutsourcingTotal,
                _ => budget.PeriodCostTotal,
            };
            Assert.True(decimal.Parse(row["金額"]) == actual, $"{row["区分"]} の合計: 期待 {row["金額"]} / 実際 {actual}");
        }
    }

    [Then("予算の計画損益は {long} である")]
    public void 計画損益は(long profit) => Assert.Equal(profit, ctx.予算().PlannedProfit);

    [Then("課 {string} の {string} の予算の版は次のとおりである:")]
    public async Task 予算の版は(string department, string half, DataTable table)
    {
        var versions = await ctx.Fx.Budgets.ListAsync(ctx.課Id(department), half);
        Assert.Equal(
            table.Rows.Select(r => (int.Parse(r["版"]), r["名称"], r["状態"])),
            versions.Select(v => (v.Version, v.Label, 状態(v.Status))));
    }

    [Then("版 {int} から版 {int} への {string} の増減は {long} である")]
    public async Task 版の増減は(int baseVersion, int targetVersion, string category, long difference)
    {
        var report = await ctx.Fx.Analysis.CompareBudgetsAsync(ctx.課Id(ctx.現在の課!), ctx.現在の半期!,
            baseVersion, targetVersion);
        Assert.Equal(difference, report.Categories.Single(c => c.Category == 区分(category)).Difference);
    }

    /// <summary>表の1行(区分・案件・費目・明細名・金額)を明細登録リクエストにする。</summary>
    private UpsertBudgetLineRequest 明細リクエスト(DataTableRow row)
    {
        var category = 区分(row["区分"]);
        var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(ctx.現在の課!, code) : (Guid?)null;
        return new UpsertBudgetLineRequest(category, project, 空ならnull(row["費目"]),
            decimal.Parse(row["金額"]), null, 空ならnull(row["明細名"]));
    }
}

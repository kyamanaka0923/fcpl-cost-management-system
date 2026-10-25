using Reqnroll;
using static CostManagement.Application.Tests.Support.業務コンテキスト;

namespace CostManagement.Application.Tests.Steps;

/// <summary>課予算の起票・明細登録・承認・改定のステップ。</summary>
[Binding]
public sealed class 予算のステップ(業務コンテキスト ctx)
{
    // ---- 起票・承認 ----

    [StepDefinition("課 {string} の {string} の予算ドラフトを起票する")]
    public async Task 予算ドラフトを起票する(string department, string half)
    {
        ctx.現在の課 = department;
        ctx.現在の半期 = half;
        ctx.現在の予算 = await ctx.Fx.Budgets.CreateDraftAsync(ctx.課Id(department),
            new CreateBudgetRequest(half, "当初予算"));
    }

    [When("課 {string} の {string} の予算ドラフトを起票しようとする")]
    public Task 予算ドラフトを起票しようとする(string department, string half) =>
        ctx.試行(() => ctx.Fx.Budgets.CreateDraftAsync(ctx.課Id(department),
            new CreateBudgetRequest(half, "重複ドラフト")));

    [StepDefinition("予算を承認する")]
    public async Task 予算を承認する() =>
        ctx.現在の予算 = await ctx.Fx.Budgets.ApproveAsync(ctx.予算().Id);

    [When("予算を承認しようとする")]
    public Task 予算を承認しようとする() => ctx.試行(予算を承認する);

    [StepDefinition("課 {string} の {string} に次の明細の承認済み予算がある:")]
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

    [StepDefinition("課 {string} の {string} の改定版 {string} を次の明細で承認する:")]
    public async Task 改定版を承認する(string department, string half, string label, DataTable table)
    {
        ctx.現在の課 = department;
        ctx.現在の半期 = half;
        await 改定版を起票する(label);
        await 次の明細を登録する(table);
        await 予算を承認する();
    }

    [StepDefinition("保存した予算を取得し直す")]
    public async Task 予算を取得し直す() =>
        ctx.現在の予算 = await ctx.Fx.Budgets.GetAsync(ctx.予算().Id);

    // ---- 明細 ----

    [StepDefinition("次の明細を登録する:")]
    public async Task 次の明細を登録する(DataTable table)
    {
        foreach (var row in table.Rows)
            ctx.現在の予算 = await ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id, 明細リクエスト(row));
    }

    [When("次の明細を登録しようとする:")]
    public Task 次の明細を登録しようとする(DataTable table) => ctx.試行(() => 次の明細を登録する(table));

    [StepDefinition("区分 {string} の案件 {string} を次の月別金額で登録する:")]
    public async Task 月別金額で登録する(string category, string project, DataTable table)
    {
        var monthly = table.Rows.ToDictionary(r => 整数(r["月"]), r => 金額(r["金額"]));
        ctx.現在の予算 = await ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id, new UpsertBudgetLineRequest(
            区分(category), ctx.案件Id(ctx.現在の課!, project), null, 0m, monthly));
    }

    [When("区分 {string} の案件 {string} の金額を {long} に変更しようとする")]
    public Task 案件明細の金額を変更しようとする(string category, string project, long amount) =>
        ctx.試行(() => ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id,
            new UpsertBudgetLineRequest(区分(category), ctx.案件Id(ctx.現在の課!, project), null, amount)));

    [When("区分 {string} に課 {string} の案件 {string} を {long} で登録しようとする")]
    public Task 他課の案件で登録しようとする(string category, string otherDepartment, string project, long amount) =>
        ctx.試行(() => ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id,
            new UpsertBudgetLineRequest(区分(category), ctx.案件Id(otherDepartment, project), null, amount)));

    [StepDefinition("期間費用 {string} を明細名 {string} で {long} 登録する")]
    public async Task 期間費用を登録する(string element, string detail, long amount) =>
        ctx.現在の予算 = await ctx.Fx.Budgets.UpsertLineAsync(ctx.予算().Id,
            new UpsertBudgetLineRequest("PeriodCost", null, element, amount, null, 空ならnull(detail)));

    [When("期間費用 {string} を明細名 {string} で {long} 登録しようとする")]
    public Task 期間費用を登録しようとする(string element, string detail, long amount) =>
        ctx.試行(() => 期間費用を登録する(element, detail, amount));

    [When("期間費用 {string} の明細名 {string} を {string} に変更する")]
    public async Task 明細名を変更する(string element, string oldDetail, string newDetail) =>
        ctx.現在の予算 = await ctx.Fx.Budgets.RenamePeriodDetailAsync(ctx.予算().Id,
            new RenamePeriodDetailRequest(element, oldDetail, newDetail));

    // ---- 検証 ----

    [Then("予算の状態は {string} である")]
    public void 予算の状態は(string status) => Assert.Equal(status, 状態(ctx.予算().Status));

    [Then("予算の版は {int} である")]
    public void 予算の版は(int version) => Assert.Equal(version, ctx.予算().Version);

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
            Assert.True(金額(row["金額"]) == actual, $"{row["区分"]} の合計: 期待 {row["金額"]} / 実際 {actual}");
        }
    }

    [Then("予算の計画損益は {long} である")]
    public void 計画損益は(long profit) => Assert.Equal(profit, ctx.予算().PlannedProfit);

    [Then("予算の明細は次のとおりである:")]
    public async Task 予算の明細は(DataTable table)
    {
        // 案件はコードで書くため、明細の案件Idを現在のコードへ引き直して比べる(コード変更後も追える)
        var codes = new Dictionary<Guid, string>();
        foreach (var line in ctx.予算().Lines.Where(l => l.ProjectId is not null))
            codes[line.ProjectId!.Value] = (await ctx.Fx.Projects.GetAsync(line.ProjectId.Value)).Code;

        var expected = table.Rows
            .Select(r => (区分(r["区分"]), 空ならnull(r["案件"]), 空ならnull(r["費目"]), 空ならnull(r["明細名"]),
                金額(r["金額"])))
            .Order();
        var actual = ctx.予算().Lines
            .Select(l => (l.Category, l.ProjectId is { } id ? codes[id] : null, l.ElementCode, l.PeriodDetail,
                l.Amount))
            .Order();
        Assert.Equal(expected, actual);
    }

    [Then("予算に明細はない")]
    public void 予算に明細はない() => Assert.Empty(ctx.予算().Lines);

    [Then("区分 {string} の案件 {string} の明細は月次入力で、月別金額は次のとおりである:")]
    public void 月次入力の明細は(string category, string project, DataTable table)
    {
        var line = 案件の明細(category, project);
        Assert.True(line.IsMonthly, "月次入力になっていません。");
        Assert.Equal(
            table.Rows.Select(r => (整数(r["月"]), 金額(r["金額"]))).Order(),
            line.MonthlyAmounts.Select(kv => (kv.Key, kv.Value)).Order());
    }

    [Then("区分 {string} の案件 {string} の明細は半期一括で、金額は {long} である")]
    public void 半期一括の明細は(string category, string project, long amount)
    {
        var line = 案件の明細(category, project);
        Assert.False(line.IsMonthly, "月次入力のままです。");
        Assert.Empty(line.MonthlyAmounts);
        Assert.Equal(amount, line.Amount);
    }

    [Then("課 {string} の {string} の予算の版は次のとおりである:")]
    public async Task 予算の版の一覧は(string department, string half, DataTable table)
    {
        var versions = await ctx.Fx.Budgets.ListAsync(ctx.課Id(department), half);
        Assert.Equal(
            table.Rows.Select(r => (整数(r["版"]), r["名称"], r["状態"])),
            versions.Select(v => (v.Version, v.Label, 状態(v.Status))));
    }

    [Then("版 {int} から版 {int} への {string} の増減は {long} である")]
    public async Task 版の増減は(int baseVersion, int targetVersion, string category, long difference)
    {
        var report = await ctx.Fx.Analysis.CompareBudgetsAsync(ctx.課Id(ctx.現在の課!), ctx.現在の半期!,
            baseVersion, targetVersion);
        Assert.Equal(difference, report.Categories.Single(c => c.Category == 区分(category)).Difference);
    }

    [When("版 {int} から版 {int} への増減を比較しようとする")]
    public Task 版を比較しようとする(int baseVersion, int targetVersion) =>
        ctx.試行(() => ctx.Fx.Analysis.CompareBudgetsAsync(ctx.課Id(ctx.現在の課!), ctx.現在の半期!,
            baseVersion, targetVersion));

    // ---- 補助 ----

    /// <summary>表の1行(区分・案件・費目・明細名・金額)を明細登録リクエストにする。</summary>
    private UpsertBudgetLineRequest 明細リクエスト(DataTableRow row)
    {
        var category = 区分(row["区分"]);
        var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(ctx.現在の課!, code) : (Guid?)null;
        return new UpsertBudgetLineRequest(category, project, 空ならnull(row["費目"]),
            金額(row["金額"]), null, 空ならnull(row["明細名"]));
    }

    private BudgetLineDto 案件の明細(string category, string project)
    {
        var key = (区分(category), (Guid?)ctx.案件Id(ctx.現在の課!, project));
        return ctx.予算().Lines.SingleOrDefault(l => (l.Category, l.ProjectId) == key)
            ?? throw new Xunit.Sdk.XunitException($"明細が見つかりません: {category} {project}");
    }
}

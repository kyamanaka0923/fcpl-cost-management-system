using Reqnroll;
using static CostManagement.Application.Tests.Support.業務コンテキスト;

namespace CostManagement.Application.Tests.Steps;

/// <summary>
/// 予算を読み込んだ画面からの保存のステップ(楽観ロック。BR-BUD-008・FR-4.15〜4.17・FR-5.7・FR-5.8)。
/// 画面は、読み込んだ予算と、そのときか自分の保存の応答で受け取った更新番号を持ち、保存にその番号を付けて送る。
/// </summary>
[Binding]
public sealed class 画面からの保存のステップ(業務コンテキスト ctx)
{
    // ---- 読み込み ----

    [StepDefinition("画面 {string} と画面 {string} がこの予算を読み込んでいる")]
    public async Task 二つの画面が読み込んでいる(string a, string b)
    {
        await 予算を読み込む(a, ctx.予算().Id);
        await 予算を読み込む(b, ctx.予算().Id);
    }

    [StepDefinition("画面 {string} が予算を読み込み直す")]
    public Task 予算を読み込み直す(string screen) => 予算を読み込む(screen, 画面の(screen).予算Id);

    // ---- 保存 ----

    [StepDefinition("画面 {string} から次の明細を登録する:")]
    public async Task 次の明細を登録する(string screen, DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var (budgetId, number) = 画面の(screen);
            受け付けた(screen, await ctx.Fx.Budgets.UpsertLineAsync(budgetId, 明細リクエスト(row), number));
        }
    }

    [When("画面 {string} から次の明細を登録しようとする:")]
    public Task 次の明細を登録しようとする(string screen, DataTable table) =>
        ctx.試行(() => 次の明細を登録する(screen, table));

    [StepDefinition("画面 {string} から区分 {string} の案件 {string} の明細を削除する")]
    public async Task 案件の明細を削除する(string screen, string category, string project)
    {
        var (budgetId, number) = 画面の(screen);
        受け付けた(screen, await ctx.Fx.Budgets.RemoveLineAsync(budgetId, 区分(category),
            ctx.案件Id(ctx.現在の課!, project), null, null, number));
    }

    [When("画面 {string} から区分 {string} の案件 {string} の明細を削除しようとする")]
    public Task 案件の明細を削除しようとする(string screen, string category, string project) =>
        ctx.試行(() => 案件の明細を削除する(screen, category, project));

    [StepDefinition("画面 {string} から期間費用 {string} の明細名 {string} を {string} に変更する")]
    public async Task 明細名を変更する(string screen, string element, string oldDetail, string newDetail)
    {
        var (budgetId, number) = 画面の(screen);
        受け付けた(screen, await ctx.Fx.Budgets.RenamePeriodDetailAsync(budgetId,
            new RenamePeriodDetailRequest(element, oldDetail, newDetail), number));
    }

    [When("画面 {string} から期間費用 {string} の明細名 {string} を {string} に変更しようとする")]
    public Task 明細名を変更しようとする(string screen, string element, string oldDetail, string newDetail) =>
        ctx.試行(() => 明細名を変更する(screen, element, oldDetail, newDetail));

    [StepDefinition("画面 {string} から予算を承認する")]
    public async Task 予算を承認する(string screen)
    {
        var (budgetId, number) = 画面の(screen);
        受け付けた(screen, await ctx.Fx.Budgets.ApproveAsync(budgetId, number));
    }

    [When("画面 {string} から予算を承認しようとする")]
    public Task 予算を承認しようとする(string screen) => ctx.試行(() => 予算を承認する(screen));

    // ---- 検証 ----

    [Then("予算の更新番号は画面 {string} が持つ更新番号と同じである")]
    public async Task 更新番号は画面と同じ(string screen) =>
        Assert.Equal(画面の(screen).更新番号, await 保存されている更新番号());

    [Then("予算の更新番号は画面 {string} が持つ更新番号と違う")]
    public async Task 更新番号は画面と違う(string screen) =>
        Assert.NotEqual(画面の(screen).更新番号, await 保存されている更新番号());

    // ---- 補助 ----

    /// <summary>画面に予算を読み込み、その予算と更新番号を持たせる。</summary>
    private async Task 予算を読み込む(string screen, Guid budgetId)
    {
        var budget = await ctx.Fx.Budgets.GetAsync(budgetId);
        ctx.画面[screen] = (budget.Id, budget.UpdateNumber);
    }

    /// <summary>保存を受け付けた応答の更新番号を画面に持たせ、応答の予算を直近の予算にする。</summary>
    private void 受け付けた(string screen, BudgetDetailDto response)
    {
        ctx.画面[screen] = (response.Id, response.UpdateNumber);
        ctx.現在の予算 = response;
    }

    private (Guid 予算Id, int 更新番号) 画面の(string screen) =>
        ctx.画面.TryGetValue(screen, out var s)
            ? s
            : throw new InvalidOperationException($"画面 \"{screen}\" は予算を読み込んでいません。");

    /// <summary>直近の予算を読み直し、DB に保存されている更新番号を返す。</summary>
    private async Task<int> 保存されている更新番号() =>
        (await ctx.Fx.Budgets.GetAsync(ctx.予算().Id)).UpdateNumber;

    /// <summary>表の1行(区分・案件・費目・明細名・金額)を明細登録リクエストにする。</summary>
    private UpsertBudgetLineRequest 明細リクエスト(DataTableRow row)
    {
        var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(ctx.現在の課!, code) : (Guid?)null;
        return new UpsertBudgetLineRequest(区分(row["区分"]), project, 空ならnull(row["費目"]),
            金額(row["金額"]), null, 空ならnull(row["明細名"]));
    }
}

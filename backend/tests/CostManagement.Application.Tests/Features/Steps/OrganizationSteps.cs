using Reqnroll;

namespace CostManagement.Application.Tests.Features.Steps;

/// <summary>組織(部・課)・案件・費目のマスタを用意・編集するステップ。コードと名称は同じ文字列で登録する。</summary>
[Binding]
public sealed class 組織と案件のステップ(業務コンテキスト ctx)
{
    // ---- 部・課 ----

    [Given("部 {string} がある")]
    public async Task 部がある(string division) =>
        ctx.部[division] = (await ctx.Fx.部を作成(division, division)).Id;

    [When("部 {string} を登録しようとする")]
    public Task 部を登録しようとする(string division) => ctx.試行(() => 部がある(division));

    [Given("部 {string} に課 {string} がある")]
    public async Task 部に課がある(string division, string department)
    {
        if (!ctx.部.ContainsKey(division))
            await 部がある(division);
        ctx.課[department] = (await ctx.Fx.課を作成(ctx.部[division], department, department)).Id;
    }

    [When("部 {string} に課 {string} を登録しようとする")]
    public Task 課を登録しようとする(string division, string department) =>
        ctx.試行(() => 部に課がある(division, department));

    // ---- 案件 ----

    [Given("課 {string} に案件 {string} がある")]
    [When("課 {string} に案件 {string} を登録する")]
    public async Task 課に案件がある(string department, string project) =>
        ctx.案件[(department, project)] =
            (await ctx.Fx.案件を作成(ctx.課Id(department), project, project)).Id;

    [When("課 {string} に案件 {string} を登録しようとする")]
    public Task 案件を登録しようとする(string department, string project) =>
        ctx.試行(() => 課に案件がある(department, project));

    [When("課 {string} の案件 {string} をコード {string}、名称 {string} に変更する")]
    public async Task 案件を変更する(string department, string project, string newCode, string newName)
    {
        var id = ctx.案件Id(department, project);
        await ctx.Fx.Projects.UpdateAsync(id, new UpdateProjectRequest(newCode, newName));
        // 以降のステップは新しいコードで案件を指す
        ctx.案件.Remove((department, project));
        ctx.案件[(department, newCode)] = id;
    }

    [When("課 {string} の案件 {string} をコード {string}、名称 {string} に変更しようとする")]
    public Task 案件を変更しようとする(string department, string project, string newCode, string newName) =>
        ctx.試行(() => 案件を変更する(department, project, newCode, newName));

    [Then("課 {string} の案件 {string} が登録されている")]
    public async Task 案件が登録されている(string department, string project)
    {
        var projects = await ctx.Fx.Projects.ListByDepartmentAsync(ctx.課Id(department));
        Assert.Contains(projects, p => p.Code == project);
    }

    [Then("課 {string} の案件 {string} の名称は {string} である")]
    public async Task 案件の名称は(string department, string project, string name)
    {
        // DB から取得し直して、コードと名称の両方が反映されていることを確かめる
        var reloaded = await ctx.Fx.Projects.GetAsync(ctx.案件Id(department, project));
        Assert.Equal((project, name), (reloaded.Code, reloaded.Name));
    }

    // ---- 費目 ----

    [StepDefinition("費目 {string} を名称 {string} で追加する")]
    public Task 費目を追加する(string code, string name) =>
        ctx.Fx.CostElements.CreateAsync(new CreateCostElementRequest(code, name));

    [When("費目 {string} を名称 {string} で追加しようとする")]
    public Task 費目を追加しようとする(string code, string name) => ctx.試行(() => 費目を追加する(code, name));

    [Then("費目マスタには次の費目がある:")]
    public async Task 費目マスタには(DataTable table)
    {
        var codes = (await ctx.Fx.CostElements.ListAsync()).Select(e => e.Code).ToList();
        foreach (var row in table.Rows)
            Assert.Contains(row["費目"], codes);
    }

    // ---- エラー ----

    [Then("{string} を含むエラーになる")]
    public void エラーになる(string message)
    {
        var error = ctx.直前のエラー ?? throw new Xunit.Sdk.XunitException("エラーになりませんでした。");
        Assert.Contains(message, error.Message);
    }
}

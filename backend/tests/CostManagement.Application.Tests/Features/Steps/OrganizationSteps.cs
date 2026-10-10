using Reqnroll;

namespace CostManagement.Application.Tests.Features.Steps;

/// <summary>組織(部・課)と案件を用意するステップ。コードと名称は同じ文字列で登録する。</summary>
[Binding]
public sealed class 組織と案件のステップ(業務コンテキスト ctx)
{
    [Given("部 {string} に課 {string} がある")]
    public async Task 部に課がある(string division, string department)
    {
        if (!ctx.部.TryGetValue(division, out var divisionId))
        {
            divisionId = (await ctx.Fx.部を作成(division, division)).Id;
            ctx.部[division] = divisionId;
        }
        ctx.課[department] = (await ctx.Fx.課を作成(divisionId, department, department)).Id;
    }

    [Given("課 {string} に案件 {string} がある")]
    public async Task 課に案件がある(string department, string project) =>
        ctx.案件[(department, project)] =
            (await ctx.Fx.案件を作成(ctx.課Id(department), project, project)).Id;

    [Then("{string} を含むエラーになる")]
    public void エラーになる(string message)
    {
        var error = ctx.直前のエラー ?? throw new Xunit.Sdk.XunitException("エラーになりませんでした。");
        Assert.Contains(message, error.Message);
    }
}

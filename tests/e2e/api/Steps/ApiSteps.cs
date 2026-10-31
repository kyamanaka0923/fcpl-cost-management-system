using System.Net;
using System.Text.Json;
using Reqnroll;
using static CostManagement.E2E.Tests.Support.APIコンテキスト;

namespace CostManagement.E2E.Tests.Steps;

/// <summary>
/// HTTP 経由で業務操作を行うステップ。「API で … する」は成功(2xx)を前提にし、
/// 期待しない応答ならその場で失敗する。エラーを確かめるシナリオでは「応答は … で」で検証する。
/// </summary>
[Binding]
public sealed class APIのステップ(APIコンテキスト ctx)
{
    private static readonly HttpMethod Get = HttpMethod.Get;
    private static readonly HttpMethod Post = HttpMethod.Post;
    private static readonly HttpMethod Put = HttpMethod.Put;
    private static readonly HttpMethod Delete = HttpMethod.Delete;

    // ---- マスタ ----

    [Given("API で部 {string} を登録している")]
    public async Task 部を登録している(string division)
    {
        var res = await ctx.成功する(Post, "/api/divisions", new { code = division, name = division },
            HttpStatusCode.Created);
        ctx.部[division] = Id(res);
    }

    [Given("API で部 {string} に課 {string} を登録している")]
    public async Task 課を登録している(string division, string department)
    {
        var res = await ctx.成功する(Post, $"/api/divisions/{ctx.部[division]}/departments",
            new { code = department, name = department }, HttpStatusCode.Created);
        ctx.課[department] = Id(res);
    }

    [Given("API で課 {string} に案件 {string} を登録している")]
    public async Task 案件を登録している(string department, string project)
    {
        var res = await ctx.成功する(Post, $"/api/departments/{ctx.課Id(department)}/projects",
            new { code = project, name = project }, HttpStatusCode.Created);
        ctx.案件[(department, project)] = Id(res);
    }

    [When("API で費目の一覧を取得する")]
    public Task 費目の一覧を取得する() => ctx.成功する(Get, "/api/cost-elements");

    [Then("費目の一覧に {string} と {string} がある")]
    public void 費目の一覧にある(string code1, string code2)
    {
        var codes = ctx.応答本文.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Contains(code1, codes);
        Assert.Contains(code2, codes);
    }

    [When("API で費目 {string} を名称 {string} で追加する")]
    public Task 費目を追加する(string code, string name) =>
        ctx.成功する(Post, "/api/cost-elements", new { code, name }, HttpStatusCode.Created);

    [When("API で存在しない課を取得する")]
    public Task 存在しない課を取得する() => ctx.送信(Get, $"/api/departments/{Guid.NewGuid()}");

    // ---- 予算 ----

    /// <summary>起票は成否を問わず送る(不正な半期のシナリオでも使う)。成功時だけ予算Idを記録する。</summary>
    [StepDefinition("API で課 {string} の {string} の予算 {string} を起票する")]
    public async Task 予算を起票する(string department, string half, string label)
    {
        ctx.現在の課 = department;
        ctx.現在の半期 = half;
        var res = await ctx.送信(Post, $"/api/departments/{ctx.課Id(department)}/budgets",
            new { fiscalHalf = half, label });
        if (res.StatusCode == HttpStatusCode.Created)
            ctx.予算Id = Id(ctx.応答本文);
    }

    /// <summary>成否を問わず送る(拒否のシナリオ用)。表の 1 行目だけを送る。</summary>
    [When("API で次の明細を登録しようとする:")]
    public async Task 明細を登録しようとする(DataTable table)
    {
        var row = table.Rows.Single();
        var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(ctx.現在の課!, code) : null;
        await ctx.送信(Put, $"/api/budgets/{予算Id()}/lines", new
        {
            category = 区分(row["区分"]),
            projectId = project,
            elementCode = 空ならnull(row["費目"]),
            amount = 金額(row["金額"]),
        });
    }

    /// <summary>月別金額で成否を問わず送る(拒否のシナリオ用)。</summary>
    [When("API で区分 {string} の案件 {string} を次の月別金額で登録しようとする:")]
    public async Task 月別金額で登録しようとする(string category, string project, DataTable table)
    {
        await ctx.送信(Put, $"/api/budgets/{予算Id()}/lines", new
        {
            category = 区分(category),
            projectId = ctx.案件Id(ctx.現在の課!, project),
            amount = 0m,
            monthlyAmounts = table.Rows.ToDictionary(r => int.Parse(r["月"], System.Globalization.CultureInfo.InvariantCulture), r => 金額(r["金額"])),
        });
    }

    [StepDefinition("API で次の明細を登録する:")]
    public async Task 明細を登録する(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(ctx.現在の課!, code) : null;
            await ctx.成功する(Put, $"/api/budgets/{予算Id()}/lines", new
            {
                category = 区分(row["区分"]),
                projectId = project,
                elementCode = 空ならnull(row["費目"]),
                amount = 金額(row["金額"]),
            });
        }
    }

    /// <summary>承認は成否を問わず送る(明細のない予算のシナリオでも使う)。</summary>
    [StepDefinition("API で予算を承認する")]
    public Task 予算を承認する() => ctx.送信(Post, $"/api/budgets/{予算Id()}/approve", new { });

    [Given("API で課 {string} の {string} に次の明細の承認済み予算がある:")]
    public async Task 承認済み予算がある(string department, string half, DataTable table)
    {
        await 予算を起票する(department, half, "当初予算");
        await 明細を登録する(table);
        await 予算を承認する();
        Assert.Equal(HttpStatusCode.OK, ctx.応答!.StatusCode);
    }

    [Then("起票した予算の版は {int} である")]
    public void 起票した予算の版は(int version)
    {
        Assert.Equal(HttpStatusCode.Created, ctx.応答!.StatusCode);
        Assert.Equal(version, ctx.応答本文.GetProperty("version").GetInt32());
    }

    [Then("起票した予算は明細を {int} 件引き継いでいる")]
    public void 明細を引き継いでいる(int count) =>
        Assert.Equal(count, ctx.応答本文.GetProperty("lines").GetArrayLength());

    [Then("応答の予算は次のとおりである:")]
    public void 応答の予算は(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var property = row["項目"] switch
            {
                "売上高" => "revenueTotal",
                "加工費" => "processingTotal",
                "外注費" => "outsourcingTotal",
                "期間費用" => "periodCostTotal",
                "計画損益" => "plannedProfit",
                var other => throw new ArgumentException($"未知の項目です: {other}"),
            };
            Assert.True(金額(row["金額"]) == ctx.応答本文.GetProperty(property).GetDecimal(),
                $"{row["項目"]}: 期待 {row["金額"]} / 実際 {ctx.応答本文.GetProperty(property)}");
        }
    }

    [Then("応答の予算の状態は {string} である")]
    public void 応答の予算の状態は(string status)
    {
        Assert.Equal(HttpStatusCode.OK, ctx.応答!.StatusCode);
        Assert.Equal(status, ctx.応答本文.GetProperty("status").GetString());
    }

    [Then("API で取得した課 {string} の {string} の予算の版は次のとおりである:")]
    public async Task 予算の版は(string department, string half, DataTable table)
    {
        var budgets = await ctx.成功する(Get, $"/api/departments/{ctx.課Id(department)}/budgets?fiscalHalf={half}");
        Assert.Equal(
            table.Rows.Select(r => (int.Parse(r["版"], System.Globalization.CultureInfo.InvariantCulture), r["状態"])),
            budgets.EnumerateArray()
                .Select(b => (b.GetProperty("version").GetInt32(), b.GetProperty("status").GetString()!))
                .OrderBy(b => b.Item1));
    }

    [Then("API で版 {int} と版 {int} を比べると {string} の増減は {long} である")]
    public async Task 版を比べると(int baseVersion, int targetVersion, string category, long difference)
    {
        var report = await ctx.成功する(Get, $"/api/departments/{ctx.課Id(ctx.現在の課!)}/budget-comparison"
            + $"?fiscalHalf={ctx.現在の半期}&baseVersion={baseVersion}&targetVersion={targetVersion}");
        var line = report.GetProperty("categories").EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == 区分(category));
        Assert.Equal(difference, line.GetProperty("difference").GetDecimal());
    }

    // ---- 実績・分析 ----

    [When("API で課 {string} の {string} に次の実績を計上する:")]
    public async Task 実績を計上する(string department, string half, DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(department, code) : null;
            await ctx.成功する(Post, $"/api/departments/{ctx.課Id(department)}/actuals", new
            {
                fiscalHalf = half,
                category = 区分(row["区分"]),
                projectId = project,
                elementCode = 空ならnull(row["費目"]),
                amount = 金額(row["金額"]),
                note = row.TryGetValue("備考", out var note) ? 空ならnull(note) : null,
            }, HttpStatusCode.Created);
        }
    }

    /// <summary>成否を問わず送る(拒否のシナリオ用)。表の 1 行だけを送る。</summary>
    [When("API で課 {string} の {string} に次の実績を計上しようとする:")]
    public async Task 実績を計上しようとする(string department, string half, DataTable table)
    {
        var row = table.Rows.Single();
        var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(department, code) : null;
        await ctx.送信(Post, $"/api/departments/{ctx.課Id(department)}/actuals", new
        {
            fiscalHalf = half,
            category = 区分(row["区分"]),
            projectId = project,
            elementCode = 空ならnull(row["費目"]),
            amount = 金額(row["金額"]),
        });
    }

    [StepDefinition("API で課 {string} の {string} の予実差異を取得する")]
    public Task 予実差異を取得する(string department, string half) =>
        ctx.成功する(Get, $"/api/departments/{ctx.課Id(department)}/variance?fiscalHalf={half}");

    [Then("予実差異の売上高差異は {long}、コスト差異は {long} である")]
    public void 予実差異は(long revenueVariance, long costVariance) =>
        Assert.Equal((revenueVariance, costVariance),
            ((long)ctx.応答本文.GetProperty("revenueVariance").GetDecimal(),
                (long)ctx.応答本文.GetProperty("costVariance").GetDecimal()));

    [Then("予実差異の {string} の明細に案件名 {string} が表示される")]
    public void 案件名が表示される(string category, string projectName)
    {
        var lines = ctx.応答本文.GetProperty("categories").EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == 区分(category))
            .GetProperty("lines").EnumerateArray();
        Assert.Contains(lines, l => l.GetProperty("projectName").GetString() == projectName);
    }

    [Then("API で取得した課 {string} の {string} の予実差異は版 {int} を基準にしている")]
    public async Task 予実差異の基準は(string department, string half, int version)
    {
        await 予実差異を取得する(department, half);
        Assert.Equal(version, ctx.応答本文.GetProperty("budgetVersion").GetInt32());
    }

    [When("API で課 {string} の {string} の損益を取得する")]
    public Task 損益を取得する(string department, string half) =>
        ctx.成功する(Get, $"/api/departments/{ctx.課Id(department)}/profit?fiscalHalf={half}");

    [Then("課全体の実績損益は {long}、実績の期間費用は {long} である")]
    public void 課全体の実績損益は(long profit, long periodCost) =>
        Assert.Equal((profit, periodCost),
            ((long)ctx.応答本文.GetProperty("actualProfit").GetDecimal(),
                (long)ctx.応答本文.GetProperty("actualPeriodCost").GetDecimal()));

    [Then("案件別の実績損益は次のとおりである:")]
    public void 案件別の実績損益は(DataTable table)
    {
        var lines = ctx.応答本文.GetProperty("projectLines").EnumerateArray().ToList();
        Assert.Equal(table.RowCount, lines.Count);
        foreach (var row in table.Rows)
        {
            var line = lines.Single(l => l.GetProperty("projectCode").GetString() == row["案件"]);
            Assert.Equal(金額(row["実績損益"]), line.GetProperty("actualProfit").GetDecimal());
        }
    }

    // ---- 部 ----

    [StepDefinition("API で部 {string} の {string} の予実サマリを取得する")]
    public Task 部の予実サマリを取得する(string division, string half) =>
        ctx.成功する(Get, $"/api/divisions/{ctx.部[division]}/budget-summary?fiscalHalf={half}");

    [Then("部サマリの計画売上高は {long} である")]
    public void 部サマリの計画売上高は(long amount) =>
        Assert.Equal(amount, (long)ctx.応答本文.GetProperty("plannedRevenue").GetDecimal());

    [Then("部サマリの課別内訳は課 {string} の承認済み予算 1 件である")]
    public void 課別内訳は1件(string department)
    {
        var line = Assert.Single(ctx.応答本文.GetProperty("departmentLines").EnumerateArray());
        Assert.Equal(ctx.課Id(department), line.GetProperty("departmentId").GetString());
        Assert.True(line.GetProperty("hasApprovedBudget").GetBoolean());
    }

    [Then("部サマリは承認できる状態で、未承認である")]
    public void 承認できる状態で未承認()
    {
        Assert.True(ctx.応答本文.GetProperty("canApprove").GetBoolean());
        Assert.False(ctx.応答本文.GetProperty("isApproved").GetBoolean());
    }

    /// <summary>部承認は成否を問わず送る(未承認の課があるシナリオでも使う)。</summary>
    [When("API で部 {string} の {string} を承認する")]
    public Task 部を承認する(string division, string half) =>
        ctx.送信(Post, $"/api/divisions/{ctx.部[division]}/budget-approval?fiscalHalf={half}", new { });

    [When("API で部 {string} の {string} の承認を取り消す")]
    public Task 部の承認を取り消す(string division, string half) =>
        ctx.送信(Delete, $"/api/divisions/{ctx.部[division]}/budget-approval?fiscalHalf={half}");

    [Then("API で取得した部 {string} の {string} は承認済みである")]
    public async Task 部は承認済み(string division, string half)
    {
        await 部の予実サマリを取得する(division, half);
        Assert.True(ctx.応答本文.GetProperty("isApproved").GetBoolean());
    }

    [Then("API で取得した部 {string} の {string} は未承認である")]
    public async Task 部は未承認(string division, string half)
    {
        await 部の予実サマリを取得する(division, half);
        Assert.False(ctx.応答本文.GetProperty("isApproved").GetBoolean());
    }

    // ---- 応答 ----

    [Then("応答のステータスは {int} である")]
    public void 応答のステータスは(int status) => Assert.Equal(status, (int)ctx.応答!.StatusCode);

    [Then("応答は {int} で、エラーメッセージに {string} を含む")]
    public void エラー応答は(int status, string message)
    {
        Assert.Equal(status, (int)ctx.応答!.StatusCode);
        Assert.Contains(message, ctx.応答本文.GetProperty("error").GetString());
    }

    // ---- 補助 ----

    private static string Id(JsonElement body) => body.GetProperty("id").GetString()!;

    private string 予算Id() =>
        ctx.予算Id ?? throw new InvalidOperationException("対象の予算がありません。先に予算を起票してください。");
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CostManagement.E2E.Tests;

/// <summary>
/// 実際の WebApi(ルーティング・JSON変換・例外→HTTP変換・DB永続化)を
/// まるごと起動して HTTP 経由で検証する E2E テスト。
/// テストごとに独立した一時 SQLite データベースを使う。
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"cm-e2e-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

public class 業務フロー全体のE2E : IDisposable
{
    private readonly ApiFixture _api = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public 業務フロー全体のE2E()
    {
        _client = _api.CreateClient();
    }

    public void Dispose() => _api.Dispose();

    private async Task<JsonElement> PostAsync(string path, object body,
        HttpStatusCode expected = HttpStatusCode.Created)
    {
        var res = await _client.PostAsJsonAsync(path, body, Json);
        Assert.Equal(expected, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private async Task<JsonElement> PutAsync(string path, object body)
    {
        var res = await _client.PutAsJsonAsync(path, body, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var res = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task 計画策定から実績入力そして計画変更までの一連の業務フローが動作する()
    {
        // ---- 1. 計画策定 ----

        // 1-1. プロジェクトを登録する
        var project = await PostAsync("/api/projects",
            new { code = "SE-001", name = "受託開発2026", fiscalYear = 2026 });
        var projectId = project.GetProperty("id").GetString();

        // 1-2. 売上予算(当初)を策定して承認する
        var revenuePlan = await PostAsync($"/api/projects/{projectId}/revenue-plans",
            new { label = "当初売上予算" });
        var revenuePlanId = revenuePlan.GetProperty("id").GetString();
        await PutAsync($"/api/revenue-plans/{revenuePlanId}/lines",
            new { itemName = "案件A", period = "2026-04", amount = 2_000_000 });
        await PutAsync($"/api/revenue-plans/{revenuePlanId}/lines",
            new { itemName = "案件B", period = "2026-04", amount = 1_000_000 });
        var approvedRevenue = await PostAsync($"/api/revenue-plans/{revenuePlanId}/approve",
            new { }, HttpStatusCode.OK);
        Assert.Equal("Approved", approvedRevenue.GetProperty("status").GetString());

        // 1-3. 原価予算(当初)を策定して承認する(売上対応品目つき + 共通費)
        var costPlan = await PostAsync($"/api/projects/{projectId}/plans",
            new { label = "当初原価予算" });
        var costPlanId = costPlan.GetProperty("id").GetString();
        await PutAsync($"/api/plans/{costPlanId}/lines",
            new { elementCode = "LAB-SE", revenueItem = "案件A", period = "2026-04", amount = 1_400_000 });
        await PutAsync($"/api/plans/{costPlanId}/lines",
            new { elementCode = "LAB-SE", revenueItem = "案件B", period = "2026-04", amount = 700_000 });
        await PutAsync($"/api/plans/{costPlanId}/lines",
            new { elementCode = "OVH-COM", period = "2026-04", amount = 300_000 });
        await PostAsync($"/api/plans/{costPlanId}/approve", new { }, HttpStatusCode.OK);

        // ---- 2. 実績入力 ----

        await PostAsync($"/api/projects/{projectId}/actual-revenues",
            new { itemName = "案件A", period = "2026-04", amount = 2_100_000, note = "検収" });
        await PostAsync($"/api/projects/{projectId}/actual-revenues",
            new { itemName = "案件B", period = "2026-04", amount = 900_000 });
        await PostAsync($"/api/projects/{projectId}/actuals",
            new { elementCode = "LAB-SE", revenueItem = "案件A", period = "2026-04", amount = 1_480_000 });
        await PostAsync($"/api/projects/{projectId}/actuals",
            new { elementCode = "LAB-SE", revenueItem = "案件B", period = "2026-04", amount = 650_000 });
        await PostAsync($"/api/projects/{projectId}/actuals",
            new { elementCode = "OVH-COM", period = "2026-04", amount = 320_000 });

        // ---- 3. 分析: 差異と損益 ----

        // 原価差異: 実績245万 − 予算240万 = +5万(不利)
        var variance = await GetAsync($"/api/projects/{projectId}/variance");
        Assert.Equal(50_000, variance.GetProperty("totalVariance").GetDecimal());

        // 損益: 品目別の粗利と共通費行が返る
        var profit = await GetAsync($"/api/projects/{projectId}/profit");
        Assert.Equal(550_000, profit.GetProperty("actualProfit").GetDecimal());
        var itemLines = profit.GetProperty("itemLines").EnumerateArray().ToList();
        Assert.Equal(3, itemLines.Count); // 案件A・案件B・共通費
        Assert.Contains(itemLines, l =>
            l.GetProperty("itemName").ValueKind == JsonValueKind.Null &&
            l.GetProperty("actualCost").GetDecimal() == 320_000);

        // ---- 4. 計画変更(四半期改定) ----

        var revised = await PostAsync($"/api/projects/{projectId}/plans",
            new { label = "第2四半期改定" });
        var revisedId = revised.GetProperty("id").GetString();
        Assert.Equal(2, revised.GetProperty("version").GetInt32());
        Assert.Equal(3, revised.GetProperty("lines").GetArrayLength()); // 明細を引き継ぐ

        await PutAsync($"/api/plans/{revisedId}/lines",
            new { elementCode = "LAB-SE", revenueItem = "案件A", period = "2026-04", amount = 1_500_000 });
        await PostAsync($"/api/plans/{revisedId}/approve", new { }, HttpStatusCode.OK);

        // 旧バージョンは失効として履歴に残る
        var plans = await GetAsync($"/api/projects/{projectId}/plans");
        var statusByVersion = plans.EnumerateArray()
            .ToDictionary(p => p.GetProperty("version").GetInt32(),
                p => p.GetProperty("status").GetString());
        Assert.Equal("Approved", statusByVersion[2]);
        Assert.Equal("Superseded", statusByVersion[1]);

        // バージョン比較: 増減 +10万
        var comparison = await GetAsync(
            $"/api/projects/{projectId}/plan-comparison?baseVersion=1&targetVersion=2");
        Assert.Equal(100_000, comparison.GetProperty("totalDifference").GetDecimal());

        // 差異分析の既定基準は最新承認版(v2)に切り替わる
        var varianceAfter = await GetAsync($"/api/projects/{projectId}/variance");
        Assert.Equal(2, varianceAfter.GetProperty("planVersion").GetInt32());
    }
}

public class エラー応答のE2E : IDisposable
{
    private readonly ApiFixture _api = new();
    private readonly HttpClient _client;

    public エラー応答のE2E()
    {
        _client = _api.CreateClient();
    }

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task 存在しないリソースは404とエラーメッセージを返す()
    {
        var res = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("見つかりません", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ドメインルール違反は400とエラーメッセージを返す()
    {
        var project = await _client.PostAsJsonAsync("/api/projects",
            new { code = "SE-001", name = "テスト", fiscalYear = 2026 });
        var projectId = (await project.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        // 明細のない予算は承認できない
        var plan = await _client.PostAsJsonAsync($"/api/projects/{projectId}/plans",
            new { label = "空の予算" });
        var planId = (await plan.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();
        var res = await _client.PostAsJsonAsync($"/api/plans/{planId}/approve", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("承認できません", body.GetProperty("error").GetString());
    }
}

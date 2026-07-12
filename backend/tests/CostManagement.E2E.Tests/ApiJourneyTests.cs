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

        // 1-1. 部・課・案件を登録する
        var division = await PostAsync("/api/divisions", new { code = "SALES", name = "営業本部" });
        var divisionId = division.GetProperty("id").GetString();

        var dept = await PostAsync($"/api/divisions/{divisionId}/departments",
            new { code = "DEV-1", name = "開発1課" });
        var deptId = dept.GetProperty("id").GetString();

        var projectA = await PostAsync($"/api/departments/{deptId}/projects",
            new { code = "PJ-A", name = "案件A" });
        var projectAId = projectA.GetProperty("id").GetString();
        var projectB = await PostAsync($"/api/departments/{deptId}/projects",
            new { code = "PJ-B", name = "案件B" });
        var projectBId = projectB.GetProperty("id").GetString();

        // 1-2. 2026年度上期の予算を策定する(4区分)
        var budget = await PostAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "当初予算" });
        var budgetId = budget.GetProperty("id").GetString();
        Assert.Equal(1, budget.GetProperty("version").GetInt32());

        await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "Revenue", projectId = projectAId, amount = 2_000_000 });
        await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "Revenue", projectId = projectBId, amount = 1_000_000 });
        await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "Processing", projectId = projectAId, amount = 1_400_000 });
        await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "Outsourcing", projectId = projectBId, amount = 700_000 });
        var withPeriodCost = await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "PeriodCost", elementCode = "PERSONNEL", amount = 300_000 });

        // 課の区分合計 = 案件明細の合計
        Assert.Equal(3_000_000, withPeriodCost.GetProperty("revenueTotal").GetDecimal());
        Assert.Equal(1_400_000, withPeriodCost.GetProperty("processingTotal").GetDecimal());
        Assert.Equal(700_000, withPeriodCost.GetProperty("outsourcingTotal").GetDecimal());
        Assert.Equal(300_000, withPeriodCost.GetProperty("periodCostTotal").GetDecimal());
        Assert.Equal(600_000, withPeriodCost.GetProperty("plannedProfit").GetDecimal());

        // 1-3. 予算を承認する
        var approved = await PostAsync($"/api/budgets/{budgetId}/approve",
            new { }, HttpStatusCode.OK);
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        // ---- 2. 実績入力 ----

        await PostAsync($"/api/departments/{deptId}/actuals",
            new { fiscalHalf = "2026-H1", category = "Revenue", projectId = projectAId,
                amount = 2_100_000, note = "検収" });
        await PostAsync($"/api/departments/{deptId}/actuals",
            new { fiscalHalf = "2026-H1", category = "Revenue", projectId = projectBId,
                amount = 900_000 });
        await PostAsync($"/api/departments/{deptId}/actuals",
            new { fiscalHalf = "2026-H1", category = "Processing", projectId = projectAId,
                amount = 1_480_000 });
        await PostAsync($"/api/departments/{deptId}/actuals",
            new { fiscalHalf = "2026-H1", category = "Outsourcing", projectId = projectBId,
                amount = 650_000 });
        await PostAsync($"/api/departments/{deptId}/actuals",
            new { fiscalHalf = "2026-H1", category = "PeriodCost", elementCode = "PERSONNEL",
                amount = 320_000 });

        // ---- 3. 分析: 差異と損益 ----

        // コスト差異: 実績245万 − 予算240万 = +5万(不利)
        var variance = await GetAsync($"/api/departments/{deptId}/variance?fiscalHalf=2026-H1");
        Assert.Equal(50_000, variance.GetProperty("costVariance").GetDecimal());
        Assert.Equal(0, variance.GetProperty("revenueVariance").GetDecimal());

        // 案件名が差異明細で解決される
        var revenueCategory = variance.GetProperty("categories").EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == "Revenue");
        Assert.Contains(revenueCategory.GetProperty("lines").EnumerateArray(),
            l => l.GetProperty("projectName").GetString() == "案件A");

        // 損益: 全体は期間費用込み、案件別は売上 − 加工費 − 外注費
        var profit = await GetAsync($"/api/departments/{deptId}/profit?fiscalHalf=2026-H1");
        Assert.Equal(550_000, profit.GetProperty("actualProfit").GetDecimal());
        Assert.Equal(320_000, profit.GetProperty("actualPeriodCost").GetDecimal());
        var projectLines = profit.GetProperty("projectLines").EnumerateArray().ToList();
        Assert.Equal(2, projectLines.Count);
        var 案件A損益 = projectLines.Single(l => l.GetProperty("projectCode").GetString() == "PJ-A");
        Assert.Equal(620_000, 案件A損益.GetProperty("actualProfit").GetDecimal()); // 210万−148万

        // ---- 4. 計画変更(半期途中の見直し) ----

        var revised = await PostAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "上期見直し" });
        var revisedId = revised.GetProperty("id").GetString();
        Assert.Equal(2, revised.GetProperty("version").GetInt32());
        Assert.Equal(5, revised.GetProperty("lines").GetArrayLength()); // 明細を引き継ぐ

        await PutAsync($"/api/budgets/{revisedId}/lines",
            new { category = "Processing", projectId = projectAId, amount = 1_500_000 });
        await PostAsync($"/api/budgets/{revisedId}/approve", new { }, HttpStatusCode.OK);

        // 旧バージョンは失効として履歴に残る
        var budgets = await GetAsync($"/api/departments/{deptId}/budgets?fiscalHalf=2026-H1");
        var statusByVersion = budgets.EnumerateArray()
            .ToDictionary(b => b.GetProperty("version").GetInt32(),
                b => b.GetProperty("status").GetString());
        Assert.Equal("Approved", statusByVersion[2]);
        Assert.Equal("Superseded", statusByVersion[1]);

        // バージョン比較: 加工費 +10万
        var comparison = await GetAsync(
            $"/api/departments/{deptId}/budget-comparison?fiscalHalf=2026-H1&baseVersion=1&targetVersion=2");
        var processingDiff = comparison.GetProperty("categories").EnumerateArray()
            .Single(c => c.GetProperty("category").GetString() == "Processing");
        Assert.Equal(100_000, processingDiff.GetProperty("difference").GetDecimal());

        // 差異分析の既定基準は最新承認版(v2)に切り替わる
        var varianceAfter = await GetAsync($"/api/departments/{deptId}/variance?fiscalHalf=2026-H1");
        Assert.Equal(2, varianceAfter.GetProperty("budgetVersion").GetInt32());

        // ---- 5. 部の予実サマリに配下課の予実が合計される ----
        var divisionSummary = await GetAsync(
            $"/api/divisions/{divisionId}/budget-summary?fiscalHalf=2026-H1");
        Assert.Equal(3_000_000, divisionSummary.GetProperty("plannedRevenue").GetDecimal());
        var deptLine = divisionSummary.GetProperty("departmentLines").EnumerateArray().Single();
        Assert.True(deptLine.GetProperty("hasApprovedBudget").GetBoolean());
        Assert.Equal(deptId, deptLine.GetProperty("departmentId").GetString());
        // 配下課がすべて承認済みなので部承認が可能・未承認
        Assert.True(divisionSummary.GetProperty("canApprove").GetBoolean());
        Assert.False(divisionSummary.GetProperty("isApproved").GetBoolean());

        // ---- 6. 部予算の承認・取り消し ----
        // 部を承認する → isApproved
        var approveRes = await _client.PostAsJsonAsync(
            $"/api/divisions/{divisionId}/budget-approval?fiscalHalf=2026-H1", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approveRes.StatusCode);
        var afterApprove = await GetAsync($"/api/divisions/{divisionId}/budget-summary?fiscalHalf=2026-H1");
        Assert.True(afterApprove.GetProperty("isApproved").GetBoolean());

        // 課を改定・再承認しても部承認は残る(独立)
        var revised3 = await PostAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "再見直し" });
        await PostAsync($"/api/budgets/{revised3.GetProperty("id").GetString()}/approve",
            new { }, HttpStatusCode.OK);
        var afterRevise = await GetAsync($"/api/divisions/{divisionId}/budget-summary?fiscalHalf=2026-H1");
        Assert.True(afterRevise.GetProperty("isApproved").GetBoolean());

        // 部承認を取り消す → 未承認に戻る
        var revokeRes = await _client.DeleteAsync(
            $"/api/divisions/{divisionId}/budget-approval?fiscalHalf=2026-H1");
        Assert.Equal(HttpStatusCode.NoContent, revokeRes.StatusCode);
        var afterRevoke = await GetAsync($"/api/divisions/{divisionId}/budget-summary?fiscalHalf=2026-H1");
        Assert.False(afterRevoke.GetProperty("isApproved").GetBoolean());
    }

    [Fact]
    public async Task 費目マスタを拡張して期間費用に利用できる()
    {
        var division = await PostAsync("/api/divisions", new { code = "SALES", name = "営業本部" });
        var divisionId = division.GetProperty("id").GetString();
        var dept = await PostAsync($"/api/divisions/{divisionId}/departments",
            new { code = "DEV-9", name = "開発9課" });
        var deptId = dept.GetProperty("id").GetString();

        // シード済みの標準費目(人件費・ライセンス費)を確認
        var elements = await GetAsync("/api/cost-elements");
        var codes = elements.EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Contains("PERSONNEL", codes);
        Assert.Contains("LICENSE", codes);

        // 費目を追加して期間費用の明細に使う
        await PostAsync("/api/cost-elements", new { code = "TRAINING", name = "教育研修費" });
        var budget = await PostAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "当初予算" });
        var budgetId = budget.GetProperty("id").GetString();
        var updated = await PutAsync($"/api/budgets/{budgetId}/lines",
            new { category = "PeriodCost", elementCode = "TRAINING", amount = 250_000 });

        Assert.Equal(250_000, updated.GetProperty("periodCostTotal").GetDecimal());
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
        var res = await _client.GetAsync($"/api/departments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("見つかりません", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ドメインルール違反は400とエラーメッセージを返す()
    {
        var division = await _client.PostAsJsonAsync("/api/divisions",
            new { code = "SALES", name = "営業本部" });
        var divisionId = (await division.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();
        var dept = await _client.PostAsJsonAsync($"/api/divisions/{divisionId}/departments",
            new { code = "DEV-1", name = "開発1課" });
        var deptId = (await dept.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        // 明細のない予算は承認できない
        var budget = await _client.PostAsJsonAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "空の予算" });
        var budgetId = (await budget.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();
        var res = await _client.PostAsJsonAsync($"/api/budgets/{budgetId}/approve", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("承認できません", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task 不正な半期の形式は400を返す()
    {
        var division = await _client.PostAsJsonAsync("/api/divisions",
            new { code = "SALES", name = "営業本部" });
        var divisionId = (await division.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();
        var dept = await _client.PostAsJsonAsync($"/api/divisions/{divisionId}/departments",
            new { code = "DEV-2", name = "開発2課" });
        var deptId = (await dept.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        var res = await _client.PostAsJsonAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-04", label = "不正な半期" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("半期の形式が不正です", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task 未承認の課がある部は承認できず400を返す()
    {
        var division = await _client.PostAsJsonAsync("/api/divisions",
            new { code = "SALES", name = "営業本部" });
        var divisionId = (await division.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();
        // 課だけ作り予算は未承認のまま
        await _client.PostAsJsonAsync($"/api/divisions/{divisionId}/departments",
            new { code = "DEV-1", name = "開発1課" });

        var res = await _client.PostAsJsonAsync(
            $"/api/divisions/{divisionId}/budget-approval?fiscalHalf=2026-H1", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("未承認の予算がある課", body.GetProperty("error").GetString());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CostManagement.E2E.Tests;

/// <summary>
/// 予算への保存の楽観ロックの HTTP の入口と出口(SPEC-004)。
/// 4 つの操作のクエリ updateNumber、応答の updateNumber、衝突の 409 と ErrorResponse、入力の 400、予算がない 404。
/// テスト名の「行 n」は SPEC-004 の具体例の行番号。
/// </summary>
public class 予算への保存の楽観ロックのE2E : IDisposable
{
    private const string 衝突の文言 =
        "ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _api = new();
    private readonly HttpClient _client;

    public 予算への保存の楽観ロックのE2E()
    {
        _client = _api.CreateClient();
    }

    public void Dispose() => _api.Dispose();

    private async Task<JsonElement> 本文(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>(Json);

    private async Task<JsonElement> 成功(Task<HttpResponseMessage> call, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await call;
        Assert.True(res.StatusCode == expected, $"期待 {expected} / 実際 {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return await 本文(res);
    }

    /// <summary>課・案件 PJ-A・PJ-B と、具体例の予算 P(明細 3 件のドラフト)を作り、(予算 Id, PJ-A, PJ-B, 予算) を返す。</summary>
    private async Task<(string Id, string PjA, string PjB, JsonElement P)> 予算Pを作る()
    {
        var division = await 成功(_client.PostAsJsonAsync("/api/divisions", new { code = "SALES", name = "営業本部" }, Json),
            HttpStatusCode.Created);
        var dept = await 成功(_client.PostAsJsonAsync($"/api/divisions/{division.GetProperty("id")}/departments",
            new { code = "DEV-1", name = "開発1課" }, Json), HttpStatusCode.Created);
        var deptId = dept.GetProperty("id").GetString();
        var pjA = (await 成功(_client.PostAsJsonAsync($"/api/departments/{deptId}/projects",
            new { code = "PJ-A", name = "案件A" }, Json), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var pjB = (await 成功(_client.PostAsJsonAsync($"/api/departments/{deptId}/projects",
            new { code = "PJ-B", name = "案件B" }, Json), HttpStatusCode.Created)).GetProperty("id").GetString()!;
        var p = await 成功(_client.PostAsJsonAsync($"/api/departments/{deptId}/budgets",
            new { fiscalHalf = "2026-H1", label = "当初予算" }, Json), HttpStatusCode.Created);
        var id = p.GetProperty("id").GetString()!;
        p = await 明細を保存(id, 番号(p), new { category = "Revenue", projectId = pjA, amount = 1_000_000 });
        p = await 明細を保存(id, 番号(p), new { category = "Revenue", projectId = pjB, amount = 500_000 });
        p = await 明細を保存(id, 番号(p), new { category = "PeriodCost", elementCode = "LICENSE", amount = 300_000, periodDetail = "AWS" });
        return (id, pjA, pjB, p);
    }

    private static int 番号(JsonElement budget) => budget.GetProperty("updateNumber").GetInt32();

    private Task<HttpResponseMessage> 明細の保存を送る(string id, string number, object body) =>
        _client.PutAsJsonAsync($"/api/budgets/{id}/lines?updateNumber={number}", body, Json);

    private Task<JsonElement> 明細を保存(string id, int number, object body) =>
        成功(明細の保存を送る(id, number.ToString(System.Globalization.CultureInfo.InvariantCulture), body));

    private async Task<JsonElement> 予算を読む(string id) => await 成功(_client.GetAsync($"/api/budgets/{id}"));

    /// <summary>応答が 409 と衝突の文言で、予算(明細・状態・更新番号)が変わっていないことを確かめる。</summary>
    private async Task 衝突で拒否され予算は変わらない(string id, Func<Task<HttpResponseMessage>> call)
    {
        var before = (await 予算を読む(id)).GetRawText();

        var res = await call();

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(衝突の文言, (await 本文(res)).GetProperty("error").GetString());
        Assert.Equal(before, (await 予算を読む(id)).GetRawText());
    }

    [Fact]
    public async Task 行1と行3_起票の応答の更新番号は1で受け付けた保存の応答は1増えた更新番号を返す()
    {
        var division = await 成功(_client.PostAsJsonAsync("/api/divisions", new { code = "S", name = "部" }, Json),
            HttpStatusCode.Created);
        var dept = await 成功(_client.PostAsJsonAsync($"/api/divisions/{division.GetProperty("id")}/departments",
            new { code = "D", name = "課" }, Json), HttpStatusCode.Created);
        var created = await 成功(_client.PostAsJsonAsync($"/api/departments/{dept.GetProperty("id")}/budgets",
            new { fiscalHalf = "2026-H1", label = "当初予算" }, Json), HttpStatusCode.Created);
        Assert.Equal(1, 番号(created));

        var (id, pjA, _, p) = await 予算Pを作る();
        var n = 番号(p);
        var res = await 明細を保存(id, n, new { category = "Revenue", projectId = pjA, amount = 1_200_000 });

        Assert.Equal(n + 1, 番号(res));
        Assert.Equal(n + 1, 番号(await 予算を読む(id)));
        var list = await 成功(_client.GetAsync(
            $"/api/departments/{p.GetProperty("departmentId")}/budgets?fiscalHalf=2026-H1"));
        Assert.Equal(n + 1, 番号(list.EnumerateArray().Single()));
    }

    [Fact]
    public async Task 行8_ほかの保存のあとの古い番号での明細の保存は409と衝突の文言を返す()
    {
        var (id, pjA, _, p) = await 予算Pを作る();
        var n = 番号(p);
        await 明細を保存(id, n, new { category = "Revenue", projectId = pjA, amount = 1_200_000 });

        await 衝突で拒否され予算は変わらない(id, () => 明細の保存を送る(id, $"{n}",
            new { category = "Revenue", projectId = pjA, amount = 900_000 }));
    }

    [Fact]
    public async Task 行12_ほかの保存のあとの古い番号での明細の削除は409と衝突の文言を返す()
    {
        var (id, pjA, pjB, p) = await 予算Pを作る();
        var n = 番号(p);
        await 明細を保存(id, n, new { category = "Revenue", projectId = pjA, amount = 1_200_000 });

        await 衝突で拒否され予算は変わらない(id, () => _client.DeleteAsync(
            $"/api/budgets/{id}/lines?category=Revenue&projectId={pjB}&updateNumber={n}"));
    }

    [Fact]
    public async Task 行13_ほかの保存のあとの古い番号での明細名の変更は409と衝突の文言を返す()
    {
        var (id, pjA, _, p) = await 予算Pを作る();
        var n = 番号(p);
        await 明細を保存(id, n, new { category = "Revenue", projectId = pjA, amount = 1_200_000 });

        await 衝突で拒否され予算は変わらない(id, () => _client.PostAsJsonAsync(
            $"/api/budgets/{id}/rename-period-detail?updateNumber={n}",
            new { elementCode = "LICENSE", oldDetail = "AWS", newDetail = "AWS本番" }, Json));
    }

    [Fact]
    public async Task 行16_ほかの保存のあとの古い番号での承認は409と衝突の文言を返す()
    {
        var (id, pjA, _, p) = await 予算Pを作る();
        var n = 番号(p);
        await 明細を保存(id, n, new { category = "Revenue", projectId = pjA, amount = 1_200_000 });

        await 衝突で拒否され予算は変わらない(id, () => _client.PostAsJsonAsync(
            $"/api/budgets/{id}/approve?updateNumber={n}", new { }, Json));
    }

    [Fact]
    public async Task 一致する番号での削除と明細名の変更と承認は200と1増えた更新番号を返す()
    {
        var (id, _, pjB, p) = await 予算Pを作る();
        var n = 番号(p);

        var removed = await 成功(_client.DeleteAsync(
            $"/api/budgets/{id}/lines?category=Revenue&projectId={pjB}&updateNumber={n}"));
        var renamed = await 成功(_client.PostAsJsonAsync($"/api/budgets/{id}/rename-period-detail?updateNumber={n + 1}",
            new { elementCode = "LICENSE", oldDetail = "AWS", newDetail = "AWS本番" }, Json));
        var approved = await 成功(_client.PostAsJsonAsync($"/api/budgets/{id}/approve?updateNumber={n + 2}", new { }, Json));

        Assert.Equal(n + 1, 番号(removed));
        Assert.Equal(n + 2, 番号(renamed));
        Assert.Equal(n + 3, 番号(approved));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task 行22_承認済みの予算への保存は409ではなく400と状態の文言を返す()
    {
        var (id, pjA, _, p) = await 予算Pを作る();
        var n = 番号(p);
        await 成功(_client.PostAsJsonAsync($"/api/budgets/{id}/approve?updateNumber={n}", new { }, Json));

        var res = await 明細の保存を送る(id, $"{n}", new { category = "Revenue", projectId = pjA, amount = 900_000 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("承認済み・失効済みの予算は編集できません。改定版を作成してください。",
            (await 本文(res)).GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public async Task 行31と行39_クエリのupdateNumberがない保存や整数でない保存は400で予算は変わらない(string? number)
    {
        var (id, pjA, _, _) = await 予算Pを作る();
        var before = (await 予算を読む(id)).GetRawText();
        var path = number is null ? $"/api/budgets/{id}/lines" : $"/api/budgets/{id}/lines?updateNumber={number}";

        var res = await _client.PutAsJsonAsync(path, new { category = "Revenue", projectId = pjA, amount = 900_000 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(before, (await 予算を読む(id)).GetRawText());
    }

    [Fact]
    public async Task 行32_存在しない予算への保存は404と予算が見つからない文言を返す()
    {
        var missing = Guid.NewGuid();

        var res = await 明細の保存を送る(missing.ToString(), "1",
            new { category = "Revenue", projectId = Guid.NewGuid(), amount = 1 });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal($"予算が見つかりません: {missing}", (await 本文(res)).GetProperty("error").GetString());
    }
}

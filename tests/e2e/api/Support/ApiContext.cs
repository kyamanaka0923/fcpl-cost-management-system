using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CostManagement.E2E.Tests.Support;

/// <summary>
/// Gherkin シナリオ1本ぶんの API 呼び出しの状態。Reqnroll のコンテキスト注入で各ステップ定義クラスに共有され、
/// シナリオ終了時に Dispose されて WebApi と一時 SQLite が破棄される。
/// フィーチャーでは部・課・案件をコード(= 名称)で書き、ここで API が払い出した ID へ解決する。
/// </summary>
public sealed class APIコンテキスト : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private WebApplicationFactory<Program>? _api;
    private HttpClient? _client;

    public Dictionary<string, string> 部 { get; } = new();
    public Dictionary<string, string> 課 { get; } = new();
    public Dictionary<(string 課, string 案件), string> 案件 { get; } = new();

    /// <summary>直近に起票した予算。「API で予算を承認する」等の主語を省いたステップが対象にする。</summary>
    public string? 現在の課 { get; set; }
    public string? 現在の半期 { get; set; }
    public string? 予算Id { get; set; }

    /// <summary>直近の応答。「応答は … である」系のステップで検証する。</summary>
    public HttpResponseMessage? 応答 { get; private set; }
    public JsonElement 応答本文 { get; private set; }

    /// <summary>認証を有効にした WebApi で起動する(最初の呼び出しより前に使う)。</summary>
    public void 認証ありで起動する()
    {
        if (_api is not null)
            throw new InvalidOperationException("WebApi は既に起動しています。");
        _api = new 認証ありのApi();
    }

    public WebApplicationFactory<Program> Api => _api ??= new ApiFixture();

    public HttpClient Client => _client ??= Api.CreateClient();

    public Task<HttpResponseMessage> 送信(HttpMethod method, string path, object? body = null) =>
        送信(new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        });

    /// <summary>リクエストを送り、応答と本文(JSON なら)を記録する。</summary>
    public async Task<HttpResponseMessage> 送信(HttpRequestMessage request)
    {
        応答 = await Client.SendAsync(request);
        var text = await 応答.Content.ReadAsStringAsync();
        応答本文 = text.TrimStart() is ['{' or '[', ..]
            ? JsonDocument.Parse(text).RootElement.Clone()
            : default;
        return 応答;
    }

    /// <summary>成功を前提とした呼び出し。期待と違うステータスならエラー本文つきで失敗させる。</summary>
    public async Task<JsonElement> 成功する(HttpMethod method, string path, object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await 送信(method, path, body);
        Assert.True(res.StatusCode == expected,
            $"{method} {path}: 期待 {(int)expected} / 実際 {(int)res.StatusCode} {応答本文}");
        return 応答本文;
    }

    public string 課Id(string code) =>
        課.TryGetValue(code, out var id) ? id : throw new InvalidOperationException($"課 \"{code}\" が未登録です。");

    public string 案件Id(string dept, string code) =>
        案件.TryGetValue((dept, code), out var id)
            ? id
            : throw new InvalidOperationException($"課 \"{dept}\" の案件 \"{code}\" が未登録です。");

    // ---- 日本語表記 ↔ API の表記 ----

    public static string 区分(string 日本語) => 日本語 switch
    {
        "売上高" => "Revenue",
        "加工費" => "Processing",
        "外注費" => "Outsourcing",
        "期間費用" => "PeriodCost",
        _ => throw new ArgumentException($"未知の区分です: {日本語}"),
    };

    public static decimal 金額(string cell) => decimal.Parse(cell, CultureInfo.InvariantCulture);

    public static string? 空ならnull(string? cell) => string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();

    public void Dispose()
    {
        _client?.Dispose();
        _api?.Dispose();
    }
}

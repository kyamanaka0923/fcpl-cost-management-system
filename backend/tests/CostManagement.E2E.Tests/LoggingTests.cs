using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CostManagement.E2E.Tests;

/// <summary>テスト用に記録したログ1件(イベントID・メッセージ・スコープの値)。</summary>
public sealed record 記録されたログ(int EventId, string Category, string Message,
    IReadOnlyDictionary<string, object?> Scope);

/// <summary>ログをメモリに集めるプロバイダ(スコープの値も取り出す)。</summary>
public sealed class 記録用ロガー : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<記録されたログ> ログ { get; } = new();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
    public void Dispose() { }

    private sealed class Logger(記録用ロガー owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, object?>();
            owner._scopes.ForEachScope((s, d) =>
            {
                if (s is IEnumerable<KeyValuePair<string, object>> pairs)
                    foreach (var (k, v) in pairs)
                        d[k] = v;
            }, scope);
            owner.ログ.Enqueue(new 記録されたログ(eventId.Id, category, formatter(state, exception), scope));
        }
    }
}

public class ログ出力のE2E
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static (WebApplicationFactory<Program> Api, 記録用ロガー Logs) 起動(WebApplicationFactory<Program> api)
    {
        var logs = new 記録用ロガー();
        return (api.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs))), logs);
    }

    private static async Task<string> 承認済み予算を作る(HttpClient client)
    {
        async Task<JsonElement> Post(string path, object body)
        {
            var res = await client.PostAsJsonAsync(path, body, Json);
            Assert.True(res.IsSuccessStatusCode, $"{path}: {res.StatusCode}");
            return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        }
        var division = await Post("/api/divisions", new { code = "LOG", name = "ログ検証部" });
        var dept = await Post($"/api/divisions/{division.GetProperty("id")}/departments",
            new { code = "LOG-1", name = "ログ検証課" });
        var deptId = dept.GetProperty("id").GetString();
        var budget = await Post($"/api/departments/{deptId}/budgets", new { fiscalHalf = "2026-H1", label = "当初予算" });
        var budgetId = budget.GetProperty("id").GetString();
        var line = await client.PutAsJsonAsync($"/api/budgets/{budgetId}/lines",
            new { category = "PeriodCost", elementCode = "PERSONNEL", amount = 100 }, Json);
        Assert.Equal(HttpStatusCode.OK, line.StatusCode);
        await Post($"/api/budgets/{budgetId}/approve", new { });
        return budgetId!;
    }

    [Fact]
    public async Task 予算の起票と承認は業務イベントとして予算IDと版つきで記録される()
    {
        var (api, logs) = 起動(new ApiFixture());
        using var _ = api;
        var budgetId = await 承認済み予算を作る(api.CreateClient());

        var created = Assert.Single(logs.ログ, l => l.EventId == 1001);
        Assert.Contains(budgetId, created.Message);
        var approved = Assert.Single(logs.ログ, l => l.EventId == 1002);
        Assert.Contains(budgetId, approved.Message);
        Assert.Contains("2026-H1 v1", approved.Message);
    }

    [Fact]
    public async Task リクエストごとにメソッドとパスとステータスが記録され業務エラーは理由も残る()
    {
        var (api, logs) = 起動(new ApiFixture());
        using var _ = api;
        var res = await api.CreateClient().GetAsync($"/api/departments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        Assert.Contains(logs.ログ, l => l.EventId == 2003 && l.Message.Contains("課が見つかりません"));
        var request = Assert.Single(logs.ログ, l => l.EventId == 2001);
        Assert.Contains("GET /api/departments/", request.Message);
        Assert.Contains("responded 404", request.Message);
        Assert.Equal("anonymous", request.Scope["User"]);
        Assert.False(string.IsNullOrEmpty(request.Scope["RequestId"]?.ToString()));
    }

    [Fact]
    public async Task 認証ありでは操作した利用者がリクエストと業務イベントのログに付く()
    {
        var (api, logs) = 起動(new 認証ありのApi());
        using var _ = api;
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", 認証ありのApi.トークン());
        await 承認済み予算を作る(client);

        var approved = Assert.Single(logs.ログ, l => l.EventId == 1002);
        Assert.Equal("taro", approved.Scope["User"]);
        Assert.All(logs.ログ.Where(l => l.EventId == 2001), l => Assert.Equal("taro", l.Scope["User"]));
    }

    [Fact]
    public async Task 認証で拒否されたリクエストも401として記録される()
    {
        var (api, logs) = 起動(new 認証ありのApi());
        using var _ = api;
        var res = await api.CreateClient().GetAsync("/api/divisions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        var request = Assert.Single(logs.ログ, l => l.EventId == 2001);
        Assert.Contains("responded 401", request.Message);
        Assert.Equal("anonymous", request.Scope["User"]);
    }
}

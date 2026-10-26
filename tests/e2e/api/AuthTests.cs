using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace CostManagement.E2E.Tests;

public class 認証のE2E : IDisposable
{
    private readonly 認証ありのApi _api = new();

    public void Dispose() => _api.Dispose();

    private async Task<HttpStatusCode> 部一覧を取得(string? token)
    {
        var client = _api.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/api/divisions")).StatusCode;
    }

    [Fact]
    public async Task 認証を設定するとトークンなしのAPI呼び出しは401になる()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await 部一覧を取得(null));
    }

    [Fact]
    public async Task 正しいアクセストークンならAPIを呼び出せる()
    {
        Assert.Equal(HttpStatusCode.OK, await 部一覧を取得(認証ありのApi.トークン()));
    }

    [Fact]
    public async Task 別のアプリクライアントに発行されたトークンは拒否される()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await 部一覧を取得(認証ありのApi.トークン(clientId: "other")));
    }

    [Fact]
    public async Task IDトークンではAPIを呼び出せない()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await 部一覧を取得(認証ありのApi.トークン(tokenUse: "id")));
    }

    [Fact]
    public async Task 期限切れのトークンは拒否される()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            await 部一覧を取得(認証ありのApi.トークン(expires: DateTime.UtcNow.AddMinutes(-10))));
    }

    [Fact]
    public async Task 発行者が違うトークンは拒否される()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            await 部一覧を取得(認証ありのApi.トークン(issuer: "https://evil.example")));
    }

    [Fact]
    public async Task 署名鍵が違うトークンは拒否される()
    {
        var other = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-that-is-also-long-enough-9876543210"));
        Assert.Equal(HttpStatusCode.Unauthorized, await 部一覧を取得(認証ありのApi.トークン(key: other)));
    }
}

public class 認証設定の必須チェック
{
    [Fact]
    public void Development以外で認証設定がなければ起動しない()
    {
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:Default",
                $"Data Source={Path.Combine(Path.GetTempPath(), $"cm-noauth-{Guid.NewGuid():N}.db")}");
        });
        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());
        Assert.Contains("Auth:Authority", error.ToString());
    }
}

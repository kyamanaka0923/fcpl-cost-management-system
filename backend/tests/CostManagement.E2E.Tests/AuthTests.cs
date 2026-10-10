using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CostManagement.E2E.Tests;

/// <summary>
/// 認証を有効にした WebApi。Cognito の代わりに、テスト用の対称鍵で署名したトークンを検証する
/// (発行者のメタデータ取得は行わない)。
/// </summary>
public sealed class 認証ありのApi : WebApplicationFactory<Program>
{
    public const string Issuer = "https://cognito-idp.ap-northeast-1.amazonaws.com/ap-northeast-1_TEST";
    public const string ClientId = "test-client";
    public static readonly SymmetricSecurityKey Key =
        new(Encoding.UTF8.GetBytes("e2e-test-signing-key-that-is-long-enough-0123456789"));

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cm-auth-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Auth:Authority", Issuer);
        builder.UseSetting("Auth:ClientId", ClientId);
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.Authority = null;
                jwt.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                jwt.TokenValidationParameters.IssuerSigningKey = Key;
            }));
    }

    /// <summary>Cognito のアクセストークンと同じ形のトークンを作る。</summary>
    public static string トークン(string issuer = Issuer, string clientId = ClientId,
        string tokenUse = "access", DateTime? expires = null, SecurityKey? key = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("username", "taro"),
                new Claim("client_id", clientId),
                new Claim("token_use", tokenUse),
            ]),
            NotBefore = (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key ?? Key, SecurityAlgorithms.HmacSha256),
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

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

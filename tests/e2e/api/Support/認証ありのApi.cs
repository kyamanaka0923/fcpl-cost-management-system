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

namespace CostManagement.E2E.Tests.Support;

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

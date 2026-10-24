using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Reqnroll;

namespace CostManagement.E2E.Tests.Features.Steps;

/// <summary>Cognito のアクセストークンによる認証のステップ。トークンは <see cref="認証ありのApi"/> で作る。</summary>
[Binding]
public sealed class 認証のステップ(APIコンテキスト ctx)
{
    private Exception? _起動エラー;

    [Given("API の認証が有効になっている")]
    public void 認証が有効になっている() => ctx.認証ありで起動する();

    [When("{string} を付けて部の一覧を取得する")]
    public Task 部の一覧を取得する(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/divisions");
        if (トークン(token) is { } jwt)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return ctx.送信(request);
    }

    [When("認証の設定をせずに本番環境で API を起動する")]
    public void 本番環境で起動する()
    {
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:Default",
                $"Data Source={Path.Combine(Path.GetTempPath(), $"cm-noauth-{Guid.NewGuid():N}.db")}");
        });
        _起動エラー = Record.Exception(() => api.CreateClient());
    }

    [Then("{string} を含むエラーで起動に失敗する")]
    public void 起動に失敗する(string message)
    {
        Assert.NotNull(_起動エラー);
        Assert.Contains(message, _起動エラー.ToString());
    }

    /// <summary>フィーチャーに書いたトークンの種類を、実際のトークン文字列にする(「トークンなし」は null)。</summary>
    private static string? トークン(string 種類) => 種類 switch
    {
        "トークンなし" => null,
        "正しいアクセストークン" => 認証ありのApi.トークン(),
        "別のアプリクライアントに発行されたトークン" => 認証ありのApi.トークン(clientId: "other"),
        "IDトークン" => 認証ありのApi.トークン(tokenUse: "id"),
        "期限切れのトークン" => 認証ありのApi.トークン(expires: DateTime.UtcNow.AddMinutes(-10)),
        "発行者が違うトークン" => 認証ありのApi.トークン(issuer: "https://evil.example"),
        "署名鍵が違うトークン" => 認証ありのApi.トークン(key: new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("another-key-that-is-also-long-enough-9876543210"))),
        _ => throw new ArgumentException($"未知のトークンの種類です: {種類}"),
    };
}

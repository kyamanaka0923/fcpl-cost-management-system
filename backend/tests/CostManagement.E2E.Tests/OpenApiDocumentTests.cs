using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CostManagement.E2E.Tests;

/// <summary>
/// OpenAPI 定義(docs/api/openapi.json)がコードと一致していることを確かめる。
/// フロントエンドの型(frontend/src/api-schema.d.ts)はこのファイルから生成するため、
/// DTO やエンドポイントを変えたら定義を更新し、フロントの型も作り直す必要がある。
/// 更新方法: UPDATE_OPENAPI=1 dotnet test --filter OpenApi → cd frontend && npm run gen:api
/// </summary>
public class OpenAPI定義
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>リポジトリ直下の docs/api/openapi.json の場所(テストの出力先から遡って探す)。</summary>
    private static string 定義ファイル()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("docs/ が見つかりません"),
            "docs", "api", "openapi.json");
    }

    [Fact]
    public async Task コミット済みのOpenAPI定義は現在のAPIと一致する()
    {
        using var api = new ApiFixture();
        var res = await api.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // 実行環境に依存する servers(テストサーバの URL)は除き、整形して比較する
        var document = JsonNode.Parse(await res.Content.ReadAsStringAsync())!.AsObject();
        document.Remove("servers");
        var current = document.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";

        var path = 定義ファイル();
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, current);
        }

        Assert.True(File.Exists(path), $"{path} がありません。UPDATE_OPENAPI=1 で生成してください。");
        var committed = (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");
        Assert.True(committed == current,
            "docs/api/openapi.json がコードと一致しません。UPDATE_OPENAPI=1 dotnet test --filter OpenApi で更新し、" +
            "cd frontend && npm run gen:api でフロントの型も作り直してください。");
    }

    [Fact]
    public async Task OpenAPI定義は全エンドポイントと応答の型を含む()
    {
        using var api = new ApiFixture();
        var document = JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json"))!;
        var paths = document["paths"]!.AsObject();

        var operations = paths.Sum(p => p.Value!.AsObject().Count);
        Assert.Equal(28, operations); // README の API 概要と同じ 28 操作

        // 作成系(201)にも応答の型が付いている
        var created = document["paths"]!["/api/divisions"]!["post"]!["responses"]!["201"]!;
        Assert.Contains("DivisionDto", created.ToJsonString());
        // エラー応答の型
        Assert.NotNull(document["components"]!["schemas"]!["ErrorResponse"]);
    }
}

using Microsoft.AspNetCore.Mvc.Testing;

namespace CostManagement.E2E.Tests.Support;

/// <summary>
/// 実際の WebApi(ルーティング・JSON変換・例外→HTTP変換・DB永続化)を
/// テストのプロセス内でまるごと起動する。テストごとに独立した一時 SQLite データベースを使う。
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

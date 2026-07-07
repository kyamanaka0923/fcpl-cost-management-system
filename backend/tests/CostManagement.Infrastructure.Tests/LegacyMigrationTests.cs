using CostManagement.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Tests;

/// <summary>
/// 旧スキーマ(数量×単価で明細管理していた世代)からの自動移行の検証。
/// 金額 = 数量 × 単価 で引き継がれ、売上対応品目は未設定(共通費)になる。
/// </summary>
public class 旧スキーマからの自動移行 : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"cm-legacy-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>旧世代のスキーマとデータを直接 SQL で作成する。</summary>
    private void 旧スキーマのデータベースを作成()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        conn.Execute("""
            CREATE TABLE projects (
                id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
                fiscal_year INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL
            );
            CREATE TABLE cost_elements (
                code TEXT PRIMARY KEY, name TEXT NOT NULL, element_type TEXT NOT NULL,
                is_quantity_managed INTEGER NOT NULL
            );
            CREATE TABLE cost_plans (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL, version INTEGER NOT NULL,
                label TEXT NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL,
                approved_at TEXT NULL, UNIQUE (project_id, version)
            );
            CREATE TABLE cost_plan_lines (
                id TEXT PRIMARY KEY, plan_id TEXT NOT NULL, element_code TEXT NOT NULL,
                period TEXT NOT NULL, quantity TEXT NOT NULL, unit_price TEXT NOT NULL
            );
            CREATE TABLE actual_costs (
                id TEXT PRIMARY KEY, project_id TEXT NOT NULL, element_code TEXT NOT NULL,
                period TEXT NOT NULL, quantity TEXT NOT NULL, unit_price TEXT NOT NULL,
                note TEXT NULL, recorded_at TEXT NOT NULL
            );

            INSERT INTO projects VALUES
                ('11111111-1111-1111-1111-111111111111', 'PJ-OLD', '旧プロジェクト',
                 2026, 'Active', '2026-04-01T00:00:00.0000000Z');
            INSERT INTO cost_elements VALUES ('MAT-RAW', '原材料費', 'Material', 1);
            INSERT INTO cost_plans VALUES
                ('22222222-2222-2222-2222-222222222222',
                 '11111111-1111-1111-1111-111111111111', 1, '当初予算', 'Approved',
                 '2026-04-01T00:00:00.0000000Z', '2026-04-01T00:00:00.0000000Z');
            -- 数量 100 × 単価 500 = 50,000 円
            INSERT INTO cost_plan_lines VALUES
                ('33333333-3333-3333-3333-333333333333',
                 '22222222-2222-2222-2222-222222222222', 'MAT-RAW', '2026-04', '100', '500');
            -- 数量 60 × 単価 520 = 31,200 円
            INSERT INTO actual_costs VALUES
                ('44444444-4444-4444-4444-444444444444',
                 '11111111-1111-1111-1111-111111111111', 'MAT-RAW', '2026-04', '60', '520',
                 '前半分', '2026-04-30T00:00:00.0000000Z');
            """);
    }

    [Fact]
    public async Task 旧スキーマの予算明細は数量x単価の金額で引き継がれる()
    {
        旧スキーマのデータベースを作成();

        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(factory).Initialize(); // 起動時と同じ移行処理

        var repo = new Repositories.CostPlanRepository(factory);
        var plan = await repo.FindByIdAsync(
            new Domain.Planning.CostPlanId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        Assert.NotNull(plan);
        var line = Assert.Single(plan.Lines);
        Assert.Equal(50_000m, line.Amount.Value); // 100 × 500
        Assert.Null(line.RevenueItem); // 売上対応品目は未設定(共通費)扱い
    }

    [Fact]
    public async Task 旧スキーマの実績は金額換算され摘要も保持される()
    {
        旧スキーマのデータベースを作成();

        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(factory).Initialize();

        var repo = new Repositories.ActualCostRepository(factory);
        var actuals = await repo.ListByProjectAsync(
            new Domain.Projects.ProjectId(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        var actual = Assert.Single(actuals);
        Assert.Equal(31_200m, actual.Amount.Value); // 60 × 520
        Assert.Equal("前半分", actual.Note);
    }

    [Fact]
    public void 移行は冪等であり2回実行しても壊れない()
    {
        旧スキーマのデータベースを作成();
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");

        new DatabaseInitializer(factory).Initialize();
        new DatabaseInitializer(factory).Initialize(); // 2回目(通常起動と同じ)

        using var conn = factory.Create();
        var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM cost_plan_lines");
        Assert.Equal(1, count);
    }

    [Fact]
    public void 新規データベースには移行なしで新スキーマが作成される()
    {
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(factory).Initialize();

        using var conn = factory.Create();
        var hasAmount = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info('cost_plan_lines') WHERE name = 'amount'");
        var hasQuantity = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info('cost_plan_lines') WHERE name = 'quantity'");
        Assert.Equal(1, hasAmount);
        Assert.Equal(0, hasQuantity);
    }
}

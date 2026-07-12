using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Tests;

/// <summary>
/// 旧世代(プロジェクト単位予算)のスキーマが残っているDBに対して、
/// DatabaseInitializer が旧テーブルを破棄し新スキーマで作り直すことを検証する。
/// </summary>
public class スキーマの作り直し : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public スキーマの作り直し()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-schema-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void 旧スキーマのDBを作成()
    {
        using var conn = _factory.Create();
        conn.Execute("""
            CREATE TABLE projects (
                id          TEXT PRIMARY KEY,
                code        TEXT NOT NULL UNIQUE,
                name        TEXT NOT NULL,
                fiscal_year INTEGER NOT NULL,
                status      TEXT NOT NULL,
                created_at  TEXT NOT NULL
            );

            CREATE TABLE cost_elements (
                code         TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                element_type TEXT NOT NULL
            );

            CREATE TABLE cost_plans (
                id          TEXT PRIMARY KEY,
                project_id  TEXT NOT NULL REFERENCES projects(id),
                version     INTEGER NOT NULL,
                label       TEXT NOT NULL,
                status      TEXT NOT NULL,
                created_at  TEXT NOT NULL,
                approved_at TEXT NULL
            );

            CREATE TABLE cost_plan_lines (
                id           TEXT PRIMARY KEY,
                plan_id      TEXT NOT NULL REFERENCES cost_plans(id) ON DELETE CASCADE,
                element_code TEXT NOT NULL,
                revenue_item TEXT NOT NULL DEFAULT '',
                period       TEXT NOT NULL,
                amount       TEXT NOT NULL
            );

            CREATE TABLE actual_costs (id TEXT PRIMARY KEY, project_id TEXT NOT NULL);
            CREATE TABLE revenue_plans (id TEXT PRIMARY KEY, project_id TEXT NOT NULL);
            CREATE TABLE revenue_plan_lines (id TEXT PRIMARY KEY, plan_id TEXT NOT NULL);
            CREATE TABLE actual_revenues (id TEXT PRIMARY KEY, project_id TEXT NOT NULL);

            INSERT INTO projects VALUES ('p1', 'PJ-001', '旧プロジェクト', 2026, 'Active', '2026-04-01');
            INSERT INTO cost_elements VALUES ('LAB-SE', 'SE人件費', 'Labor');
            """);
    }

    private long テーブル数(string name)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @Name",
            new { Name = name });
    }

    private bool カラムが存在する(string table, string column)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column",
            new { Table = table, Column = column }) > 0;
    }

    [Fact]
    public void 旧スキーマのDBは旧テーブルが破棄され新スキーマになる()
    {
        旧スキーマのDBを作成();

        new DatabaseInitializer(_factory).Initialize();

        // 旧世代専用テーブルは消える
        Assert.Equal(0, テーブル数("cost_plans"));
        Assert.Equal(0, テーブル数("cost_plan_lines"));
        Assert.Equal(0, テーブル数("actual_costs"));
        Assert.Equal(0, テーブル数("revenue_plans"));
        Assert.Equal(0, テーブル数("revenue_plan_lines"));
        Assert.Equal(0, テーブル数("actual_revenues"));

        // 同名テーブルは新構造で作り直される(旧データは引き継がない)
        Assert.True(カラムが存在する("projects", "department_id"));
        Assert.False(カラムが存在する("projects", "fiscal_year"));
        Assert.False(カラムが存在する("cost_elements", "element_type"));

        // 新スキーマのテーブルが揃う
        Assert.Equal(1, テーブル数("divisions"));
        Assert.Equal(1, テーブル数("division_budget_approvals"));
        Assert.Equal(1, テーブル数("departments"));
        Assert.Equal(1, テーブル数("department_budgets"));
        Assert.Equal(1, テーブル数("department_budget_lines"));
        Assert.Equal(1, テーブル数("actual_entries"));

        // 費目は新シード(2件)に置き換わる
        using var conn = _factory.Create();
        var elements = conn.Query<(string Code, string Name)>(
            "SELECT code, name FROM cost_elements ORDER BY code").ToList();
        Assert.Equal(2, elements.Count);
        Assert.Contains(("LICENSE", "ライセンス費"), elements);
        Assert.Contains(("PERSONNEL", "人件費"), elements);
    }

    [Fact]
    public void 初期化は冪等で2回実行しても壊れない()
    {
        旧スキーマのDBを作成();

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
        initializer.Initialize(); // 2回目も例外なく完走する

        Assert.Equal(1, テーブル数("departments"));
        Assert.True(カラムが存在する("projects", "department_id"));
    }

    [Fact]
    public void 新規DBは移行処理なしで新スキーマが作成される()
    {
        new DatabaseInitializer(_factory).Initialize();

        Assert.Equal(1, テーブル数("divisions"));
        Assert.Equal(1, テーブル数("division_budget_approvals"));
        Assert.Equal(1, テーブル数("departments"));
        Assert.Equal(1, テーブル数("department_budgets"));
        Assert.True(カラムが存在する("departments", "division_id"));
        Assert.True(カラムが存在する("projects", "department_id"));
    }

    [Fact]
    public void 新スキーマのデータは再初期化しても保持される()
    {
        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();

        using (var conn = _factory.Create())
        {
            conn.Execute("""
                INSERT INTO divisions VALUES ('v1', 'SALES', '営業本部', '2026-04-01');
                INSERT INTO departments VALUES ('d1', 'v1', 'DEV-1', '開発1課', '2026-04-01');
                """);
        }

        initializer.Initialize(); // 新スキーマの既存データは破棄されない

        using var check = _factory.Create();
        Assert.Equal(1, check.ExecuteScalar<long>("SELECT COUNT(*) FROM departments"));
    }

    [Fact]
    public void 部を持たない課スキーマは部あり新スキーマに作り直される()
    {
        using (var conn = _factory.Create())
        {
            // division_id を持たない旧世代の departments とその配下。
            conn.Execute("""
                CREATE TABLE departments (
                    id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, created_at TEXT NOT NULL
                );
                CREATE TABLE projects (id TEXT PRIMARY KEY, department_id TEXT NOT NULL);
                CREATE TABLE department_budgets (id TEXT PRIMARY KEY, department_id TEXT NOT NULL);
                CREATE TABLE department_budget_lines (id TEXT PRIMARY KEY, budget_id TEXT NOT NULL);
                CREATE TABLE actual_entries (id TEXT PRIMARY KEY, department_id TEXT NOT NULL);
                INSERT INTO departments VALUES ('d1', 'DEV-1', '旧課', '2026-04-01');
                """);
        }
        Assert.False(カラムが存在する("departments", "division_id"));

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();

        Assert.True(カラムが存在する("departments", "division_id"));
        Assert.Equal(1, テーブル数("divisions"));
        using (var conn = _factory.Create())
        {
            // 旧 departments のデータは引き継がず作り直す
            Assert.Equal(0, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM departments"));
        }

        initializer.Initialize(); // 冪等
        Assert.True(カラムが存在する("departments", "division_id"));
    }
}

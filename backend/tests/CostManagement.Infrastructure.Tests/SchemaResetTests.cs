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
        Assert.False(カラムが存在する("projects", "status")); // 終了ステータスは廃止(Issue #2)
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

/// <summary>
/// 案件コードの一意制約を「グローバル一意」→「課ごとに一意」へ変更するマイグレーションの検証。
/// 制約緩和のため既存の案件データは保持したまま作り直される(Issue #1)。
/// </summary>
public class 案件コードの課ごと一意化マイグレーション : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public 案件コードの課ごと一意化マイグレーション()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-projuniq-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>旧制約(code 単独 UNIQUE)の現世代 projects を持つDBを作る。</summary>
    private void 旧一意制約のDBを作成()
    {
        using var conn = _factory.Create();
        conn.Execute("""
            CREATE TABLE divisions (
                id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, created_at TEXT NOT NULL
            );
            CREATE TABLE departments (
                id TEXT PRIMARY KEY, division_id TEXT NOT NULL, code TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL, created_at TEXT NOT NULL
            );
            CREATE TABLE projects (
                id            TEXT PRIMARY KEY,
                department_id TEXT NOT NULL REFERENCES departments(id),
                code          TEXT NOT NULL UNIQUE,
                name          TEXT NOT NULL,
                status        TEXT NOT NULL,
                created_at    TEXT NOT NULL
            );
            INSERT INTO divisions VALUES ('v1', 'SALES', '営業本部', '2026-04-01');
            INSERT INTO departments VALUES
                ('d1', 'v1', 'DEV-1', '開発1課', '2026-04-01'),
                ('d2', 'v1', 'DEV-2', '開発2課', '2026-04-01');
            INSERT INTO projects VALUES ('p1', 'd1', 'PJ-001', '課1の案件', 'Active', '2026-04-01');
            """);
    }

    [Fact]
    public void 旧一意制約のDBは案件データを保持したまま課ごと一意に作り直される()
    {
        旧一意制約のDBを作成();

        new DatabaseInitializer(_factory).Initialize();

        using var conn = _factory.Create();
        // 既存の案件は保持される
        var name = conn.ExecuteScalar<string>("SELECT name FROM projects WHERE id = 'p1'");
        Assert.Equal("課1の案件", name);

        // 別の課で同じコードを登録できる(以前はグローバル UNIQUE で不可だった)
        conn.Execute(
            "INSERT INTO projects VALUES ('p2', 'd2', 'PJ-001', '課2の案件', '2026-04-01')");
        Assert.Equal(2, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM projects WHERE code = 'PJ-001'"));
    }

    [Fact]
    public void 同一課の案件コード重複は移行後も許されない()
    {
        旧一意制約のDBを作成();
        new DatabaseInitializer(_factory).Initialize();

        using var conn = _factory.Create();
        var ex = Assert.ThrowsAny<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(
                "INSERT INTO projects VALUES ('p3', 'd1', 'PJ-001', '課1の別案件', '2026-04-01')"));
        Assert.Contains("UNIQUE", ex.Message);
    }

    [Fact]
    public void 移行は冪等で2回実行しても壊れない()
    {
        旧一意制約のDBを作成();

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
        initializer.Initialize(); // 2回目は移行済みのため何もしない

        using var conn = _factory.Create();
        Assert.Equal(1, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM projects"));
        // 一時テーブルが残っていない
        Assert.Equal(0, conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE name = 'projects_pre_dept_unique'"));
    }
}

/// <summary>
/// 案件の終了ステータス(status 列)を廃止するマイグレーションの検証(Issue #2)。
/// 課ごと一意へ移行済みで status 列を持つ現世代 projects が、案件データを保持したまま
/// status 列なしに作り直されることを確認する。
/// </summary>
public class 案件の終了ステータス廃止マイグレーション : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public 案件の終了ステータス廃止マイグレーション()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-projstatus-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>課ごと一意へ移行済みで、まだ status 列を持つ現世代 projects のDBを作る。</summary>
    private void 終了ステータスありのDBを作成()
    {
        using var conn = _factory.Create();
        conn.Execute("""
            CREATE TABLE divisions (
                id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, created_at TEXT NOT NULL
            );
            CREATE TABLE departments (
                id TEXT PRIMARY KEY, division_id TEXT NOT NULL, code TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL, created_at TEXT NOT NULL
            );
            CREATE TABLE projects (
                id            TEXT PRIMARY KEY,
                department_id TEXT NOT NULL REFERENCES departments(id),
                code          TEXT NOT NULL,
                name          TEXT NOT NULL,
                status        TEXT NOT NULL,
                created_at    TEXT NOT NULL,
                UNIQUE (department_id, code)
            );
            INSERT INTO divisions VALUES ('v1', 'SALES', '営業本部', '2026-04-01');
            INSERT INTO departments VALUES ('d1', 'v1', 'DEV-1', '開発1課', '2026-04-01');
            INSERT INTO projects VALUES ('p1', 'd1', 'PJ-001', '課1の案件', 'Completed', '2026-04-01');
            """);
    }

    private bool カラムが存在する(string table, string column)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column",
            new { Table = table, Column = column }) > 0;
    }

    [Fact]
    public void status列を持つDBは案件データを保持したままstatus列なしに作り直される()
    {
        終了ステータスありのDBを作成();
        Assert.True(カラムが存在する("projects", "status"));

        new DatabaseInitializer(_factory).Initialize();

        // status 列は消える
        Assert.False(カラムが存在する("projects", "status"));
        Assert.True(カラムが存在する("projects", "department_id"));

        using var conn = _factory.Create();
        // 既存の案件(id/コード/名称/作成日時)は保持される
        var name = conn.ExecuteScalar<string>("SELECT name FROM projects WHERE id = 'p1'");
        Assert.Equal("課1の案件", name);
        // 課ごと一意制約も維持される
        var ex = Assert.ThrowsAny<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(
                "INSERT INTO projects VALUES ('p2', 'd1', 'PJ-001', '課1の別案件', '2026-04-01')"));
        Assert.Contains("UNIQUE", ex.Message);
    }

    [Fact]
    public void 移行は冪等で2回実行しても壊れない()
    {
        終了ステータスありのDBを作成();

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
        initializer.Initialize(); // 2回目は移行済みのため何もしない

        using var conn = _factory.Create();
        Assert.Equal(1, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM projects"));
        Assert.False(カラムが存在する("projects", "status"));
        // 一時テーブルが残っていない
        Assert.Equal(0, conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE name = 'projects_pre_drop_status'"));
    }
}

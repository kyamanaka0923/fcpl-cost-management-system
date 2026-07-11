using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Persistence;

/// <summary>
/// SQLite のスキーマを初期化する。アプリ起動時に一度呼び出す。
/// 旧世代(プロジェクト単位予算)のテーブルが残っている場合は破棄し、
/// 課×半期予算の新スキーマで作り直す(データ移行は行わない)。
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _factory;

    public DatabaseInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Initialize()
    {
        using var connection = _factory.Create();

        DropLegacyTables(connection);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS departments (
                id         TEXT PRIMARY KEY,
                code       TEXT NOT NULL UNIQUE,
                name       TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
                id            TEXT PRIMARY KEY,
                department_id TEXT NOT NULL REFERENCES departments(id),
                code          TEXT NOT NULL UNIQUE,
                name          TEXT NOT NULL,
                status        TEXT NOT NULL,
                created_at    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cost_elements (
                code TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );

            -- fiscal_half は "2026-H1" 形式(FiscalHalf.ToString)。
            CREATE TABLE IF NOT EXISTS department_budgets (
                id            TEXT PRIMARY KEY,
                department_id TEXT NOT NULL REFERENCES departments(id),
                fiscal_half   TEXT NOT NULL,
                version       INTEGER NOT NULL,
                label         TEXT NOT NULL,
                status        TEXT NOT NULL,
                created_at    TEXT NOT NULL,
                approved_at   TEXT NULL,
                UNIQUE (department_id, fiscal_half, version)
            );

            -- project_id / element_code は区分により排他。未使用側は '' で保存する
            -- (NULL は SQLite の UNIQUE 制約で重複可となるため)。
            CREATE TABLE IF NOT EXISTS department_budget_lines (
                id           TEXT PRIMARY KEY,
                budget_id    TEXT NOT NULL REFERENCES department_budgets(id) ON DELETE CASCADE,
                category     TEXT NOT NULL,
                project_id   TEXT NOT NULL DEFAULT '',
                element_code TEXT NOT NULL DEFAULT '',
                amount       TEXT NOT NULL,
                UNIQUE (budget_id, category, project_id, element_code)
            );

            CREATE TABLE IF NOT EXISTS actual_entries (
                id            TEXT PRIMARY KEY,
                department_id TEXT NOT NULL REFERENCES departments(id),
                fiscal_half   TEXT NOT NULL,
                category      TEXT NOT NULL,
                project_id    TEXT NOT NULL DEFAULT '',
                element_code  TEXT NOT NULL DEFAULT '',
                amount        TEXT NOT NULL,
                note          TEXT NULL,
                recorded_at   TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_projects_department ON projects(department_id);
            CREATE INDEX IF NOT EXISTS ix_budgets_dept_half
                ON department_budgets(department_id, fiscal_half);
            CREATE INDEX IF NOT EXISTS ix_budget_lines_budget ON department_budget_lines(budget_id);
            CREATE INDEX IF NOT EXISTS ix_actual_entries_dept_half
                ON actual_entries(department_id, fiscal_half);
            """);

        // 期間費用の標準費目をシードする(既存があれば何もしない)。
        connection.Execute("""
            INSERT OR IGNORE INTO cost_elements (code, name) VALUES
                ('PERSONNEL', '人件費'),
                ('LICENSE',   'ライセンス費');
            """);
    }

    /// <summary>
    /// 旧世代(プロジェクト単位予算)のテーブルを破棄する。冪等(何度実行しても安全)。
    /// 同名で構造が変わる projects / cost_elements は旧世代のカラム有無で判定する。
    /// </summary>
    private static void DropLegacyTables(SqliteConnection connection)
    {
        bool HasColumn(string table, string column) =>
            connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column",
                new { Table = table, Column = column }) > 0;

        using var tx = connection.BeginTransaction();

        // 旧世代専用のテーブルは無条件に破棄する(子 → 親の順)。
        connection.Execute("""
            DROP TABLE IF EXISTS cost_plan_lines;
            DROP TABLE IF EXISTS cost_plan_lines_legacy;
            DROP TABLE IF EXISTS cost_plans;
            DROP TABLE IF EXISTS actual_costs;
            DROP TABLE IF EXISTS actual_costs_legacy;
            DROP TABLE IF EXISTS revenue_plan_lines;
            DROP TABLE IF EXISTS revenue_plan_lines_legacy;
            DROP TABLE IF EXISTS revenue_plans;
            DROP TABLE IF EXISTS actual_revenues;
            DROP TABLE IF EXISTS actual_revenues_legacy;
            """, transaction: tx);

        // 旧: 予算策定単位のプロジェクト(fiscal_year 列を持つ)
        if (HasColumn("projects", "fiscal_year"))
            connection.Execute("DROP TABLE projects;", transaction: tx);

        // 旧: 原価要素分類つきの費目マスタ(element_type 列を持つ)
        if (HasColumn("cost_elements", "element_type"))
            connection.Execute("DROP TABLE cost_elements;", transaction: tx);

        tx.Commit();
    }
}

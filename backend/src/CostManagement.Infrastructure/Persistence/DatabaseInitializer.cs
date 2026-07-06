using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Persistence;

/// <summary>スキーマ作成・旧スキーマからの移行・費目マスタの初期投入を行う。</summary>
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

        MigrateLegacyQuantitySchema(connection);

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS projects (
                id          TEXT PRIMARY KEY,
                code        TEXT NOT NULL UNIQUE,
                name        TEXT NOT NULL,
                fiscal_year INTEGER NOT NULL,
                status      TEXT NOT NULL,
                created_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cost_elements (
                code         TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                element_type TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cost_plans (
                id          TEXT PRIMARY KEY,
                project_id  TEXT NOT NULL REFERENCES projects(id),
                version     INTEGER NOT NULL,
                label       TEXT NOT NULL,
                status      TEXT NOT NULL,
                created_at  TEXT NOT NULL,
                approved_at TEXT NULL,
                UNIQUE (project_id, version)
            );

            -- revenue_item は売上対応品目('' = 共通費)。
            CREATE TABLE IF NOT EXISTS cost_plan_lines (
                id           TEXT PRIMARY KEY,
                plan_id      TEXT NOT NULL REFERENCES cost_plans(id) ON DELETE CASCADE,
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                revenue_item TEXT NOT NULL DEFAULT '',
                period       TEXT NOT NULL,
                amount       TEXT NOT NULL,
                UNIQUE (plan_id, element_code, revenue_item, period)
            );

            CREATE TABLE IF NOT EXISTS actual_costs (
                id           TEXT PRIMARY KEY,
                project_id   TEXT NOT NULL REFERENCES projects(id),
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                revenue_item TEXT NOT NULL DEFAULT '',
                period       TEXT NOT NULL,
                amount       TEXT NOT NULL,
                note         TEXT NULL,
                recorded_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS revenue_plans (
                id          TEXT PRIMARY KEY,
                project_id  TEXT NOT NULL REFERENCES projects(id),
                version     INTEGER NOT NULL,
                label       TEXT NOT NULL,
                status      TEXT NOT NULL,
                created_at  TEXT NOT NULL,
                approved_at TEXT NULL,
                UNIQUE (project_id, version)
            );

            CREATE TABLE IF NOT EXISTS revenue_plan_lines (
                id        TEXT PRIMARY KEY,
                plan_id   TEXT NOT NULL REFERENCES revenue_plans(id) ON DELETE CASCADE,
                item_name TEXT NOT NULL,
                period    TEXT NOT NULL,
                amount    TEXT NOT NULL,
                UNIQUE (plan_id, item_name, period)
            );

            CREATE TABLE IF NOT EXISTS actual_revenues (
                id          TEXT PRIMARY KEY,
                project_id  TEXT NOT NULL REFERENCES projects(id),
                item_name   TEXT NOT NULL,
                period      TEXT NOT NULL,
                amount      TEXT NOT NULL,
                note        TEXT NULL,
                recorded_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_cost_plans_project ON cost_plans(project_id);
            CREATE INDEX IF NOT EXISTS ix_actual_costs_project ON actual_costs(project_id);
            CREATE INDEX IF NOT EXISTS ix_revenue_plans_project ON revenue_plans(project_id);
            CREATE INDEX IF NOT EXISTS ix_actual_revenues_project ON actual_revenues(project_id);
            """);

        // SE費用管理を想定した標準費目をシードする(既存があれば何もしない)。
        connection.Execute("""
            INSERT OR IGNORE INTO cost_elements (code, name, element_type) VALUES
                ('LAB-SE',  'SE人件費',       'Labor'),
                ('LAB-PM',  'PM人件費',       'Labor'),
                ('SUB-DEV', '外注開発費',     'Expense'),
                ('LIC-SW',  'ライセンス費',   'Expense'),
                ('HW-EQP',  '機器・材料費',   'Material'),
                ('OVH-COM', '共通間接費',     'Overhead'),
                ('EXP-TRV', '旅費交通費',     'Expense'),
                ('EXP-OTH', 'その他経費',     'Expense');
            """);
    }

    /// <summary>
    /// 旧スキーマ(数量×単価で管理していた世代)からの移行。
    /// 明細の金額は quantity × unit_price で引き継ぎ、売上対応品目は未設定(共通費)とする。
    /// </summary>
    private static void MigrateLegacyQuantitySchema(SqliteConnection connection)
    {
        bool HasColumn(string table, string column) =>
            connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column",
                new { Table = table, Column = column }) > 0;

        if (!HasColumn("cost_plan_lines", "quantity"))
            return;

        using var tx = connection.BeginTransaction();

        connection.Execute("""
            ALTER TABLE cost_plan_lines RENAME TO cost_plan_lines_legacy;

            CREATE TABLE cost_plan_lines (
                id           TEXT PRIMARY KEY,
                plan_id      TEXT NOT NULL REFERENCES cost_plans(id) ON DELETE CASCADE,
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                revenue_item TEXT NOT NULL DEFAULT '',
                period       TEXT NOT NULL,
                amount       TEXT NOT NULL,
                UNIQUE (plan_id, element_code, revenue_item, period)
            );

            INSERT INTO cost_plan_lines (id, plan_id, element_code, revenue_item, period, amount)
            SELECT id, plan_id, element_code, '', period,
                   CAST(CAST(quantity AS REAL) * CAST(unit_price AS REAL) AS TEXT)
            FROM cost_plan_lines_legacy;

            DROP TABLE cost_plan_lines_legacy;
            """, transaction: tx);

        connection.Execute("""
            ALTER TABLE actual_costs RENAME TO actual_costs_legacy;

            CREATE TABLE actual_costs (
                id           TEXT PRIMARY KEY,
                project_id   TEXT NOT NULL REFERENCES projects(id),
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                revenue_item TEXT NOT NULL DEFAULT '',
                period       TEXT NOT NULL,
                amount       TEXT NOT NULL,
                note         TEXT NULL,
                recorded_at  TEXT NOT NULL
            );

            INSERT INTO actual_costs (id, project_id, element_code, revenue_item, period, amount, note, recorded_at)
            SELECT id, project_id, element_code, '', period,
                   CAST(CAST(quantity AS REAL) * CAST(unit_price AS REAL) AS TEXT),
                   note, recorded_at
            FROM actual_costs_legacy;

            DROP TABLE actual_costs_legacy;
            """, transaction: tx);

        if (HasColumn("revenue_plan_lines", "quantity"))
        {
            connection.Execute("""
                ALTER TABLE revenue_plan_lines RENAME TO revenue_plan_lines_legacy;

                CREATE TABLE revenue_plan_lines (
                    id        TEXT PRIMARY KEY,
                    plan_id   TEXT NOT NULL REFERENCES revenue_plans(id) ON DELETE CASCADE,
                    item_name TEXT NOT NULL,
                    period    TEXT NOT NULL,
                    amount    TEXT NOT NULL,
                    UNIQUE (plan_id, item_name, period)
                );

                INSERT INTO revenue_plan_lines (id, plan_id, item_name, period, amount)
                SELECT id, plan_id, item_name, period,
                       CAST(CAST(quantity AS REAL) * CAST(unit_price AS REAL) AS TEXT)
                FROM revenue_plan_lines_legacy;

                DROP TABLE revenue_plan_lines_legacy;
                """, transaction: tx);
        }

        if (HasColumn("actual_revenues", "quantity"))
        {
            connection.Execute("""
                ALTER TABLE actual_revenues RENAME TO actual_revenues_legacy;

                CREATE TABLE actual_revenues (
                    id          TEXT PRIMARY KEY,
                    project_id  TEXT NOT NULL REFERENCES projects(id),
                    item_name   TEXT NOT NULL,
                    period      TEXT NOT NULL,
                    amount      TEXT NOT NULL,
                    note        TEXT NULL,
                    recorded_at TEXT NOT NULL
                );

                INSERT INTO actual_revenues (id, project_id, item_name, period, amount, note, recorded_at)
                SELECT id, project_id, item_name, period,
                       CAST(CAST(quantity AS REAL) * CAST(unit_price AS REAL) AS TEXT),
                       note, recorded_at
                FROM actual_revenues_legacy;

                DROP TABLE actual_revenues_legacy;
                """, transaction: tx);
        }

        if (HasColumn("cost_elements", "is_quantity_managed"))
        {
            connection.Execute("""
                ALTER TABLE cost_elements DROP COLUMN is_quantity_managed;
                """, transaction: tx);
        }

        tx.Commit();
    }
}

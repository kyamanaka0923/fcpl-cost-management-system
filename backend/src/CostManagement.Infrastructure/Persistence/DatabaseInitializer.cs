using Dapper;

namespace CostManagement.Infrastructure.Persistence;

/// <summary>スキーマ作成と費目マスタの初期投入を行う。</summary>
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
                code                TEXT PRIMARY KEY,
                name                TEXT NOT NULL,
                element_type        TEXT NOT NULL,
                is_quantity_managed INTEGER NOT NULL
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

            CREATE TABLE IF NOT EXISTS cost_plan_lines (
                id           TEXT PRIMARY KEY,
                plan_id      TEXT NOT NULL REFERENCES cost_plans(id) ON DELETE CASCADE,
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                period       TEXT NOT NULL,
                quantity     TEXT NOT NULL,
                unit_price   TEXT NOT NULL,
                UNIQUE (plan_id, element_code, period)
            );

            CREATE TABLE IF NOT EXISTS actual_costs (
                id           TEXT PRIMARY KEY,
                project_id   TEXT NOT NULL REFERENCES projects(id),
                element_code TEXT NOT NULL REFERENCES cost_elements(code),
                period       TEXT NOT NULL,
                quantity     TEXT NOT NULL,
                unit_price   TEXT NOT NULL,
                note         TEXT NULL,
                recorded_at  TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_cost_plans_project ON cost_plans(project_id);
            CREATE INDEX IF NOT EXISTS ix_actual_costs_project ON actual_costs(project_id);
            """);

        // 総合原価計算の標準的な費目をシードする(既存があれば何もしない)。
        connection.Execute("""
            INSERT OR IGNORE INTO cost_elements (code, name, element_type, is_quantity_managed) VALUES
                ('MAT-RAW',  '原材料費',   'Material', 1),
                ('MAT-SUB',  '補助材料費', 'Material', 1),
                ('LAB-DIR',  '直接労務費', 'Labor',    1),
                ('LAB-IND',  '間接労務費', 'Labor',    1),
                ('OVH-MFG',  '製造間接費', 'Overhead', 0),
                ('EXP-SUB',  '外注加工費', 'Expense',  1),
                ('EXP-OTH',  'その他経費', 'Expense',  0);
            """);
    }
}

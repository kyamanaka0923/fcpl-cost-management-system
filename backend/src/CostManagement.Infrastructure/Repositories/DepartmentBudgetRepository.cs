using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>
/// 課予算の永続化ポート <see cref="IDepartmentBudgetRepository"/> の Dapper/SQLite 実装。
/// ヘッダ(department_budgets)・明細(department_budget_lines)・明細の月別金額
/// (department_budget_line_months)をまとめて1つの集約として扱う。
/// </summary>
public sealed class DepartmentBudgetRepository : IDepartmentBudgetRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public DepartmentBudgetRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>department_budgets(ヘッダ)テーブルの1行に対応する DTO。</summary>
    private sealed record BudgetRow(Guid Id, Guid DepartmentId, string FiscalHalf, long Version,
        string Label, string Status, DateTime CreatedAt, DateTime? ApprovedAt);

    /// <summary>department_budget_lines(明細)テーブルの1行に対応する DTO。</summary>
    private sealed record LineRow(Guid Id, Guid BudgetId, string Category, string ProjectId,
        string ElementCode, decimal Amount);

    /// <summary>department_budget_line_months(明細の月別金額)テーブルの1行に対応する DTO。
    /// SQLite の INTEGER は Int64 で返るため month は long で受ける。</summary>
    private sealed record MonthRow(Guid LineId, long Month, decimal Amount);

    private const string SelectBudgetSql = """
        SELECT id AS Id, department_id AS DepartmentId, fiscal_half AS FiscalHalf,
               version AS Version, label AS Label, status AS Status,
               created_at AS CreatedAt, approved_at AS ApprovedAt
        FROM department_budgets
        """;

    private const string SelectLineSql = """
        SELECT id AS Id, budget_id AS BudgetId, category AS Category,
               project_id AS ProjectId, element_code AS ElementCode, amount AS Amount
        FROM department_budget_lines
        """;

    /// <inheritdoc />
    public async Task<DepartmentBudget?> FindByIdAsync(DepartmentBudgetId id,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var budget = await conn.QuerySingleOrDefaultAsync<BudgetRow>(
            $"{SelectBudgetSql} WHERE id = @Id", new { Id = id.Value });
        if (budget is null)
            return null;
        var lines = await conn.QueryAsync<LineRow>(
            $"{SelectLineSql} WHERE budget_id = @Id", new { Id = id.Value });
        var months = await MonthsByLineAsync(conn, id.Value);
        return ToEntity(budget, lines, months);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentBudget>> ListAsync(DepartmentId departmentId,
        FiscalHalf fiscalHalf, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var arg = new { Did = departmentId.Value, Half = fiscalHalf.ToString() };
        var budgets = (await conn.QueryAsync<BudgetRow>(
            $"{SelectBudgetSql} WHERE department_id = @Did AND fiscal_half = @Half ORDER BY version",
            arg)).ToList();
        // 明細・月別金額は JOIN で一括取得し、予算ID・明細IDでルックアップして N+1 を避ける。
        var lines = (await conn.QueryAsync<LineRow>("""
            SELECT l.id AS Id, l.budget_id AS BudgetId, l.category AS Category,
                   l.project_id AS ProjectId, l.element_code AS ElementCode, l.amount AS Amount
            FROM department_budget_lines l
            JOIN department_budgets b ON b.id = l.budget_id
            WHERE b.department_id = @Did AND b.fiscal_half = @Half
            """, arg)).ToLookup(l => l.BudgetId);
        var months = (await conn.QueryAsync<MonthRow>("""
            SELECT m.line_id AS LineId, m.month AS Month, m.amount AS Amount
            FROM department_budget_line_months m
            JOIN department_budget_lines l ON l.id = m.line_id
            JOIN department_budgets b ON b.id = l.budget_id
            WHERE b.department_id = @Did AND b.fiscal_half = @Half
            """, arg)).ToLookup(m => m.LineId);
        return budgets.Select(b => ToEntity(b, lines[b.Id], months)).ToList();
    }

    /// <inheritdoc />
    public async Task<DepartmentBudget?> FindLatestApprovedAsync(DepartmentId departmentId,
        FiscalHalf fiscalHalf, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var budget = await conn.QuerySingleOrDefaultAsync<BudgetRow>(
            $"{SelectBudgetSql} WHERE department_id = @Did AND fiscal_half = @Half AND status = 'Approved' ORDER BY version DESC LIMIT 1",
            new { Did = departmentId.Value, Half = fiscalHalf.ToString() });
        if (budget is null)
            return null;
        var lines = await conn.QueryAsync<LineRow>(
            $"{SelectLineSql} WHERE budget_id = @Id", new { Id = budget.Id });
        var months = await MonthsByLineAsync(conn, budget.Id);
        return ToEntity(budget, lines, months);
    }

    /// <inheritdoc />
    public async Task<int> GetMaxVersionAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(version), 0) FROM department_budgets WHERE department_id = @Did AND fiscal_half = @Half",
            new { Did = departmentId.Value, Half = fiscalHalf.ToString() });
    }

    /// <inheritdoc />
    public async Task AddAsync(DepartmentBudget budget, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("""
            INSERT INTO department_budgets
                (id, department_id, fiscal_half, version, label, status, created_at, approved_at)
            VALUES (@Id, @DepartmentId, @FiscalHalf, @Version, @Label, @Status, @CreatedAt, @ApprovedAt)
            """, new
        {
            Id = budget.Id.Value,
            DepartmentId = budget.DepartmentId.Value,
            FiscalHalf = budget.FiscalHalf.ToString(),
            budget.Version,
            budget.Label,
            Status = budget.Status.ToString(),
            budget.CreatedAt,
            budget.ApprovedAt,
        }, tx);
        await InsertLinesAsync(conn, tx, budget);
        tx.Commit();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(DepartmentBudget budget, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("""
            UPDATE department_budgets
            SET label = @Label, status = @Status, approved_at = @ApprovedAt
            WHERE id = @Id
            """, new
        {
            Id = budget.Id.Value,
            budget.Label,
            Status = budget.Status.ToString(),
            budget.ApprovedAt,
        }, tx);
        // 明細は洗い替え(集約単位での置き換え)。月別金額は FK の ON DELETE CASCADE で一緒に消える。
        await conn.ExecuteAsync("DELETE FROM department_budget_lines WHERE budget_id = @Id",
            new { Id = budget.Id.Value }, tx);
        await InsertLinesAsync(conn, tx, budget);
        tx.Commit();
    }

    /// <summary>予算IDに紐づく明細の月別金額を、明細IDでルックアップできる形で取得する。</summary>
    private static async Task<ILookup<Guid, MonthRow>> MonthsByLineAsync(SqliteConnection conn,
        Guid budgetId)
    {
        var rows = await conn.QueryAsync<MonthRow>("""
            SELECT m.line_id AS LineId, m.month AS Month, m.amount AS Amount
            FROM department_budget_line_months m
            JOIN department_budget_lines l ON l.id = m.line_id
            WHERE l.budget_id = @Id
            """, new { Id = budgetId });
        return rows.ToLookup(m => m.LineId);
    }

    /// <summary>予算の全明細を INSERT し、月次モードの明細は月別金額も INSERT する。</summary>
    private static async Task InsertLinesAsync(SqliteConnection conn, SqliteTransaction tx,
        DepartmentBudget budget)
    {
        foreach (var line in budget.Lines)
        {
            await conn.ExecuteAsync("""
                INSERT INTO department_budget_lines (id, budget_id, category, project_id, element_code, amount)
                VALUES (@Id, @BudgetId, @Category, @ProjectId, @ElementCode, @Amount)
                """, new
            {
                line.Id,
                BudgetId = budget.Id.Value,
                Category = line.Category.ToString(),
                ProjectId = line.ProjectId?.Value.ToString("D") ?? "",
                ElementCode = line.ElementCode?.Value ?? "",
                Amount = line.Amount.Value,
            }, tx);

            foreach (var (month, amount) in line.MonthlyAmounts)
            {
                await conn.ExecuteAsync("""
                    INSERT INTO department_budget_line_months (line_id, month, amount)
                    VALUES (@LineId, @Month, @Amount)
                    """, new { LineId = line.Id, Month = month, Amount = amount.Value }, tx);
            }
        }
    }

    /// <summary>
    /// ヘッダ行・明細行・月別金額から課予算の集約を復元する。
    /// 空文字の案件/費目は null に、月別金額の有無で半期一括/月次モードを復元する。
    /// </summary>
    private static DepartmentBudget ToEntity(BudgetRow budget, IEnumerable<LineRow> lines,
        ILookup<Guid, MonthRow> monthsByLine) =>
        DepartmentBudget.Restore(budget.Id, budget.DepartmentId, budget.FiscalHalf,
            (int)budget.Version, budget.Label, Enum.Parse<BudgetStatus>(budget.Status),
            budget.CreatedAt, budget.ApprovedAt,
            lines.Select(l => (l.Id, l.Category,
                l.ProjectId == "" ? (Guid?)null : Guid.Parse(l.ProjectId),
                l.ElementCode == "" ? null : l.ElementCode,
                l.Amount,
                (IReadOnlyDictionary<int, decimal>)monthsByLine[l.Id]
                    .ToDictionary(m => (int)m.Month, m => m.Amount))));
}

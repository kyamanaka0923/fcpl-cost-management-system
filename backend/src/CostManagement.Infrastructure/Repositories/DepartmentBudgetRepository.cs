using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Repositories;

public sealed class DepartmentBudgetRepository : IDepartmentBudgetRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DepartmentBudgetRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record BudgetRow(Guid Id, Guid DepartmentId, string FiscalHalf, long Version,
        string Label, string Status, DateTime CreatedAt, DateTime? ApprovedAt);

    private sealed record LineRow(Guid Id, Guid BudgetId, string Category, string ProjectId,
        string ElementCode, decimal Amount);

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
        return ToEntity(budget, lines);
    }

    public async Task<IReadOnlyList<DepartmentBudget>> ListAsync(DepartmentId departmentId,
        FiscalHalf fiscalHalf, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var budgets = (await conn.QueryAsync<BudgetRow>(
            $"{SelectBudgetSql} WHERE department_id = @Did AND fiscal_half = @Half ORDER BY version",
            new { Did = departmentId.Value, Half = fiscalHalf.ToString() })).ToList();
        var lines = (await conn.QueryAsync<LineRow>("""
            SELECT l.id AS Id, l.budget_id AS BudgetId, l.category AS Category,
                   l.project_id AS ProjectId, l.element_code AS ElementCode, l.amount AS Amount
            FROM department_budget_lines l
            JOIN department_budgets b ON b.id = l.budget_id
            WHERE b.department_id = @Did AND b.fiscal_half = @Half
            """, new { Did = departmentId.Value, Half = fiscalHalf.ToString() }))
            .ToLookup(l => l.BudgetId);
        return budgets.Select(b => ToEntity(b, lines[b.Id])).ToList();
    }

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
        return ToEntity(budget, lines);
    }

    public async Task<int> GetMaxVersionAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(version), 0) FROM department_budgets WHERE department_id = @Did AND fiscal_half = @Half",
            new { Did = departmentId.Value, Half = fiscalHalf.ToString() });
    }

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
        // 明細は洗い替え(集約単位での置き換え)とする。
        await conn.ExecuteAsync("DELETE FROM department_budget_lines WHERE budget_id = @Id",
            new { Id = budget.Id.Value }, tx);
        await InsertLinesAsync(conn, tx, budget);
        tx.Commit();
    }

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
        }
    }

    private static DepartmentBudget ToEntity(BudgetRow budget, IEnumerable<LineRow> lines) =>
        DepartmentBudget.Restore(budget.Id, budget.DepartmentId, budget.FiscalHalf,
            (int)budget.Version, budget.Label, Enum.Parse<BudgetStatus>(budget.Status),
            budget.CreatedAt, budget.ApprovedAt,
            lines.Select(l => (l.Id, l.Category,
                l.ProjectId == "" ? (Guid?)null : Guid.Parse(l.ProjectId),
                l.ElementCode == "" ? null : l.ElementCode,
                l.Amount)));
}

using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class DivisionBudgetApprovalRepository : IDivisionBudgetApprovalRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DivisionBudgetApprovalRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, Guid DivisionId, string FiscalHalf, DateTime ApprovedAt);

    private const string SelectSql = """
        SELECT id AS Id, division_id AS DivisionId, fiscal_half AS FiscalHalf, approved_at AS ApprovedAt
        FROM division_budget_approvals
        """;

    public async Task<DivisionBudgetApproval?> FindAsync(DivisionId divisionId, FiscalHalf fiscalHalf,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE division_id = @Div AND fiscal_half = @Half",
            new { Div = divisionId.Value, Half = fiscalHalf.ToString() });
        return row is null ? null : DivisionBudgetApproval.Restore(row.Id, row.DivisionId, row.FiscalHalf, row.ApprovedAt);
    }

    public async Task AddAsync(DivisionBudgetApproval approval, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO division_budget_approvals (id, division_id, fiscal_half, approved_at)
            VALUES (@Id, @DivisionId, @FiscalHalf, @ApprovedAt)
            """, new
        {
            Id = approval.Id.Value,
            DivisionId = approval.DivisionId.Value,
            FiscalHalf = approval.FiscalHalf.ToString(),
            approval.ApprovedAt,
        });
    }

    public async Task DeleteAsync(DivisionId divisionId, FiscalHalf fiscalHalf,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "DELETE FROM division_budget_approvals WHERE division_id = @Div AND fiscal_half = @Half",
            new { Div = divisionId.Value, Half = fiscalHalf.ToString() });
    }
}

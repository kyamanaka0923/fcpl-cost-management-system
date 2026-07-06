using CostManagement.Domain.Actuals;
using CostManagement.Domain.Projects;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class ActualCostRepository : IActualCostRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ActualCostRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, Guid ProjectId, string ElementCode, string RevenueItem,
        string Period, decimal Amount, string? Note, DateTime RecordedAt);

    private const string SelectSql = """
        SELECT id AS Id, project_id AS ProjectId, element_code AS ElementCode,
               revenue_item AS RevenueItem, period AS Period, amount AS Amount,
               note AS Note, recorded_at AS RecordedAt
        FROM actual_costs
        """;

    public async Task<ActualCost?> FindByIdAsync(ActualCostId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<ActualCost>> ListByProjectAsync(ProjectId projectId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE project_id = @Pid ORDER BY period, element_code",
            new { Pid = projectId.Value });
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(ActualCost actual, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO actual_costs (id, project_id, element_code, revenue_item, period, amount, note, recorded_at)
            VALUES (@Id, @ProjectId, @ElementCode, @RevenueItem, @Period, @Amount, @Note, @RecordedAt)
            """, new
        {
            Id = actual.Id.Value,
            ProjectId = actual.ProjectId.Value,
            ElementCode = actual.ElementCode.Value,
            RevenueItem = actual.RevenueItem ?? "",
            Period = actual.Period.ToString(),
            Amount = actual.Amount.Value,
            actual.Note,
            actual.RecordedAt,
        });
    }

    public async Task DeleteAsync(ActualCostId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM actual_costs WHERE id = @Id", new { Id = id.Value });
    }

    private static ActualCost ToEntity(Row row) =>
        ActualCost.Restore(row.Id, row.ProjectId, row.ElementCode,
            row.RevenueItem == "" ? null : row.RevenueItem, row.Period,
            row.Amount, row.Note, row.RecordedAt);
}

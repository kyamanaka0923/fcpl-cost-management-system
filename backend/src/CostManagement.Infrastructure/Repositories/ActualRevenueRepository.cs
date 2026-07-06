using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class ActualRevenueRepository : IActualRevenueRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ActualRevenueRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, Guid ProjectId, string ItemName, string Period,
        decimal Quantity, decimal UnitPrice, string? Note, DateTime RecordedAt);

    private const string SelectSql = """
        SELECT id AS Id, project_id AS ProjectId, item_name AS ItemName, period AS Period,
               quantity AS Quantity, unit_price AS UnitPrice, note AS Note, recorded_at AS RecordedAt
        FROM actual_revenues
        """;

    public async Task<ActualRevenue?> FindByIdAsync(ActualRevenueId id,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<ActualRevenue>> ListByProjectAsync(ProjectId projectId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE project_id = @Pid ORDER BY period, item_name",
            new { Pid = projectId.Value });
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(ActualRevenue actual, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO actual_revenues (id, project_id, item_name, period, quantity, unit_price, note, recorded_at)
            VALUES (@Id, @ProjectId, @ItemName, @Period, @Quantity, @UnitPrice, @Note, @RecordedAt)
            """, new
        {
            Id = actual.Id.Value,
            ProjectId = actual.ProjectId.Value,
            actual.ItemName,
            Period = actual.Period.ToString(),
            actual.Quantity,
            UnitPrice = actual.UnitPrice.Value,
            actual.Note,
            actual.RecordedAt,
        });
    }

    public async Task DeleteAsync(ActualRevenueId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM actual_revenues WHERE id = @Id", new { Id = id.Value });
    }

    private static ActualRevenue ToEntity(Row row) =>
        ActualRevenue.Restore(row.Id, row.ProjectId, row.ItemName, row.Period,
            row.Quantity, row.UnitPrice, row.Note, row.RecordedAt);
}

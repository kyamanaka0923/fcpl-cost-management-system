using CostManagement.Domain.Divisions;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class DivisionRepository : IDivisionRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DivisionRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, string Code, string Name, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, code AS Code, name AS Name, created_at AS CreatedAt
        FROM divisions
        """;

    public async Task<Division?> FindByIdAsync(DivisionId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<Division?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<Division>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY code");
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(Division division, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO divisions (id, code, name, created_at)
            VALUES (@Id, @Code, @Name, @CreatedAt)
            """, new
        {
            Id = division.Id.Value,
            division.Code,
            division.Name,
            division.CreatedAt,
        });
    }

    public async Task UpdateAsync(Division division, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE divisions SET name = @Name WHERE id = @Id
            """, new { Id = division.Id.Value, division.Name });
    }

    private static Division ToEntity(Row row) =>
        Division.Restore(row.Id, row.Code, row.Name, row.CreatedAt);
}

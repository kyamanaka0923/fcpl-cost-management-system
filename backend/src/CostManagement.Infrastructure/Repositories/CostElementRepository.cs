using CostManagement.Domain.CostElements;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class CostElementRepository : ICostElementRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CostElementRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(string Code, string Name, string ElementType);

    private const string SelectSql = """
        SELECT code AS Code, name AS Name, element_type AS ElementType
        FROM cost_elements
        """;

    public async Task<CostElement?> FindByCodeAsync(CostElementCode code,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY element_type, code");
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(CostElement element, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO cost_elements (code, name, element_type)
            VALUES (@Code, @Name, @ElementType)
            """, new
        {
            Code = element.Code.Value,
            element.Name,
            ElementType = element.Type.ToString(),
        });
    }

    private static CostElement ToEntity(Row row) =>
        CostElement.Restore(row.Code, row.Name, Enum.Parse<CostElementType>(row.ElementType));
}

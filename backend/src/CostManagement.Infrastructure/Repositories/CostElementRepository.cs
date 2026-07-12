using CostManagement.Domain.CostElements;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>費目マスタの永続化ポート <see cref="ICostElementRepository"/> の Dapper/SQLite 実装。</summary>
public sealed class CostElementRepository : ICostElementRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public CostElementRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>cost_elements テーブルの1行に対応する DTO。</summary>
    private sealed record Row(string Code, string Name);

    private const string SelectSql = """
        SELECT code AS Code, name AS Name
        FROM cost_elements
        """;

    /// <inheritdoc />
    public async Task<CostElement?> FindByCodeAsync(CostElementCode code,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code.Value });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY code");
        return rows.Select(ToEntity).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(CostElement element, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO cost_elements (code, name) VALUES (@Code, @Name)
            """, new { Code = element.Code.Value, element.Name });
    }

    /// <summary>取得行を費目エンティティへ復元する。</summary>
    private static CostElement ToEntity(Row row) => CostElement.Restore(row.Code, row.Name);
}

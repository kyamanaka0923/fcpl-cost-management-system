using CostManagement.Domain.Divisions;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>部の永続化ポート <see cref="IDivisionRepository"/> の Dapper/SQLite 実装。</summary>
public sealed class DivisionRepository : IDivisionRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public DivisionRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>divisions テーブルの1行に対応する DTO。</summary>
    private sealed record Row(Guid Id, string Code, string Name, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, code AS Code, name AS Name, created_at AS CreatedAt
        FROM divisions
        """;

    /// <inheritdoc />
    public async Task<Division?> FindByIdAsync(DivisionId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<Division?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Division>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY code");
        return rows.Select(ToEntity).ToList();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task UpdateAsync(Division division, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE divisions SET name = @Name WHERE id = @Id
            """, new { Id = division.Id.Value, division.Name });
    }

    /// <summary>取得行を部エンティティへ復元する。</summary>
    private static Division ToEntity(Row row) =>
        Division.Restore(row.Id, row.Code, row.Name, row.CreatedAt);
}

using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>課の永続化ポート <see cref="IDepartmentRepository"/> の Dapper/SQLite 実装。</summary>
public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public DepartmentRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>departments テーブルの1行に対応する DTO。</summary>
    private sealed record Row(Guid Id, Guid DivisionId, string Code, string Name, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, division_id AS DivisionId, code AS Code, name AS Name, created_at AS CreatedAt
        FROM departments
        """;

    /// <inheritdoc />
    public async Task<Department?> FindByIdAsync(DepartmentId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<Department?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Department>> ListByDivisionAsync(DivisionId divisionId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE division_id = @Div ORDER BY code", new { Div = divisionId.Value });
        return rows.Select(ToEntity).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(Department department, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO departments (id, division_id, code, name, created_at)
            VALUES (@Id, @DivisionId, @Code, @Name, @CreatedAt)
            """, new
        {
            Id = department.Id.Value,
            DivisionId = department.DivisionId.Value,
            department.Code,
            department.Name,
            department.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Department department, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE departments SET name = @Name WHERE id = @Id
            """, new { Id = department.Id.Value, department.Name });
    }

    /// <summary>取得行を課エンティティへ復元する。</summary>
    private static Department ToEntity(Row row) =>
        Department.Restore(row.Id, row.DivisionId, row.Code, row.Name, row.CreatedAt);
}

using CostManagement.Domain.Departments;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DepartmentRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, string Code, string Name, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, code AS Code, name AS Name, created_at AS CreatedAt
        FROM departments
        """;

    public async Task<Department?> FindByIdAsync(DepartmentId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<Department?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY code");
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(Department department, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO departments (id, code, name, created_at)
            VALUES (@Id, @Code, @Name, @CreatedAt)
            """, new
        {
            Id = department.Id.Value,
            department.Code,
            department.Name,
            department.CreatedAt,
        });
    }

    public async Task UpdateAsync(Department department, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE departments SET name = @Name WHERE id = @Id
            """, new { Id = department.Id.Value, department.Name });
    }

    private static Department ToEntity(Row row) =>
        Department.Restore(row.Id, row.Code, row.Name, row.CreatedAt);
}

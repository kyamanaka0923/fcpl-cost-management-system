using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>案件マスタの永続化ポート <see cref="IProjectRepository"/> の Dapper/SQLite 実装。</summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public ProjectRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>projects テーブルの1行に対応する DTO。</summary>
    private sealed record Row(Guid Id, Guid DepartmentId, string Code, string Name,
        DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, department_id AS DepartmentId, code AS Code, name AS Name,
               created_at AS CreatedAt
        FROM projects
        """;

    /// <inheritdoc />
    public async Task<Project?> FindByIdAsync(ProjectId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<Project?> FindByCodeAsync(DepartmentId departmentId, string code,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE department_id = @Did AND code = @Code",
            new { Did = departmentId.Value, Code = code });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> ListByDepartmentAsync(DepartmentId departmentId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE department_id = @Did ORDER BY code",
            new { Did = departmentId.Value });
        return rows.Select(ToEntity).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO projects (id, department_id, code, name, created_at)
            VALUES (@Id, @DepartmentId, @Code, @Name, @CreatedAt)
            """, new
        {
            Id = project.Id.Value,
            DepartmentId = project.DepartmentId.Value,
            project.Code,
            project.Name,
            project.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE projects SET code = @Code, name = @Name WHERE id = @Id
            """, new
        {
            Id = project.Id.Value,
            project.Code,
            project.Name,
        });
    }

    /// <summary>取得行を案件エンティティへ復元する。</summary>
    private static Project ToEntity(Row row) =>
        Project.Restore(row.Id, row.DepartmentId, row.Code, row.Name, row.CreatedAt);
}

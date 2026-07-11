using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ProjectRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record Row(Guid Id, Guid DepartmentId, string Code, string Name,
        string Status, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, department_id AS DepartmentId, code AS Code, name AS Name,
               status AS Status, created_at AS CreatedAt
        FROM projects
        """;

    public async Task<Project?> FindByIdAsync(ProjectId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<Project?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE code = @Code", new { Code = code });
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<Project>> ListByDepartmentAsync(DepartmentId departmentId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE department_id = @Did ORDER BY code",
            new { Did = departmentId.Value });
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO projects (id, department_id, code, name, status, created_at)
            VALUES (@Id, @DepartmentId, @Code, @Name, @Status, @CreatedAt)
            """, new
        {
            Id = project.Id.Value,
            DepartmentId = project.DepartmentId.Value,
            project.Code,
            project.Name,
            Status = project.Status.ToString(),
            project.CreatedAt,
        });
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE projects SET name = @Name, status = @Status WHERE id = @Id
            """, new
        {
            Id = project.Id.Value,
            project.Name,
            Status = project.Status.ToString(),
        });
    }

    private static Project ToEntity(Row row) =>
        Project.Restore(row.Id, row.DepartmentId, row.Code, row.Name,
            Enum.Parse<ProjectStatus>(row.Status), row.CreatedAt);
}

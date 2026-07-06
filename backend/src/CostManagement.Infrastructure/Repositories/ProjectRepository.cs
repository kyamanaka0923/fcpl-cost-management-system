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

    private sealed record Row(Guid Id, string Code, string Name, long FiscalYear,
        string Status, DateTime CreatedAt);

    private const string SelectSql = """
        SELECT id AS Id, code AS Code, name AS Name, fiscal_year AS FiscalYear,
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

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>($"{SelectSql} ORDER BY created_at DESC");
        return rows.Select(ToEntity).ToList();
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO projects (id, code, name, fiscal_year, status, created_at)
            VALUES (@Id, @Code, @Name, @FiscalYear, @Status, @CreatedAt)
            """, ToParams(project));
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            UPDATE projects
            SET name = @Name, status = @Status
            WHERE id = @Id
            """, ToParams(project));
    }

    private static object ToParams(Project p) => new
    {
        Id = p.Id.Value,
        p.Code,
        p.Name,
        p.FiscalYear,
        Status = p.Status.ToString(),
        p.CreatedAt,
    };

    private static Project ToEntity(Row row) =>
        Project.Restore(row.Id, row.Code, row.Name, (int)row.FiscalYear,
            Enum.Parse<ProjectStatus>(row.Status), row.CreatedAt);
}

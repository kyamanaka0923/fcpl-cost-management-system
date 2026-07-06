using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Repositories;

public sealed class RevenuePlanRepository : IRevenuePlanRepository
{
    private readonly SqliteConnectionFactory _factory;

    public RevenuePlanRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private sealed record PlanRow(Guid Id, Guid ProjectId, long Version, string Label,
        string Status, DateTime CreatedAt, DateTime? ApprovedAt);

    private sealed record LineRow(Guid Id, Guid PlanId, string ItemName, string Period,
        decimal Quantity, decimal UnitPrice);

    private const string SelectPlanSql = """
        SELECT id AS Id, project_id AS ProjectId, version AS Version, label AS Label,
               status AS Status, created_at AS CreatedAt, approved_at AS ApprovedAt
        FROM revenue_plans
        """;

    private const string SelectLineSql = """
        SELECT id AS Id, plan_id AS PlanId, item_name AS ItemName, period AS Period,
               quantity AS Quantity, unit_price AS UnitPrice
        FROM revenue_plan_lines
        """;

    public async Task<RevenuePlan?> FindByIdAsync(RevenuePlanId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var plan = await conn.QuerySingleOrDefaultAsync<PlanRow>(
            $"{SelectPlanSql} WHERE id = @Id", new { Id = id.Value });
        if (plan is null)
            return null;
        var lines = await conn.QueryAsync<LineRow>(
            $"{SelectLineSql} WHERE plan_id = @Id", new { Id = id.Value });
        return ToEntity(plan, lines);
    }

    public async Task<IReadOnlyList<RevenuePlan>> ListByProjectAsync(ProjectId projectId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var plans = (await conn.QueryAsync<PlanRow>(
            $"{SelectPlanSql} WHERE project_id = @Pid ORDER BY version",
            new { Pid = projectId.Value })).ToList();
        var lines = (await conn.QueryAsync<LineRow>("""
            SELECT l.id AS Id, l.plan_id AS PlanId, l.item_name AS ItemName,
                   l.period AS Period, l.quantity AS Quantity, l.unit_price AS UnitPrice
            FROM revenue_plan_lines l
            JOIN revenue_plans p ON p.id = l.plan_id
            WHERE p.project_id = @Pid
            """, new { Pid = projectId.Value }))
            .ToLookup(l => l.PlanId);
        return plans.Select(p => ToEntity(p, lines[p.Id])).ToList();
    }

    public async Task<RevenuePlan?> FindLatestApprovedAsync(ProjectId projectId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var plan = await conn.QuerySingleOrDefaultAsync<PlanRow>(
            $"{SelectPlanSql} WHERE project_id = @Pid AND status = 'Approved' ORDER BY version DESC LIMIT 1",
            new { Pid = projectId.Value });
        if (plan is null)
            return null;
        var lines = await conn.QueryAsync<LineRow>(
            $"{SelectLineSql} WHERE plan_id = @Id", new { Id = plan.Id });
        return ToEntity(plan, lines);
    }

    public async Task<int> GetMaxVersionAsync(ProjectId projectId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(version), 0) FROM revenue_plans WHERE project_id = @Pid",
            new { Pid = projectId.Value });
    }

    public async Task AddAsync(RevenuePlan plan, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("""
            INSERT INTO revenue_plans (id, project_id, version, label, status, created_at, approved_at)
            VALUES (@Id, @ProjectId, @Version, @Label, @Status, @CreatedAt, @ApprovedAt)
            """, new
        {
            Id = plan.Id.Value,
            ProjectId = plan.ProjectId.Value,
            plan.Version,
            plan.Label,
            Status = plan.Status.ToString(),
            plan.CreatedAt,
            plan.ApprovedAt,
        }, tx);
        await InsertLinesAsync(conn, tx, plan);
        tx.Commit();
    }

    public async Task UpdateAsync(RevenuePlan plan, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("""
            UPDATE revenue_plans
            SET label = @Label, status = @Status, approved_at = @ApprovedAt
            WHERE id = @Id
            """, new
        {
            Id = plan.Id.Value,
            plan.Label,
            Status = plan.Status.ToString(),
            plan.ApprovedAt,
        }, tx);
        // 明細は洗い替え(集約単位での置き換え)とする。
        await conn.ExecuteAsync("DELETE FROM revenue_plan_lines WHERE plan_id = @Id",
            new { Id = plan.Id.Value }, tx);
        await InsertLinesAsync(conn, tx, plan);
        tx.Commit();
    }

    private static async Task InsertLinesAsync(SqliteConnection conn, SqliteTransaction tx,
        RevenuePlan plan)
    {
        foreach (var line in plan.Lines)
        {
            await conn.ExecuteAsync("""
                INSERT INTO revenue_plan_lines (id, plan_id, item_name, period, quantity, unit_price)
                VALUES (@Id, @PlanId, @ItemName, @Period, @Quantity, @UnitPrice)
                """, new
            {
                line.Id,
                PlanId = plan.Id.Value,
                line.ItemName,
                Period = line.Period.ToString(),
                line.Quantity,
                UnitPrice = line.UnitPrice.Value,
            }, tx);
        }
    }

    private static RevenuePlan ToEntity(PlanRow plan, IEnumerable<LineRow> lines) =>
        RevenuePlan.Restore(plan.Id, plan.ProjectId, (int)plan.Version, plan.Label,
            Enum.Parse<PlanStatus>(plan.Status), plan.CreatedAt, plan.ApprovedAt,
            lines.Select(l => (l.Id, l.ItemName, l.Period, l.Quantity, l.UnitPrice)));
}

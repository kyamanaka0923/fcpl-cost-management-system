using CostManagement.Application.Common;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>
/// 売上予算の策定・改定・承認に関するユースケース。
/// 原価予算とは独立にバージョン管理される。
/// </summary>
public sealed class RevenuePlanService
{
    private readonly IRevenuePlanRepository _plans;
    private readonly IProjectRepository _projects;
    private readonly ISystemClock _clock;

    public RevenuePlanService(IRevenuePlanRepository plans, IProjectRepository projects,
        ISystemClock clock)
    {
        _plans = plans;
        _projects = projects;
        _clock = clock;
    }

    public async Task<RevenuePlanDetailDto> CreateDraftAsync(Guid projectId,
        CreatePlanRequest request, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        _ = await _projects.FindByIdAsync(pid, ct)
            ?? throw new NotFoundException($"プロジェクトが見つかりません: {projectId}");

        var existingPlans = await _plans.ListByProjectAsync(pid, ct);
        if (existingPlans.Any(p => p.Status == PlanStatus.Draft))
            throw new DomainException("策定中のドラフトが既に存在します。承認または破棄してから新しい改定版を作成してください。");

        RevenuePlan draft;
        if (existingPlans.Count == 0)
        {
            draft = RevenuePlan.CreateInitial(pid, request.Label, _clock.UtcNow);
        }
        else
        {
            var basePlan = request.BasePlanId is { } baseId
                ? existingPlans.FirstOrDefault(p => p.Id.Value == baseId)
                  ?? throw new NotFoundException($"基となる予算が見つかりません: {baseId}")
                : existingPlans.Where(p => p.Status != PlanStatus.Draft)
                      .OrderByDescending(p => p.Version).First();
            var nextVersion = await _plans.GetMaxVersionAsync(pid, ct) + 1;
            draft = RevenuePlan.ReviseFrom(basePlan, nextVersion, request.Label, _clock.UtcNow);
        }

        await _plans.AddAsync(draft, ct);
        return ToDetailDto(draft);
    }

    public async Task<IReadOnlyList<RevenuePlanSummaryDto>> ListByProjectAsync(Guid projectId,
        CancellationToken ct = default)
    {
        var plans = await _plans.ListByProjectAsync(new ProjectId(projectId), ct);
        return plans.OrderByDescending(p => p.Version).Select(ToSummaryDto).ToList();
    }

    public async Task<RevenuePlanDetailDto> GetAsync(Guid planId, CancellationToken ct = default) =>
        ToDetailDto(await RequireAsync(planId, ct));

    public async Task<RevenuePlanDetailDto> UpsertLineAsync(Guid planId,
        UpsertRevenuePlanLineRequest request, CancellationToken ct = default)
    {
        var plan = await RequireAsync(planId, ct);
        plan.UpsertLine(request.ItemName, AccountingPeriod.Parse(request.Period),
            new Money(request.Amount));
        await _plans.UpdateAsync(plan, ct);
        return ToDetailDto(plan);
    }

    public async Task<RevenuePlanDetailDto> RemoveLineAsync(Guid planId, string itemName,
        string period, CancellationToken ct = default)
    {
        var plan = await RequireAsync(planId, ct);
        plan.RemoveLine(itemName, AccountingPeriod.Parse(period));
        await _plans.UpdateAsync(plan, ct);
        return ToDetailDto(plan);
    }

    /// <summary>売上予算を承認する。既存の承認済みバージョンは失効(Superseded)となる。</summary>
    public async Task<RevenuePlanDetailDto> ApproveAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await RequireAsync(planId, ct);
        plan.Approve(_clock.UtcNow);

        var siblings = await _plans.ListByProjectAsync(plan.ProjectId, ct);
        foreach (var sibling in siblings.Where(p =>
                     p.Id != plan.Id && p.Status == PlanStatus.Approved))
        {
            sibling.Supersede();
            await _plans.UpdateAsync(sibling, ct);
        }

        await _plans.UpdateAsync(plan, ct);
        return ToDetailDto(plan);
    }

    private async Task<RevenuePlan> RequireAsync(Guid planId, CancellationToken ct) =>
        await _plans.FindByIdAsync(new RevenuePlanId(planId), ct)
        ?? throw new NotFoundException($"売上予算が見つかりません: {planId}");

    internal static RevenuePlanSummaryDto ToSummaryDto(RevenuePlan p) =>
        new(p.Id.Value, p.ProjectId.Value, p.Version, p.Label, p.Status.ToString(),
            p.CreatedAt, p.ApprovedAt, p.TotalAmount.Value);

    internal static RevenuePlanDetailDto ToDetailDto(RevenuePlan p) =>
        new(p.Id.Value, p.ProjectId.Value, p.Version, p.Label, p.Status.ToString(),
            p.CreatedAt, p.ApprovedAt, p.TotalAmount.Value,
            p.Lines
                .OrderBy(l => l.Period)
                .ThenBy(l => l.ItemName)
                .Select(l => new RevenuePlanLineDto(l.Id, l.ItemName, l.Period.ToString(),
                    l.Amount.Value))
                .ToList());
}

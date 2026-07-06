using CostManagement.Application.Common;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>
/// 原価予算(予定)の策定・改定・承認に関するユースケース。
/// 予算はプロジェクトごとにバージョン管理され、四半期などの節目で何度でも改定できる。
/// </summary>
public sealed class CostPlanService
{
    private readonly ICostPlanRepository _plans;
    private readonly IProjectRepository _projects;
    private readonly ICostElementRepository _elements;
    private readonly ISystemClock _clock;

    public CostPlanService(ICostPlanRepository plans, IProjectRepository projects,
        ICostElementRepository elements, ISystemClock clock)
    {
        _plans = plans;
        _projects = projects;
        _elements = elements;
        _clock = clock;
    }

    /// <summary>
    /// 予算ドラフトを起票する。初回は当初予算(バージョン1)、
    /// 以降は既存バージョン(未指定時は最新承認版)を引き継いだ改定版となる。
    /// </summary>
    public async Task<CostPlanDetailDto> CreateDraftAsync(Guid projectId, CreatePlanRequest request,
        CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        _ = await _projects.FindByIdAsync(pid, ct)
            ?? throw new NotFoundException($"プロジェクトが見つかりません: {projectId}");

        var existingPlans = await _plans.ListByProjectAsync(pid, ct);
        if (existingPlans.Any(p => p.Status == PlanStatus.Draft))
            throw new DomainException("策定中のドラフトが既に存在します。承認または破棄してから新しい改定版を作成してください。");

        CostPlan draft;
        if (existingPlans.Count == 0)
        {
            draft = CostPlan.CreateInitial(pid, request.Label, _clock.UtcNow);
        }
        else
        {
            var basePlan = request.BasePlanId is { } baseId
                ? existingPlans.FirstOrDefault(p => p.Id.Value == baseId)
                  ?? throw new NotFoundException($"基となる予算が見つかりません: {baseId}")
                : existingPlans.Where(p => p.Status != PlanStatus.Draft)
                      .OrderByDescending(p => p.Version).First();
            var nextVersion = await _plans.GetMaxVersionAsync(pid, ct) + 1;
            draft = CostPlan.ReviseFrom(basePlan, nextVersion, request.Label, _clock.UtcNow);
        }

        await _plans.AddAsync(draft, ct);
        return ToDetailDto(draft);
    }

    public async Task<IReadOnlyList<CostPlanSummaryDto>> ListByProjectAsync(Guid projectId,
        CancellationToken ct = default)
    {
        var plans = await _plans.ListByProjectAsync(new ProjectId(projectId), ct);
        return plans.OrderByDescending(p => p.Version).Select(ToSummaryDto).ToList();
    }

    public async Task<CostPlanDetailDto> GetAsync(Guid planId, CancellationToken ct = default) =>
        ToDetailDto(await RequireAsync(planId, ct));

    public async Task<CostPlanDetailDto> UpsertLineAsync(Guid planId, UpsertPlanLineRequest request,
        CancellationToken ct = default)
    {
        var plan = await RequireAsync(planId, ct);
        var code = new CostElementCode(request.ElementCode);
        _ = await _elements.FindByCodeAsync(code, ct)
            ?? throw new NotFoundException($"費目が見つかりません: {request.ElementCode}");

        plan.UpsertLine(code, request.RevenueItem, AccountingPeriod.Parse(request.Period),
            new Money(request.Amount));
        await _plans.UpdateAsync(plan, ct);
        return ToDetailDto(plan);
    }

    public async Task<CostPlanDetailDto> RemoveLineAsync(Guid planId, string elementCode,
        string? revenueItem, string period, CancellationToken ct = default)
    {
        var plan = await RequireAsync(planId, ct);
        plan.RemoveLine(new CostElementCode(elementCode), revenueItem,
            AccountingPeriod.Parse(period));
        await _plans.UpdateAsync(plan, ct);
        return ToDetailDto(plan);
    }

    /// <summary>予算を承認する。既存の承認済みバージョンは失効(Superseded)となる。</summary>
    public async Task<CostPlanDetailDto> ApproveAsync(Guid planId, CancellationToken ct = default)
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

    private async Task<CostPlan> RequireAsync(Guid planId, CancellationToken ct) =>
        await _plans.FindByIdAsync(new CostPlanId(planId), ct)
        ?? throw new NotFoundException($"予算が見つかりません: {planId}");

    internal static CostPlanSummaryDto ToSummaryDto(CostPlan p) =>
        new(p.Id.Value, p.ProjectId.Value, p.Version, p.Label, p.Status.ToString(),
            p.CreatedAt, p.ApprovedAt, p.TotalAmount.Value);

    internal static CostPlanDetailDto ToDetailDto(CostPlan p) =>
        new(p.Id.Value, p.ProjectId.Value, p.Version, p.Label, p.Status.ToString(),
            p.CreatedAt, p.ApprovedAt, p.TotalAmount.Value,
            p.Lines
                .OrderBy(l => l.Period)
                .ThenBy(l => l.ElementCode.Value)
                .ThenBy(l => l.RevenueItem)
                .Select(l => new PlanLineDto(l.Id, l.ElementCode.Value, l.RevenueItem,
                    l.Period.ToString(), l.Amount.Value))
                .ToList());
}

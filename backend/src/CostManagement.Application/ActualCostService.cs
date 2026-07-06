using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>原価実績の計上に関するユースケース。</summary>
public sealed class ActualCostService
{
    private readonly IActualCostRepository _actuals;
    private readonly IProjectRepository _projects;
    private readonly ICostElementRepository _elements;
    private readonly ISystemClock _clock;

    public ActualCostService(IActualCostRepository actuals, IProjectRepository projects,
        ICostElementRepository elements, ISystemClock clock)
    {
        _actuals = actuals;
        _projects = projects;
        _elements = elements;
        _clock = clock;
    }

    public async Task<ActualCostDto> RecordAsync(Guid projectId, RecordActualRequest request,
        CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        _ = await _projects.FindByIdAsync(pid, ct)
            ?? throw new NotFoundException($"プロジェクトが見つかりません: {projectId}");

        var code = new CostElementCode(request.ElementCode);
        _ = await _elements.FindByCodeAsync(code, ct)
            ?? throw new NotFoundException($"費目が見つかりません: {request.ElementCode}");

        var actual = ActualCost.Record(pid, code, request.RevenueItem,
            AccountingPeriod.Parse(request.Period), new Money(request.Amount),
            request.Note, _clock.UtcNow);
        await _actuals.AddAsync(actual, ct);
        return ToDto(actual);
    }

    public async Task<IReadOnlyList<ActualCostDto>> ListByProjectAsync(Guid projectId,
        CancellationToken ct = default)
    {
        var actuals = await _actuals.ListByProjectAsync(new ProjectId(projectId), ct);
        return actuals
            .OrderBy(a => a.Period)
            .ThenBy(a => a.ElementCode.Value)
            .Select(ToDto)
            .ToList();
    }

    public async Task DeleteAsync(Guid actualId, CancellationToken ct = default)
    {
        var id = new ActualCostId(actualId);
        _ = await _actuals.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"実績が見つかりません: {actualId}");
        await _actuals.DeleteAsync(id, ct);
    }

    internal static ActualCostDto ToDto(ActualCost a) =>
        new(a.Id.Value, a.ProjectId.Value, a.ElementCode.Value, a.RevenueItem,
            a.Period.ToString(), a.Amount.Value, a.Note, a.RecordedAt);
}

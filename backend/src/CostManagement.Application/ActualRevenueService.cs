using CostManagement.Application.Common;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>売上実績の計上に関するユースケース。</summary>
public sealed class ActualRevenueService
{
    private readonly IActualRevenueRepository _actuals;
    private readonly IProjectRepository _projects;
    private readonly ISystemClock _clock;

    public ActualRevenueService(IActualRevenueRepository actuals, IProjectRepository projects,
        ISystemClock clock)
    {
        _actuals = actuals;
        _projects = projects;
        _clock = clock;
    }

    public async Task<ActualRevenueDto> RecordAsync(Guid projectId, RecordRevenueRequest request,
        CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        _ = await _projects.FindByIdAsync(pid, ct)
            ?? throw new NotFoundException($"プロジェクトが見つかりません: {projectId}");

        var actual = ActualRevenue.Record(pid, request.ItemName,
            AccountingPeriod.Parse(request.Period), new Money(request.Amount),
            request.Note, _clock.UtcNow);
        await _actuals.AddAsync(actual, ct);
        return ToDto(actual);
    }

    public async Task<IReadOnlyList<ActualRevenueDto>> ListByProjectAsync(Guid projectId,
        CancellationToken ct = default)
    {
        var actuals = await _actuals.ListByProjectAsync(new ProjectId(projectId), ct);
        return actuals
            .OrderBy(a => a.Period)
            .ThenBy(a => a.ItemName)
            .Select(ToDto)
            .ToList();
    }

    public async Task DeleteAsync(Guid actualId, CancellationToken ct = default)
    {
        var id = new ActualRevenueId(actualId);
        _ = await _actuals.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"売上実績が見つかりません: {actualId}");
        await _actuals.DeleteAsync(id, ct);
    }

    internal static ActualRevenueDto ToDto(ActualRevenue a) =>
        new(a.Id.Value, a.ProjectId.Value, a.ItemName, a.Period.ToString(),
            a.Amount.Value, a.Note, a.RecordedAt);
}

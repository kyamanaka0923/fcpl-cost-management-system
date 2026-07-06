using CostManagement.Application.Common;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>プロジェクトに関するユースケース。</summary>
public sealed class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly ISystemClock _clock;

    public ProjectService(IProjectRepository projects, ISystemClock clock)
    {
        _projects = projects;
        _clock = clock;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var existing = await _projects.FindByCodeAsync(request.Code.Trim(), ct);
        if (existing is not null)
            throw new DomainException($"プロジェクトコード '{request.Code}' は既に使用されています。");

        var project = Project.Create(request.Code, request.Name, request.FiscalYear, _clock.UtcNow);
        await _projects.AddAsync(project, ct);
        return ToDto(project);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default) =>
        (await _projects.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await RequireAsync(id, ct));

    public async Task<ProjectDto> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await RequireAsync(id, ct);
        project.Complete();
        await _projects.UpdateAsync(project, ct);
        return ToDto(project);
    }

    private async Task<Project> RequireAsync(Guid id, CancellationToken ct) =>
        await _projects.FindByIdAsync(new ProjectId(id), ct)
        ?? throw new NotFoundException($"プロジェクトが見つかりません: {id}");

    internal static ProjectDto ToDto(Project p) =>
        new(p.Id.Value, p.Code, p.Name, p.FiscalYear, p.Status.ToString(), p.CreatedAt);
}

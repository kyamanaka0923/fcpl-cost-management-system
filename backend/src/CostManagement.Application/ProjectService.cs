using CostManagement.Application.Common;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>案件マスタ(課に属する)の登録・参照ユースケース。</summary>
public sealed class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly ISystemClock _clock;

    public ProjectService(IProjectRepository projects, IDepartmentRepository departments,
        ISystemClock clock)
    {
        _projects = projects;
        _departments = departments;
        _clock = clock;
    }

    public async Task<ProjectDto> CreateAsync(Guid departmentId, CreateProjectRequest request,
        CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        _ = await _departments.FindByIdAsync(did, ct)
            ?? throw new NotFoundException($"課が見つかりません: {departmentId}");
        if (await _projects.FindByCodeAsync(request.Code?.Trim() ?? "", ct) is not null)
            throw new DomainException($"案件コードが重複しています: {request.Code}");

        var project = Project.Create(did, request.Code!, request.Name, _clock.UtcNow);
        await _projects.AddAsync(project, ct);
        return ToDto(project);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListByDepartmentAsync(Guid departmentId,
        CancellationToken ct = default)
    {
        var projects = await _projects.ListByDepartmentAsync(new DepartmentId(departmentId), ct);
        return projects.OrderBy(p => p.Code).Select(ToDto).ToList();
    }

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
        ?? throw new NotFoundException($"案件が見つかりません: {id}");

    internal static ProjectDto ToDto(Project p) =>
        new(p.Id.Value, p.DepartmentId.Value, p.Code, p.Name, p.Status.ToString(), p.CreatedAt);
}

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

    /// <summary>依存する案件・課リポジトリと時計を受け取る。</summary>
    public ProjectService(IProjectRepository projects, IDepartmentRepository departments,
        ISystemClock clock)
    {
        _projects = projects;
        _departments = departments;
        _clock = clock;
    }

    /// <summary>
    /// 指定した課に案件を新規登録する。課が無ければ <see cref="NotFoundException"/>、
    /// 同一課での案件コード重複は <see cref="DomainException"/>(別の課では同じコード可)。
    /// </summary>
    public async Task<ProjectDto> CreateAsync(Guid departmentId, CreateProjectRequest request,
        CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        _ = await _departments.FindByIdAsync(did, ct)
            ?? throw new NotFoundException($"課が見つかりません: {departmentId}");
        // 案件コードは課ごとに一意。別の課では同じコードを使える。
        if (await _projects.FindByCodeAsync(did, request.Code?.Trim() ?? "", ct) is not null)
            throw new DomainException($"この課には既に案件コード '{request.Code}' が存在します。");

        var project = Project.Create(did, request.Code!, request.Name, _clock.UtcNow);
        await _projects.AddAsync(project, ct);
        return ToDto(project);
    }

    /// <summary>指定した課に属する案件をコード順で取得する。</summary>
    public async Task<IReadOnlyList<ProjectDto>> ListByDepartmentAsync(Guid departmentId,
        CancellationToken ct = default)
    {
        var projects = await _projects.ListByDepartmentAsync(new DepartmentId(departmentId), ct);
        return projects.OrderBy(p => p.Code).Select(ToDto).ToList();
    }

    /// <summary>IDで案件を取得する。存在しなければ <see cref="NotFoundException"/>。</summary>
    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await RequireAsync(id, ct));

    /// <summary>
    /// 案件のコード・名称を更新する。案件が無ければ <see cref="NotFoundException"/>。
    /// 同一課内で別の案件がそのコードを使っている場合は <see cref="DomainException"/>
    /// (別の課では同じコード可)。案件の同一性は GUID で保たれるため、予算明細・実績の参照は壊れない。
    /// </summary>
    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request,
        CancellationToken ct = default)
    {
        var project = await RequireAsync(id, ct);
        var newCode = request.Code?.Trim() ?? "";
        var duplicate = await _projects.FindByCodeAsync(project.DepartmentId, newCode, ct);
        if (duplicate is not null && duplicate.Id != project.Id)
            throw new DomainException($"この課には既に案件コード '{request.Code}' が存在します。");

        project.Edit(request.Code!, request.Name);
        await _projects.UpdateAsync(project, ct);
        return ToDto(project);
    }

    /// <summary>IDで案件を取得する。無ければ <see cref="NotFoundException"/> を投げる内部ヘルパ。</summary>
    private async Task<Project> RequireAsync(Guid id, CancellationToken ct) =>
        await _projects.FindByIdAsync(new ProjectId(id), ct)
        ?? throw new NotFoundException($"案件が見つかりません: {id}");

    /// <summary>ドメインの案件を応答 DTO へ変換する。</summary>
    internal static ProjectDto ToDto(Project p) =>
        new(p.Id.Value, p.DepartmentId.Value, p.Code, p.Name, p.CreatedAt);
}

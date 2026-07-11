using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Projects;

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum ProjectStatus
{
    Active,
    Completed,
}

/// <summary>課に属する案件。予算・実績の明細の内訳次元。集約ルート。</summary>
public sealed class Project
{
    public ProjectId Id { get; }
    public DepartmentId DepartmentId { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; }

    private Project(ProjectId id, DepartmentId departmentId, string code, string name,
        ProjectStatus status, DateTime createdAt)
    {
        Id = id;
        DepartmentId = departmentId;
        Code = code;
        Name = name;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Project Create(DepartmentId departmentId, string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("案件コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("案件名は必須です。");
        return new Project(ProjectId.New(), departmentId, code.Trim(), name.Trim(),
            ProjectStatus.Active, now);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("案件名は必須です。");
        Name = name.Trim();
    }

    public void Complete() => Status = ProjectStatus.Completed;

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Project Restore(Guid id, Guid departmentId, string code, string name,
        ProjectStatus status, DateTime createdAt) =>
        new(new ProjectId(id), new DepartmentId(departmentId), code, name, status, createdAt);
}

public interface IProjectRepository
{
    Task<Project?> FindByIdAsync(ProjectId id, CancellationToken ct = default);
    Task<Project?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByDepartmentAsync(DepartmentId departmentId, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

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

/// <summary>原価管理の対象単位(プロジェクト/製品)。集約ルート。</summary>
public sealed class Project
{
    public ProjectId Id { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public int FiscalYear { get; }
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; }

    private Project(ProjectId id, string code, string name, int fiscalYear,
        ProjectStatus status, DateTime createdAt)
    {
        Id = id;
        Code = code;
        Name = name;
        FiscalYear = fiscalYear;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Project Create(string code, string name, int fiscalYear, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("プロジェクトコードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("プロジェクト名は必須です。");
        if (fiscalYear is < 2000 or > 2100)
            throw new DomainException($"会計年度が不正です: {fiscalYear}");
        return new Project(ProjectId.New(), code.Trim(), name.Trim(), fiscalYear,
            ProjectStatus.Active, now);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("プロジェクト名は必須です。");
        Name = name.Trim();
    }

    public void Complete() => Status = ProjectStatus.Completed;

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Project Restore(Guid id, string code, string name, int fiscalYear,
        ProjectStatus status, DateTime createdAt) =>
        new(new ProjectId(id), code, name, fiscalYear, status, createdAt);
}

public interface IProjectRepository
{
    Task<Project?> FindByIdAsync(ProjectId id, CancellationToken ct = default);
    Task<Project?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

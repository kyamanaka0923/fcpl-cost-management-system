using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Projects;

/// <summary>案件を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct ProjectId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static ProjectId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>課に属する案件。予算・実績の明細の内訳次元。集約ルート。</summary>
public sealed class Project
{
    /// <summary>案件ID。</summary>
    public ProjectId Id { get; }

    /// <summary>所属する課のID。</summary>
    public DepartmentId DepartmentId { get; }

    /// <summary>案件コード(課ごとに一意)。</summary>
    public string Code { get; }

    /// <summary>案件名(表示用)。</summary>
    public string Name { get; private set; }

    /// <summary>作成日時(UTC)。</summary>
    public DateTime CreatedAt { get; }

    private Project(ProjectId id, DepartmentId departmentId, string code, string name,
        DateTime createdAt)
    {
        Id = id;
        DepartmentId = departmentId;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>課・コード・名称を検証して新しい案件を生成する。コード・名称必須。</summary>
    public static Project Create(DepartmentId departmentId, string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("案件コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("案件名は必須です。");
        return new Project(ProjectId.New(), departmentId, code.Trim(), name.Trim(), now);
    }

    /// <summary>案件名を変更する。空名称は <see cref="DomainException"/>。</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("案件名は必須です。");
        Name = name.Trim();
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Project Restore(Guid id, Guid departmentId, string code, string name,
        DateTime createdAt) =>
        new(new ProjectId(id), new DepartmentId(departmentId), code, name, createdAt);
}

/// <summary>案件マスタの永続化ポート(実装はインフラ層)。</summary>
public interface IProjectRepository
{
    /// <summary>IDで案件を1件取得する。無ければ null。</summary>
    Task<Project?> FindByIdAsync(ProjectId id, CancellationToken ct = default);

    /// <summary>案件コードで1件取得する。案件コードは課ごとに一意(別の課では同じコードを使える)。</summary>
    Task<Project?> FindByCodeAsync(DepartmentId departmentId, string code, CancellationToken ct = default);

    /// <summary>指定した課に属する案件の一覧を取得する。</summary>
    Task<IReadOnlyList<Project>> ListByDepartmentAsync(DepartmentId departmentId, CancellationToken ct = default);

    /// <summary>案件を1件追加する。</summary>
    Task AddAsync(Project project, CancellationToken ct = default);

    /// <summary>案件(名称など)を更新する。</summary>
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

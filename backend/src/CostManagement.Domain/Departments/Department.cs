using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Departments;

/// <summary>課を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct DepartmentId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static DepartmentId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>予算策定の管理単位となる課。部に属する。集約ルート。</summary>
public sealed class Department
{
    /// <summary>課ID。</summary>
    public DepartmentId Id { get; }

    /// <summary>所属する部のID。</summary>
    public DivisionId DivisionId { get; }

    /// <summary>課コード(一意)。</summary>
    public string Code { get; }

    /// <summary>課名(表示用)。</summary>
    public string Name { get; private set; }

    /// <summary>作成日時(UTC)。</summary>
    public DateTime CreatedAt { get; }

    private Department(DepartmentId id, DivisionId divisionId, string code, string name,
        DateTime createdAt)
    {
        Id = id;
        DivisionId = divisionId;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>部・コード・名称を検証して新しい課を生成する。コード・名称必須。</summary>
    public static Department Create(DivisionId divisionId, string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("課コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("課名は必須です。");
        return new Department(DepartmentId.New(), divisionId, code.Trim(), name.Trim(), now);
    }

    /// <summary>課名を変更する。空名称は <see cref="DomainException"/>。</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("課名は必須です。");
        Name = name.Trim();
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Department Restore(Guid id, Guid divisionId, string code, string name,
        DateTime createdAt) =>
        new(new DepartmentId(id), new DivisionId(divisionId), code, name, createdAt);
}

/// <summary>課の永続化ポート(実装はインフラ層)。</summary>
public interface IDepartmentRepository
{
    /// <summary>IDで課を1件取得する。無ければ null。</summary>
    Task<Department?> FindByIdAsync(DepartmentId id, CancellationToken ct = default);

    /// <summary>コードで課を1件取得する。無ければ null。</summary>
    Task<Department?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>指定した部に属する課の一覧を取得する。</summary>
    Task<IReadOnlyList<Department>> ListByDivisionAsync(DivisionId divisionId, CancellationToken ct = default);

    /// <summary>課を1件追加する。</summary>
    Task AddAsync(Department department, CancellationToken ct = default);

    /// <summary>課(名称など)を更新する。</summary>
    Task UpdateAsync(Department department, CancellationToken ct = default);
}

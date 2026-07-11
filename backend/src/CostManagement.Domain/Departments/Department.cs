using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Departments;

public readonly record struct DepartmentId(Guid Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>予算策定の管理単位となる課。部に属する。集約ルート。</summary>
public sealed class Department
{
    public DepartmentId Id { get; }
    public DivisionId DivisionId { get; }
    public string Code { get; }
    public string Name { get; private set; }
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

    public static Department Create(DivisionId divisionId, string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("課コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("課名は必須です。");
        return new Department(DepartmentId.New(), divisionId, code.Trim(), name.Trim(), now);
    }

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

public interface IDepartmentRepository
{
    Task<Department?> FindByIdAsync(DepartmentId id, CancellationToken ct = default);
    Task<Department?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListByDivisionAsync(DivisionId divisionId, CancellationToken ct = default);
    Task AddAsync(Department department, CancellationToken ct = default);
    Task UpdateAsync(Department department, CancellationToken ct = default);
}

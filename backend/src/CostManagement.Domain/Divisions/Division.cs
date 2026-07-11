using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Divisions;

public readonly record struct DivisionId(Guid Value)
{
    public static DivisionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>課の上位組織となる部。配下の課の予実を合計して把握する単位。集約ルート。</summary>
public sealed class Division
{
    public DivisionId Id { get; }
    public string Code { get; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; }

    private Division(DivisionId id, string code, string name, DateTime createdAt)
    {
        Id = id;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
    }

    public static Division Create(string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("部コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("部名は必須です。");
        return new Division(DivisionId.New(), code.Trim(), name.Trim(), now);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("部名は必須です。");
        Name = name.Trim();
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Division Restore(Guid id, string code, string name, DateTime createdAt) =>
        new(new DivisionId(id), code, name, createdAt);
}

public interface IDivisionRepository
{
    Task<Division?> FindByIdAsync(DivisionId id, CancellationToken ct = default);
    Task<Division?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Division>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Division division, CancellationToken ct = default);
    Task UpdateAsync(Division division, CancellationToken ct = default);
}

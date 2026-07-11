using CostManagement.Domain.Shared;

namespace CostManagement.Domain.CostElements;

/// <summary>費目コードを表す値オブジェクト。</summary>
public readonly record struct CostElementCode
{
    public string Value { get; }

    public CostElementCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("費目コードは必須です。");
        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;
}

/// <summary>期間費用の費目マスタ(人件費・ライセンス費など)。集約ルート。</summary>
public sealed class CostElement
{
    public CostElementCode Code { get; }
    public string Name { get; private set; }

    private CostElement(CostElementCode code, string name)
    {
        Code = code;
        Name = name;
    }

    public static CostElement Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("費目名は必須です。");
        return new CostElement(new CostElementCode(code), name.Trim());
    }

    public static CostElement Restore(string code, string name) =>
        new(new CostElementCode(code), name);
}

public interface ICostElementRepository
{
    Task<CostElement?> FindByCodeAsync(CostElementCode code, CancellationToken ct = default);
    Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default);
    Task AddAsync(CostElement element, CancellationToken ct = default);
}

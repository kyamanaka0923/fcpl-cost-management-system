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

/// <summary>費目の分類(総合原価計算の原価要素)。</summary>
public enum CostElementType
{
    /// <summary>材料費</summary>
    Material,

    /// <summary>労務費</summary>
    Labor,

    /// <summary>製造間接費</summary>
    Overhead,

    /// <summary>経費</summary>
    Expense,
}

/// <summary>費目マスタ。集約ルート。</summary>
public sealed class CostElement
{
    public CostElementCode Code { get; }
    public string Name { get; private set; }
    public CostElementType Type { get; }

    private CostElement(CostElementCode code, string name, CostElementType type)
    {
        Code = code;
        Name = name;
        Type = type;
    }

    public static CostElement Create(string code, string name, CostElementType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("費目名は必須です。");
        return new CostElement(new CostElementCode(code), name.Trim(), type);
    }

    public static CostElement Restore(string code, string name, CostElementType type) =>
        new(new CostElementCode(code), name, type);
}

public interface ICostElementRepository
{
    Task<CostElement?> FindByCodeAsync(CostElementCode code, CancellationToken ct = default);
    Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default);
    Task AddAsync(CostElement element, CancellationToken ct = default);
}

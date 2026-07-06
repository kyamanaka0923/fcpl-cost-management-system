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

    /// <summary>数量×単価で管理する費目か(false の場合は金額のみで管理)。</summary>
    public bool IsQuantityManaged { get; }

    private CostElement(CostElementCode code, string name, CostElementType type,
        bool isQuantityManaged)
    {
        Code = code;
        Name = name;
        Type = type;
        IsQuantityManaged = isQuantityManaged;
    }

    public static CostElement Create(string code, string name, CostElementType type,
        bool isQuantityManaged)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("費目名は必須です。");
        return new CostElement(new CostElementCode(code), name.Trim(), type, isQuantityManaged);
    }

    public static CostElement Restore(string code, string name, CostElementType type,
        bool isQuantityManaged) =>
        new(new CostElementCode(code), name, type, isQuantityManaged);
}

public interface ICostElementRepository
{
    Task<CostElement?> FindByCodeAsync(CostElementCode code, CancellationToken ct = default);
    Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default);
    Task AddAsync(CostElement element, CancellationToken ct = default);
}

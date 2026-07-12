using CostManagement.Domain.Shared;

namespace CostManagement.Domain.CostElements;

/// <summary>費目コードを表す値オブジェクト。</summary>
public readonly record struct CostElementCode
{
    /// <summary>正規化済み(トリム + 大文字化)のコード文字列。</summary>
    public string Value { get; }

    /// <summary>コード文字列を検証・正規化して生成する。空文字は <see cref="DomainException"/>。</summary>
    public CostElementCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("費目コードは必須です。");
        Value = value.Trim().ToUpperInvariant();
    }

    /// <summary>コード文字列を返す。</summary>
    public override string ToString() => Value;
}

/// <summary>期間費用の費目マスタ(人件費・ライセンス費など)。集約ルート。</summary>
public sealed class CostElement
{
    /// <summary>費目コード(システム全体で一意)。</summary>
    public CostElementCode Code { get; }

    /// <summary>費目名(表示用)。</summary>
    public string Name { get; private set; }

    private CostElement(CostElementCode code, string name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>コードと名称を検証して新しい費目を生成する。名称必須。</summary>
    public static CostElement Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("費目名は必須です。");
        return new CostElement(new CostElementCode(code), name.Trim());
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static CostElement Restore(string code, string name) =>
        new(new CostElementCode(code), name);
}

/// <summary>費目マスタの永続化ポート(実装はインフラ層)。</summary>
public interface ICostElementRepository
{
    /// <summary>コードで費目を1件取得する。無ければ null。</summary>
    Task<CostElement?> FindByCodeAsync(CostElementCode code, CancellationToken ct = default);

    /// <summary>全費目を取得する。</summary>
    Task<IReadOnlyList<CostElement>> ListAsync(CancellationToken ct = default);

    /// <summary>費目を1件追加する。</summary>
    Task AddAsync(CostElement element, CancellationToken ct = default);
}

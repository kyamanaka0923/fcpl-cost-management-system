namespace CostManagement.Domain.Shared;

/// <summary>金額を表す値オブジェクト(円)。</summary>
public readonly record struct Money(decimal Value) : IComparable<Money>
{
    /// <summary>金額 0 円。加算の初期値などに使う。</summary>
    public static readonly Money Zero = new(0m);

    /// <summary>2つの金額を加算する。</summary>
    public static Money operator +(Money a, Money b) => new(a.Value + b.Value);

    /// <summary>金額 a から b を減算する(差異計算に使う)。</summary>
    public static Money operator -(Money a, Money b) => new(a.Value - b.Value);

    /// <summary>金額に係数を掛ける。</summary>
    public static Money operator *(Money a, decimal factor) => new(a.Value * factor);

    /// <summary>金額が負(赤字・不利差異)かどうか。</summary>
    public bool IsNegative => Value < 0m;

    /// <summary>金額の大小を比較する。</summary>
    public int CompareTo(Money other) => Value.CompareTo(other.Value);

    /// <summary>3桁区切りの文字列表現を返す。</summary>
    public override string ToString() => Value.ToString("#,0.##");
}

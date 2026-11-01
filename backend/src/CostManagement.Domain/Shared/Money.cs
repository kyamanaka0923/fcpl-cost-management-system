using System.Globalization;

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

    /// <summary>金額が負(赤字・マイナスの差異)かどうか。</summary>
    public bool IsNegative => Value < 0m;

    /// <summary>
    /// 金額の受け入れ判定(BR-BUD-007)。0 以上の 1 円単位の整数だけを受け付ける。
    /// 判定するのは値であり、100.0 は 100 と同じ値として受け付ける。
    /// 両方に外れる値(例 -0.5)は 0 以上の文言を出すため、0 以上を先に判定する。
    /// </summary>
    /// <exception cref="DomainException">0 未満、または 1 円未満の端数がある。</exception>
    public void EnsureAcceptableAmount()
    {
        if (IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
        if (Value != decimal.Floor(Value))
            throw new DomainException("金額は1円単位の整数で入力してください。");
    }

    /// <summary>金額の大小を比較する。</summary>
    public int CompareTo(Money other) => Value.CompareTo(other.Value);

    /// <summary>a が b より小さいか。</summary>
    public static bool operator <(Money a, Money b) => a.Value < b.Value;

    /// <summary>a が b 以下か。</summary>
    public static bool operator <=(Money a, Money b) => a.Value <= b.Value;

    /// <summary>a が b より大きいか。</summary>
    public static bool operator >(Money a, Money b) => a.Value > b.Value;

    /// <summary>a が b 以上か。</summary>
    public static bool operator >=(Money a, Money b) => a.Value >= b.Value;

    /// <summary>3桁区切りの文字列表現を返す(実行環境のロケールに依存しない)。</summary>
    public override string ToString() => Value.ToString("#,0.##", CultureInfo.InvariantCulture);
}

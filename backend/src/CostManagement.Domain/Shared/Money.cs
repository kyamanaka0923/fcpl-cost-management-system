namespace CostManagement.Domain.Shared;

/// <summary>金額を表す値オブジェクト(円)。</summary>
public readonly record struct Money(decimal Value) : IComparable<Money>
{
    public static readonly Money Zero = new(0m);

    public static Money operator +(Money a, Money b) => new(a.Value + b.Value);
    public static Money operator -(Money a, Money b) => new(a.Value - b.Value);
    public static Money operator *(Money a, decimal factor) => new(a.Value * factor);

    public bool IsNegative => Value < 0m;

    public int CompareTo(Money other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString("#,0.##");
}

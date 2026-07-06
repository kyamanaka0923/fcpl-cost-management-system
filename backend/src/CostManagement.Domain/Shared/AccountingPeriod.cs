using System.Globalization;

namespace CostManagement.Domain.Shared;

/// <summary>会計期間(年月)を表す値オブジェクト。"yyyy-MM" 形式で表現する。</summary>
public readonly record struct AccountingPeriod : IComparable<AccountingPeriod>
{
    public int Year { get; }
    public int Month { get; }

    public AccountingPeriod(int year, int month)
    {
        if (year is < 2000 or > 2100)
            throw new DomainException($"会計年度が不正です: {year}");
        if (month is < 1 or > 12)
            throw new DomainException($"月が不正です: {month}");
        Year = year;
        Month = month;
    }

    /// <summary>この年月が属する四半期(暦月ベース、1〜4)。</summary>
    public int Quarter => (Month - 1) / 3 + 1;

    public static AccountingPeriod Parse(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            throw new DomainException($"会計期間の形式が不正です(yyyy-MM): {value}");
        return new AccountingPeriod(parsed.Year, parsed.Month);
    }

    public int CompareTo(AccountingPeriod other) =>
        (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);

    public static bool operator <=(AccountingPeriod a, AccountingPeriod b) => a.CompareTo(b) <= 0;
    public static bool operator >=(AccountingPeriod a, AccountingPeriod b) => a.CompareTo(b) >= 0;
    public static bool operator <(AccountingPeriod a, AccountingPeriod b) => a.CompareTo(b) < 0;
    public static bool operator >(AccountingPeriod a, AccountingPeriod b) => a.CompareTo(b) > 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}

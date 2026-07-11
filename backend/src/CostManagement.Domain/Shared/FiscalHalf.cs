using System.Globalization;

namespace CostManagement.Domain.Shared;

/// <summary>半期の区分(上期/下期)。</summary>
public enum HalfTerm
{
    /// <summary>上期(4月〜9月)</summary>
    H1 = 1,

    /// <summary>下期(10月〜3月)</summary>
    H2 = 2,
}

/// <summary>会計半期(年度 + 上期/下期)を表す値オブジェクト。"yyyy-H1" / "yyyy-H2" 形式で表現する。</summary>
public readonly record struct FiscalHalf : IComparable<FiscalHalf>
{
    public int Year { get; }
    public HalfTerm Half { get; }

    public FiscalHalf(int year, HalfTerm half)
    {
        if (year is < 2000 or > 2100)
            throw new DomainException($"会計年度が不正です: {year}");
        if (half is not (HalfTerm.H1 or HalfTerm.H2))
            throw new DomainException($"半期の区分が不正です: {half}");
        Year = year;
        Half = half;
    }

    public static FiscalHalf Parse(string value)
    {
        var parts = (value ?? "").Split('-');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            && parts[1] is "H1" or "H2")
        {
            return new FiscalHalf(year, parts[1] == "H1" ? HalfTerm.H1 : HalfTerm.H2);
        }
        throw new DomainException($"半期の形式が不正です(yyyy-H1/yyyy-H2): {value}");
    }

    public int CompareTo(FiscalHalf other) =>
        (Year * 2 + (int)Half).CompareTo(other.Year * 2 + (int)other.Half);

    public static bool operator <=(FiscalHalf a, FiscalHalf b) => a.CompareTo(b) <= 0;
    public static bool operator >=(FiscalHalf a, FiscalHalf b) => a.CompareTo(b) >= 0;
    public static bool operator <(FiscalHalf a, FiscalHalf b) => a.CompareTo(b) < 0;
    public static bool operator >(FiscalHalf a, FiscalHalf b) => a.CompareTo(b) > 0;

    public override string ToString() => $"{Year:D4}-H{(int)Half}";
}

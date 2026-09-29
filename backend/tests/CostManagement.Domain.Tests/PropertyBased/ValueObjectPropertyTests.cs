using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Shared;
using CsCheck;

namespace CostManagement.Domain.Tests.PropertyBased;

/// <summary>値オブジェクト(金額・半期・費目コード・半期内の月)の性質。</summary>
public class 値オブジェクトの性質
{
    private static readonly Gen<Money> 任意の金額 =
        Gen.Long[-10_000_000_000L, 10_000_000_000L].Select(v => new Money(v / 100m));

    [Fact]
    public void 金額の加算は交換法則と結合法則を満たしゼロが単位元になる()
    {
        Gen.Select(任意の金額, 任意の金額, 任意の金額).Sample((a, b, c) =>
        {
            Assert.Equal(a + b, b + a);
            Assert.Equal((a + b) + c, a + (b + c));
            Assert.Equal(a, a + Money.Zero);
        });
    }

    [Fact]
    public void 金額の減算は加算の逆演算になる()
    {
        Gen.Select(任意の金額, 任意の金額).Sample((a, b) =>
        {
            Assert.Equal(a, (a - b) + b);
            Assert.Equal(Money.Zero, a - a);
        });
    }

    [Fact]
    public void 金額の大小比較と負判定は値の比較と一致する()
    {
        Gen.Select(任意の金額, 任意の金額).Sample((a, b) =>
        {
            Assert.Equal(Math.Sign(a.Value.CompareTo(b.Value)), Math.Sign(a.CompareTo(b)));
            Assert.Equal(a.Value < 0m, a.IsNegative);
        });
    }

    private static readonly Gen<FiscalHalf> 任意の半期 =
        Gen.Select(Gen.Int[2000, 2100], Gen.OneOfConst(HalfTerm.H1, HalfTerm.H2))
            .Select((year, half) => new FiscalHalf(year, half));

    [Fact]
    public void 半期は文字列表現から元の値へ復元できる()
    {
        任意の半期.Sample(half => Assert.Equal(half, FiscalHalf.Parse(half.ToString())));
    }

    [Fact]
    public void 半期の順序は年度が先で同年度なら上期が先になる()
    {
        Gen.Select(任意の半期, 任意の半期).Sample((a, b) =>
        {
            var expected = (a.Year, (int)a.Half).CompareTo((b.Year, (int)b.Half));
            Assert.Equal(Math.Sign(expected), Math.Sign(a.CompareTo(b)));
            Assert.Equal(-Math.Sign(a.CompareTo(b)), Math.Sign(b.CompareTo(a)));
            Assert.Equal(a.CompareTo(b) < 0, a < b);
            Assert.Equal(a.CompareTo(b) <= 0, a <= b);
            Assert.Equal(a.CompareTo(b) > 0, a > b);
            Assert.Equal(a.CompareTo(b) >= 0, a >= b);
            Assert.Equal(a.CompareTo(b) == 0, a == b);
        });
    }

    [Fact]
    public void 範囲外の会計年度では半期を作れない()
    {
        Gen.Frequency((1, Gen.Int[int.MinValue, 1999]), (1, Gen.Int[2101, int.MaxValue]))
            .Sample(year => Assert.Throws<DomainException>(() => new FiscalHalf(year, HalfTerm.H1)));
    }

    [Fact]
    public void 半期の形式に合わない文字列は解析できない()
    {
        Gen.String.Where(s => !System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+-H[12]$"))
            .Sample(s => Assert.Throws<DomainException>(() => FiscalHalf.Parse(s)));
    }

    private static readonly Gen<string> 空白でない文字列 =
        Gen.String[Gen.Char.AlphaNumeric, 1, 12];

    private static readonly Gen<string> 空白 = Gen.OneOfConst("", " ", "\t", "  ");

    [Fact]
    public void 費目コードは前後の空白と大文字小文字を無視して同一視される()
    {
        Gen.Select(空白でない文字列, 空白, 空白).Sample((code, before, after) =>
        {
            var normalized = new CostElementCode(code);
            Assert.Equal(normalized, new CostElementCode(before + code.ToLowerInvariant() + after));
            Assert.Equal(normalized, new CostElementCode(normalized.Value)); // 正規化は冪等
            Assert.Equal(normalized.Value, normalized.Value.Trim().ToUpperInvariant());
        });
    }

    [Fact]
    public void 空白だけの費目コードは作れない()
    {
        空白.Sample(blank => Assert.Throws<DomainException>(() => new CostElementCode(blank)));
    }

    [Fact]
    public void 半期内の月として受け付けるのは1から6だけ()
    {
        Gen.Int.Sample(month =>
        {
            if (month is >= 1 and <= HalfMonths.Count)
                HalfMonths.Validate(month);
            else
                Assert.Throws<DomainException>(() => HalfMonths.Validate(month));
        });
    }
}

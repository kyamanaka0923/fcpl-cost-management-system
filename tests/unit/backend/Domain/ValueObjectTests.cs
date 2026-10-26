using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class FiscalHalfTests
{
    [Theory]
    [InlineData("2026-H1", 2026, HalfTerm.H1)]
    [InlineData("2026-H2", 2026, HalfTerm.H2)]
    public void 文字列から生成できる(string input, int year, HalfTerm half)
    {
        var fiscalHalf = FiscalHalf.Parse(input);

        Assert.Equal(year, fiscalHalf.Year);
        Assert.Equal(half, fiscalHalf.Half);
        Assert.Equal(input, fiscalHalf.ToString());
    }

    [Theory]
    [InlineData("2026-H3")]
    [InlineData("2026-1")]
    [InlineData("2026/H1")]
    [InlineData("2026")]
    [InlineData("abc")]
    [InlineData("")]
    public void 不正な形式は拒否される(string input)
    {
        Assert.Throws<DomainException>(() => FiscalHalf.Parse(input));
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void 不正な年度は拒否される(int year)
    {
        Assert.Throws<DomainException>(() => new FiscalHalf(year, HalfTerm.H1));
    }

    [Fact]
    public void 大小比較ができる()
    {
        Assert.True(new FiscalHalf(2026, HalfTerm.H1) < new FiscalHalf(2026, HalfTerm.H2));
        Assert.True(new FiscalHalf(2026, HalfTerm.H2) < new FiscalHalf(2027, HalfTerm.H1));
        Assert.True(new FiscalHalf(2026, HalfTerm.H1) <= new FiscalHalf(2026, HalfTerm.H1));
    }
}

public class MoneyTests
{
    [Fact]
    public void 加減乗算ができる()
    {
        var a = new Money(1_000m);
        var b = new Money(300m);

        Assert.Equal(1_300m, (a + b).Value);
        Assert.Equal(700m, (a - b).Value);
        Assert.Equal(2_500m, (a * 2.5m).Value);
    }

    [Fact]
    public void 値が同じなら等価である()
    {
        Assert.Equal(new Money(500m), new Money(500m));
    }
}

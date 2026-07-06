using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class AccountingPeriodTests
{
    [Theory]
    [InlineData("2026-04", 2026, 4)]
    [InlineData("2026-12", 2026, 12)]
    public void 文字列から生成できる(string input, int year, int month)
    {
        var period = AccountingPeriod.Parse(input);

        Assert.Equal(year, period.Year);
        Assert.Equal(month, period.Month);
        Assert.Equal(input, period.ToString());
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026/04")]
    [InlineData("abc")]
    public void 不正な形式は拒否される(string input)
    {
        Assert.Throws<DomainException>(() => AccountingPeriod.Parse(input));
    }

    [Fact]
    public void 大小比較ができる()
    {
        Assert.True(new AccountingPeriod(2026, 4) < new AccountingPeriod(2026, 5));
        Assert.True(new AccountingPeriod(2026, 12) < new AccountingPeriod(2027, 1));
        Assert.True(new AccountingPeriod(2026, 4) <= new AccountingPeriod(2026, 4));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(10, 4)]
    public void 四半期を判定できる(int month, int expectedQuarter)
    {
        Assert.Equal(expectedQuarter, new AccountingPeriod(2026, month).Quarter);
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

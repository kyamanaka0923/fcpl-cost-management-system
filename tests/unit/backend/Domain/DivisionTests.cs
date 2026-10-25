using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class DivisionTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 部はコードと名称で作成される()
    {
        var division = Division.Create("SALES", "営業本部", Now);

        Assert.Equal("SALES", division.Code);
        Assert.Equal("営業本部", division.Name);
        Assert.Equal(Now, division.CreatedAt);
    }

    [Theory]
    [InlineData("", "営業本部")]
    [InlineData("  ", "営業本部")]
    [InlineData("SALES", "")]
    [InlineData("SALES", "  ")]
    public void コードと名称は必須である(string code, string name)
    {
        Assert.Throws<DomainException>(() => Division.Create(code, name, Now));
    }

    [Fact]
    public void 名称を変更できる()
    {
        var division = Division.Create("SALES", "営業本部", Now);
        division.Rename("第一営業本部");
        Assert.Equal("第一営業本部", division.Name);
    }
}

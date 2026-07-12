using CostManagement.Application.Common;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 部の予実サマリ(配下課の合計)。</summary>
public class 部の予実サマリ : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 配下課の予実を合計して部サマリを返す()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        var pj2 = await _fx.案件を作成(課2.Id, "PJ-2", "案件2");

        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m),
            ("Processing", pj1.Id, null, 1_000_000m),
            ("PeriodCost", null, "PERSONNEL", 300_000m));
        await _fx.承認済み予算を作成(課2.Id, "2026-H1",
            ("Revenue", pj2.Id, null, 2_000_000m),
            ("Outsourcing", pj2.Id, null, 800_000m));

        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj1.Id, null, 3_200_000m, null));
        await _fx.Actuals.RecordAsync(課2.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj2.Id, null, 1_800_000m, null));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(5_000_000m, summary.PlannedRevenue);
        Assert.Equal(5_000_000m, summary.ActualRevenue);    // 320万 + 180万
        Assert.Equal(2_100_000m, summary.PlannedCost);      // (100+30) + 80 万
        Assert.Equal(2_900_000m, summary.PlannedProfit);    // 500万 − 210万

        Assert.Equal(2, summary.DepartmentLines.Count);
        Assert.All(summary.DepartmentLines, l => Assert.True(l.HasApprovedBudget));
        // 部合計 = 課別内訳の合計
        Assert.Equal(summary.PlannedRevenue, summary.DepartmentLines.Sum(l => l.PlannedRevenue));
        Assert.Equal(summary.ActualProfit, summary.DepartmentLines.Sum(l => l.ActualProfit));
    }

    [Fact]
    public async Task 課別内訳には区分別の予算内訳が含まれる()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m),
            ("Processing", pj1.Id, null, 1_000_000m),
            ("Outsourcing", pj1.Id, null, 500_000m),
            ("PeriodCost", null, "PERSONNEL", 300_000m));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        var line = summary.DepartmentLines.Single(l => l.DepartmentCode == "DEV-1");
        Assert.Equal(4, line.Categories.Count); // 4区分すべて
        Assert.Equal(1_000_000m,
            line.Categories.Single(c => c.Category == "Processing").PlannedAmount);
        Assert.Equal(500_000m,
            line.Categories.Single(c => c.Category == "Outsourcing").PlannedAmount);
        Assert.Equal(300_000m,
            line.Categories.Single(c => c.Category == "PeriodCost").PlannedAmount);
        // 区分別内訳の合計 = その課のコスト予算
        var costCategories = line.Categories.Where(c => c.Category != "Revenue");
        Assert.Equal(line.PlannedCost, costCategories.Sum(c => c.PlannedAmount));
    }

    [Fact]
    public async Task 未策定の課の区分別内訳は空になる()
    {
        var div = await _fx.部を作成();
        _ = await _fx.課を作成(div.Id, "DEV-1", "開発1課"); // 予算未策定

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Empty(summary.DepartmentLines.Single().Categories);
    }

    [Fact]
    public async Task 承認済み予算のない課は未策定として内訳に出て合計に含まれない()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        _ = await _fx.課を作成(div.Id, "DEV-2", "開発2課"); // 予算未策定
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(3_000_000m, summary.PlannedRevenue); // 課1のみ
        Assert.Equal(2, summary.DepartmentLines.Count);
        var 未策定 = summary.DepartmentLines.Single(l => l.DepartmentCode == "DEV-2");
        Assert.False(未策定.HasApprovedBudget);
        Assert.Equal(0m, 未策定.PlannedRevenue);
    }

    [Fact]
    public async Task 存在しない部のサマリはエラーになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Analysis.GetDivisionBudgetSummaryAsync(Guid.NewGuid(), "2026-H1"));
    }

    [Fact]
    public async Task 部コードは重複できない()
    {
        await _fx.部を作成("SALES", "営業本部");
        await Assert.ThrowsAsync<DomainException>(() => _fx.部を作成("SALES", "別の部"));
    }
}

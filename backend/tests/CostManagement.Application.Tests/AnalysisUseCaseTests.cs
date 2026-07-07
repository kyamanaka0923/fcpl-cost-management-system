using CostManagement.Application.Common;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 予実差異分析・バージョン比較・損益。</summary>
public class 差異分析と損益 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task<Guid> 予算と実績が揃ったプロジェクトを準備()
    {
        var project = await _fx.プロジェクトを作成();
        await _fx.承認済み売上予算を作成(project.Id,
            ("案件A", "2026-04", 2_000_000m),
            ("案件B", "2026-04", 1_000_000m));
        await _fx.承認済み原価予算を作成(project.Id,
            ("LAB-SE", "案件A", "2026-04", 1_400_000m),
            ("LAB-SE", "案件B", "2026-04", 700_000m),
            ("OVH-COM", null, "2026-04", 300_000m));

        await _fx.ActualRevenues.RecordAsync(project.Id,
            new RecordRevenueRequest("案件A", "2026-04", 2_100_000m, null));
        await _fx.ActualRevenues.RecordAsync(project.Id,
            new RecordRevenueRequest("案件B", "2026-04", 900_000m, null));
        await _fx.ActualCosts.RecordAsync(project.Id,
            new RecordActualRequest("LAB-SE", "案件A", "2026-04", 1_480_000m, null));
        await _fx.ActualCosts.RecordAsync(project.Id,
            new RecordActualRequest("LAB-SE", "案件B", "2026-04", 650_000m, null));
        await _fx.ActualCosts.RecordAsync(project.Id,
            new RecordActualRequest("OVH-COM", null, "2026-04", 320_000m, null));
        return project.Id;
    }

    [Fact]
    public async Task 原価差異は最新の承認済み予算を基準に算出される()
    {
        var projectId = await 予算と実績が揃ったプロジェクトを準備();

        var report = await _fx.Analysis.AnalyzeVarianceAsync(projectId, null, null, null);

        Assert.Equal(2_400_000m, report.TotalPlannedAmount);
        Assert.Equal(2_450_000m, report.TotalActualAmount);
        Assert.Equal(50_000m, report.TotalVariance); // 原価超過 = 不利

        var 案件A = report.Lines.Single(l => l.RevenueItem == "案件A");
        Assert.Equal(80_000m, 案件A.TotalVariance);
        Assert.True(案件A.IsAdverse);
    }

    [Fact]
    public async Task 売上差異は売上超過が有利差異として算出される()
    {
        var projectId = await 予算と実績が揃ったプロジェクトを準備();

        var report = await _fx.Analysis.AnalyzeRevenueVarianceAsync(projectId, null, null, null);

        Assert.Equal(3_000_000m, report.TotalPlannedAmount);
        Assert.Equal(3_000_000m, report.TotalActualAmount);
        Assert.True(report.Lines.Single(l => l.ItemName == "案件A").IsFavorable);
        Assert.False(report.Lines.Single(l => l.ItemName == "案件B").IsFavorable);
    }

    [Fact]
    public async Task 承認済み予算がなければ差異分析はエラーになる()
    {
        var project = await _fx.プロジェクトを作成();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Analysis.AnalyzeVarianceAsync(project.Id, null, null, null));
    }

    [Fact]
    public async Task 期間を指定して差異分析を絞り込める()
    {
        var project = await _fx.プロジェクトを作成();
        await _fx.承認済み原価予算を作成(project.Id,
            ("LAB-SE", "案件A", "2026-04", 100_000m),
            ("LAB-SE", "案件A", "2026-05", 200_000m));

        var report = await _fx.Analysis.AnalyzeVarianceAsync(project.Id, null, "2026-05", "2026-05");

        var line = Assert.Single(report.Lines);
        Assert.Equal("2026-05", line.Period);
        Assert.Equal(200_000m, line.PlannedAmount);
    }

    [Fact]
    public async Task 予算バージョン間の増減を比較できる()
    {
        var project = await _fx.プロジェクトを作成();
        await _fx.承認済み原価予算を作成(project.Id, ("LAB-SE", "案件A", "2026-04", 500_000m));

        var v2 = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("第2四半期改定", null));
        await _fx.CostPlans.UpsertLineAsync(v2.Id,
            new UpsertPlanLineRequest("LAB-SE", "案件A", "2026-04", 620_000m));
        await _fx.CostPlans.ApproveAsync(v2.Id);

        var comparison = await _fx.Analysis.ComparePlansAsync(project.Id, 1, 2);

        Assert.Equal(120_000m, comparison.TotalDifference);
        Assert.Equal(120_000m, Assert.Single(comparison.Lines).Difference);
    }

    [Fact]
    public async Task 損益サマリで品目別の粗利と共通費が突き合わされる()
    {
        var projectId = await 予算と実績が揃ったプロジェクトを準備();

        var profit = await _fx.Analysis.GetProfitSummaryAsync(projectId, null, null);

        // 全体: 売上300万/原価245万 → 粗利55万(予算60万から5万悪化)
        Assert.Equal(600_000m, profit.PlannedProfit);
        Assert.Equal(550_000m, profit.ActualProfit);
        Assert.Equal(-50_000m, profit.ProfitVariance);

        // 品目別
        var 案件A = profit.ItemLines.Single(l => l.ItemName == "案件A");
        Assert.Equal(620_000m, 案件A.ActualProfit); // 210万 − 148万

        var 共通費 = profit.ItemLines.Single(l => l.ItemName is null);
        Assert.Equal(-320_000m, 共通費.ActualProfit);

        // 品目別の合計は全体と一致する
        Assert.Equal(profit.ActualProfit, profit.ItemLines.Sum(l => l.ActualProfit));
    }

    [Fact]
    public async Task 売上対応品目の候補一覧は売上予算と売上実績から集約される()
    {
        var project = await _fx.プロジェクトを作成();
        await _fx.承認済み売上予算を作成(project.Id, ("案件A", "2026-04", 1_000_000m));
        await _fx.ActualRevenues.RecordAsync(project.Id,
            new RecordRevenueRequest("スポット案件", "2026-04", 100_000m, null));

        var items = await _fx.Analysis.ListRevenueItemsAsync(project.Id);

        Assert.Equal(["スポット案件", "案件A"], items);
    }
}

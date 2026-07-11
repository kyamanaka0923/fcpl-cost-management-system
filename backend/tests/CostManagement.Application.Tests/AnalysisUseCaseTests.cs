using CostManagement.Application.Common;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 予実差異分析・バージョン比較・損益。</summary>
public class 差異分析と損益 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task<(Guid DeptId, Guid ProjectAId, Guid ProjectBId)> 予算と実績が揃った課を準備()
    {
        var dept = await _fx.課を作成();
        var projectA = await _fx.案件を作成(dept.Id, "PJ-A", "案件A");
        var projectB = await _fx.案件を作成(dept.Id, "PJ-B", "案件B");
        await _fx.承認済み予算を作成(dept.Id, "2026-H1",
            ("Revenue", projectA.Id, null, 2_000_000m),
            ("Revenue", projectB.Id, null, 1_000_000m),
            ("Processing", projectA.Id, null, 1_400_000m),
            ("Outsourcing", projectB.Id, null, 700_000m),
            ("PeriodCost", null, "PERSONNEL", 300_000m));

        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Revenue", projectA.Id, null, 2_100_000m, null));
        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Revenue", projectB.Id, null, 900_000m, null));
        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Processing", projectA.Id, null, 1_480_000m, null));
        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Outsourcing", projectB.Id, null, 650_000m, null));
        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "PeriodCost", null, "PERSONNEL", 320_000m, null));
        return (dept.Id, projectA.Id, projectB.Id);
    }

    [Fact]
    public async Task 差異は最新の承認済み予算を基準に区分別に算出される()
    {
        var (deptId, projectAId, _) = await 予算と実績が揃った課を準備();

        var report = await _fx.Analysis.AnalyzeVarianceAsync(deptId, "2026-H1", null);

        Assert.Equal(3_000_000m, report.PlannedRevenue);
        Assert.Equal(3_000_000m, report.ActualRevenue);
        Assert.Equal(2_400_000m, report.PlannedCost);
        Assert.Equal(2_450_000m, report.ActualCost);
        Assert.Equal(50_000m, report.CostVariance); // コスト超過 = 不利

        var processing = report.Categories.Single(c => c.Category == "Processing");
        var 案件A = processing.Lines.Single(l => l.ProjectId == projectAId);
        Assert.Equal(80_000m, 案件A.Variance);
        Assert.True(案件A.IsAdverse);
        Assert.Equal("案件A", 案件A.ProjectName); // 案件名が解決される
    }

    [Fact]
    public async Task 売上差異は売上超過が有利差異として算出される()
    {
        var (deptId, projectAId, projectBId) = await 予算と実績が揃った課を準備();

        var report = await _fx.Analysis.AnalyzeVarianceAsync(deptId, "2026-H1", null);

        var revenue = report.Categories.Single(c => c.Category == "Revenue");
        Assert.True(revenue.Lines.Single(l => l.ProjectId == projectAId).IsFavorable);
        Assert.False(revenue.Lines.Single(l => l.ProjectId == projectBId).IsFavorable);
    }

    [Fact]
    public async Task 承認済み予算がなければ差異分析はエラーになる()
    {
        var dept = await _fx.課を作成();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Analysis.AnalyzeVarianceAsync(dept.Id, "2026-H1", null));
    }

    [Fact]
    public async Task 損益サマリは全体と案件別に算出され期間費用は課共通になる()
    {
        var (deptId, projectAId, projectBId) = await 予算と実績が揃った課を準備();

        var report = await _fx.Analysis.GetProfitSummaryAsync(deptId, "2026-H1", null);

        // 全体: 売上300万 − 総コスト240万 = 60万(計画)
        Assert.Equal(600_000m, report.PlannedProfit);
        Assert.Equal(550_000m, report.ActualProfit);
        Assert.Equal(300_000m, report.PlannedPeriodCost);
        Assert.Equal(0.2m, report.PlannedMarginRate);

        // 案件別損益に期間費用は含めない
        var 案件A = report.ProjectLines.Single(l => l.ProjectId == projectAId);
        Assert.Equal(600_000m, 案件A.PlannedProfit);  // 200万 − 140万
        Assert.Equal("PJ-A", 案件A.ProjectCode);
        var 案件B = report.ProjectLines.Single(l => l.ProjectId == projectBId);
        Assert.Equal(300_000m, 案件B.PlannedProfit);  // 100万 − 70万

        // 案件別損益の合計 − 期間費用 = 全体の損益
        Assert.Equal(report.PlannedProfit,
            report.ProjectLines.Sum(l => l.PlannedProfit) - report.PlannedPeriodCost);
    }

    [Fact]
    public async Task バージョン比較で改定の増減を確認できる()
    {
        var (deptId, projectAId, _) = await 予算と実績が揃った課を準備();

        var v2 = await _fx.Budgets.CreateDraftAsync(deptId,
            new CreateBudgetRequest("2026-H1", "上期見直し"));
        await _fx.Budgets.UpsertLineAsync(v2.Id,
            new UpsertBudgetLineRequest("Revenue", projectAId, null, 2_500_000m));
        await _fx.Budgets.ApproveAsync(v2.Id);

        var report = await _fx.Analysis.CompareBudgetsAsync(deptId, "2026-H1", 1, 2);

        Assert.Equal("当初予算", report.BaseLabel);
        Assert.Equal("上期見直し", report.TargetLabel);
        var revenue = report.Categories.Single(c => c.Category == "Revenue");
        Assert.Equal(500_000m, revenue.Difference);
    }

    [Fact]
    public async Task 存在しないバージョンの比較はエラーになる()
    {
        var (deptId, _, _) = await 予算と実績が揃った課を準備();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Analysis.CompareBudgetsAsync(deptId, "2026-H1", 1, 99));
    }
}

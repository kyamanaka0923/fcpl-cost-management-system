using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class ProfitAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ProjectId Pid = ProjectId.New();
    private static readonly CostElementCode Labor = new("LAB-SE");
    private static readonly AccountingPeriod Apr = new(2026, 4);

    private readonly ProfitAnalysisService _service = new();
    private readonly RevenueVarianceAnalysisService _revenueAnalysis = new();
    private readonly VarianceAnalysisService _costAnalysis = new();

    private ProfitReport Analyze(
        (string Item, decimal Amount)[] revenuePlan,
        (string Item, decimal Amount)[] revenueActuals,
        (string? Item, decimal Amount)[] costPlan,
        (string? Item, decimal Amount)[] costActuals)
    {
        var rp = RevenuePlan.CreateInitial(Pid, "売上予算", Now);
        foreach (var (item, amount) in revenuePlan)
            rp.UpsertLine(item, Apr, new Money(amount));
        rp.Approve(Now);

        var cp = CostPlan.CreateInitial(Pid, "原価予算", Now);
        foreach (var (item, amount) in costPlan)
            cp.UpsertLine(Labor, item, Apr, new Money(amount));
        cp.Approve(Now);

        var ra = revenueActuals
            .Select(a => ActualRevenue.Record(Pid, a.Item, Apr, new Money(a.Amount), null, Now))
            .ToList();
        var ca = costActuals
            .Select(a => ActualCost.Record(Pid, Labor, a.Item, Apr, new Money(a.Amount), null, Now))
            .ToList();

        return _service.Analyze(_revenueAnalysis.Analyze(rp, ra), _costAnalysis.Analyze(cp, ca));
    }

    [Fact]
    public void 品目別に売上と対応原価が突き合わされる()
    {
        var report = Analyze(
            revenuePlan: [("案件A", 2_000_000m), ("案件B", 1_000_000m)],
            revenueActuals: [("案件A", 2_100_000m), ("案件B", 900_000m)],
            costPlan: [("案件A", 1_400_000m), ("案件B", 700_000m)],
            costActuals: [("案件A", 1_500_000m), ("案件B", 650_000m)]);

        Assert.Equal(2, report.ItemLines.Count);

        var itemA = report.ItemLines.Single(l => l.ItemName == "案件A");
        Assert.Equal(600_000m, itemA.PlannedProfit);  // 200万 − 140万
        Assert.Equal(600_000m, itemA.ActualProfit);   // 210万 − 150万
        Assert.Equal(0m, itemA.ProfitVariance);

        var itemB = report.ItemLines.Single(l => l.ItemName == "案件B");
        Assert.Equal(300_000m, itemB.PlannedProfit);  // 100万 − 70万
        Assert.Equal(250_000m, itemB.ActualProfit);   // 90万 − 65万
        Assert.Equal(-50_000m, itemB.ProfitVariance);
    }

    [Fact]
    public void 売上対応品目のない原価は共通費として報告される()
    {
        var report = Analyze(
            revenuePlan: [("案件A", 2_000_000m)],
            revenueActuals: [("案件A", 2_000_000m)],
            costPlan: [("案件A", 1_400_000m), (null, 300_000m)],
            costActuals: [("案件A", 1_400_000m), (null, 350_000m)]);

        var common = report.ItemLines.Single(l => l.ItemName is null);
        Assert.Equal(0m, common.PlannedRevenue);
        Assert.Equal(300_000m, common.PlannedCost);
        Assert.Equal(-300_000m, common.PlannedProfit);
        Assert.Equal(-350_000m, common.ActualProfit);

        // 共通費は末尾に置かれる
        Assert.Null(report.ItemLines[^1].ItemName);
    }

    [Fact]
    public void 品目別損益の合計は全体の損益と一致する()
    {
        var report = Analyze(
            revenuePlan: [("案件A", 2_000_000m), ("案件B", 1_000_000m)],
            revenueActuals: [("案件A", 2_100_000m)],
            costPlan: [("案件A", 1_400_000m), (null, 300_000m)],
            costActuals: [("案件A", 1_450_000m), (null, 320_000m)]);

        Assert.Equal(report.PlannedProfit, report.ItemLines.Sum(l => l.PlannedProfit));
        Assert.Equal(report.ActualProfit, report.ItemLines.Sum(l => l.ActualProfit));
        Assert.Equal(report.PlannedProfit, 3_000_000m - 1_700_000m);
        Assert.Equal(report.ActualProfit, 2_100_000m - 1_770_000m);
    }

    [Fact]
    public void 粗利率が算出される()
    {
        var report = Analyze(
            revenuePlan: [("案件A", 2_000_000m)],
            revenueActuals: [("案件A", 2_500_000m)],
            costPlan: [("案件A", 1_500_000m)],
            costActuals: [("案件A", 1_500_000m)]);

        Assert.Equal(0.25m, report.PlannedMarginRate);
        Assert.Equal(0.4m, report.ActualMarginRate);
    }

    [Fact]
    public void 売上ゼロの場合の粗利率はnullになる()
    {
        var report = Analyze(
            revenuePlan: [("案件A", 2_000_000m)],
            revenueActuals: [],
            costPlan: [("案件A", 1_500_000m)],
            costActuals: []);

        Assert.NotNull(report.PlannedMarginRate);
        Assert.Null(report.ActualMarginRate);
    }
}

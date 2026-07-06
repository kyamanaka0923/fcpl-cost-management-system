using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class VarianceAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ProjectId Pid = ProjectId.New();
    private static readonly CostElementCode Labor = new("LAB-SE");
    private static readonly CostElementCode Sub = new("SUB-DEV");
    private static readonly AccountingPeriod Apr = new(2026, 4);
    private static readonly AccountingPeriod May = new(2026, 5);

    private readonly VarianceAnalysisService _service = new();

    private static CostPlan ApprovedPlan(params (CostElementCode Code, string? Item,
        AccountingPeriod Period, decimal Amount)[] lines)
    {
        var plan = CostPlan.CreateInitial(Pid, "当初予算", Now);
        foreach (var (code, item, period, amount) in lines)
            plan.UpsertLine(code, item, period, new Money(amount));
        plan.Approve(Now);
        return plan;
    }

    [Fact]
    public void 差異は実績金額と予算金額の差として算出される()
    {
        var plan = ApprovedPlan((Labor, "案件A", Apr, 500_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(560_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(60_000m, line.TotalVariance);
        Assert.True(line.IsAdverse);
        Assert.Equal("案件A", line.RevenueItem);
    }

    [Fact]
    public void 売上対応品目が異なる原価は別の行として差異が算出される()
    {
        var plan = ApprovedPlan(
            (Labor, "案件A", Apr, 500_000m),
            (Labor, "案件B", Apr, 300_000m),
            (Labor, null, Apr, 100_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(520_000m), null, Now),
            ActualCost.Record(Pid, Labor, "案件B", Apr, new Money(250_000m), null, Now),
            ActualCost.Record(Pid, Labor, null, Apr, new Money(120_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        Assert.Equal(3, report.Lines.Count);
        Assert.Equal(20_000m, report.Lines.Single(l => l.RevenueItem == "案件A").TotalVariance);
        Assert.Equal(-50_000m, report.Lines.Single(l => l.RevenueItem == "案件B").TotalVariance);
        Assert.Equal(20_000m, report.Lines.Single(l => l.RevenueItem is null).TotalVariance);
    }

    [Fact]
    public void 同一キーの複数実績は合算される()
    {
        var plan = ApprovedPlan((Labor, "案件A", Apr, 500_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(300_000m), "前半", Now),
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(220_000m), "後半", Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(520_000m, line.ActualAmount);
        Assert.Equal(20_000m, line.TotalVariance);
    }

    [Fact]
    public void 予算のない実績は予定外として報告される()
    {
        var plan = ApprovedPlan((Labor, "案件A", Apr, 500_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Sub, "案件A", Apr, new Money(80_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var unplanned = report.Lines.Single(l => l.ElementCode == "SUB-DEV");
        Assert.True(unplanned.IsUnplanned);
        Assert.Equal(80_000m, unplanned.TotalVariance);
    }

    [Fact]
    public void 実績のない予算明細も差異として報告される()
    {
        var plan = ApprovedPlan((Labor, "案件A", Apr, 500_000m));

        var report = _service.Analyze(plan, []);

        var line = Assert.Single(report.Lines);
        Assert.Equal(-500_000m, line.TotalVariance); // 未消化 = 有利差異
        Assert.False(line.IsAdverse);
    }

    [Fact]
    public void 期間で絞り込みできる()
    {
        var plan = ApprovedPlan(
            (Labor, "案件A", Apr, 500_000m),
            (Labor, "案件A", May, 500_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(500_000m), null, Now),
            ActualCost.Record(Pid, Labor, "案件A", May, new Money(450_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals, from: May, to: May);

        var line = Assert.Single(report.Lines);
        Assert.Equal(May, line.Period);
        Assert.Equal(-50_000m, report.TotalVariance);
    }

    [Fact]
    public void レポート合計は明細の合計と一致する()
    {
        var plan = ApprovedPlan(
            (Labor, "案件A", Apr, 500_000m),
            (Sub, "案件B", Apr, 200_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Labor, "案件A", Apr, new Money(510_000m), null, Now),
            ActualCost.Record(Pid, Sub, "案件B", Apr, new Money(180_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        Assert.Equal(700_000m, report.TotalPlannedAmount);
        Assert.Equal(690_000m, report.TotalActualAmount);
        Assert.Equal(-10_000m, report.TotalVariance);
        Assert.Equal(report.TotalVariance, report.Lines.Sum(l => l.TotalVariance));
    }
}

public class PlanComparisonTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CostElementCode Labor = new("LAB-SE");
    private static readonly AccountingPeriod Apr = new(2026, 4);
    private static readonly AccountingPeriod May = new(2026, 5);

    [Fact]
    public void 予算バージョン間の変動を明細単位で比較できる()
    {
        var pid = ProjectId.New();
        var v1 = CostPlan.CreateInitial(pid, "当初予算", Now);
        v1.UpsertLine(Labor, "案件A", Apr, new Money(500_000m));
        v1.UpsertLine(Labor, "案件A", May, new Money(500_000m));
        v1.Approve(Now);

        var v2 = CostPlan.ReviseFrom(v1, 2, "第2四半期改定", Now);
        v2.UpsertLine(Labor, "案件A", May, new Money(620_000m)); // 5月分を増額改定

        var report = new PlanComparisonService().Compare(v1, v2);

        Assert.Equal(2, report.Lines.Count);
        var may = report.Lines.Single(l => l.Period == May);
        Assert.Equal(500_000m, may.BaseAmount);
        Assert.Equal(620_000m, may.TargetAmount);
        Assert.Equal(120_000m, may.Difference);
        Assert.Equal(120_000m, report.TotalDifference);
        Assert.Equal("案件A", may.RevenueItem);
    }

    [Fact]
    public void 異なるプロジェクトの予算は比較できない()
    {
        var a = CostPlan.CreateInitial(ProjectId.New(), "A", Now);
        var b = CostPlan.CreateInitial(ProjectId.New(), "B", Now);

        Assert.Throws<DomainException>(() => new PlanComparisonService().Compare(a, b));
    }
}

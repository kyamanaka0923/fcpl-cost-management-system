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
    private static readonly CostElementCode Material = new("MAT-RAW");
    private static readonly AccountingPeriod Apr = new(2026, 4);
    private static readonly AccountingPeriod May = new(2026, 5);

    private readonly VarianceAnalysisService _service = new();

    private static CostPlan ApprovedPlan(params (CostElementCode Code, AccountingPeriod Period,
        decimal Qty, decimal Price)[] lines)
    {
        var plan = CostPlan.CreateInitial(Pid, "当初予算", Now);
        foreach (var (code, period, qty, price) in lines)
            plan.UpsertLine(code, period, qty, new Money(price));
        plan.Approve(Now);
        return plan;
    }

    [Fact]
    public void 総差異は価格差異と数量差異に分解される()
    {
        // 予定: 100個 × 500円 = 50,000円
        // 実績: 110個 × 520円 = 57,200円
        var plan = ApprovedPlan((Material, Apr, 100m, 500m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Material, Apr, 110m, new Money(520m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(7_200m, line.TotalVariance);
        // 価格差異 = (520 - 500) × 110 = 2,200
        Assert.Equal(2_200m, line.PriceVariance);
        // 数量差異 = (110 - 100) × 500 = 5,000
        Assert.Equal(5_000m, line.QuantityVariance);
        // 分解の整合性: 価格差異 + 数量差異 = 総差異
        Assert.Equal(line.TotalVariance, line.PriceVariance + line.QuantityVariance);
        Assert.True(line.IsAdverse);
    }

    [Fact]
    public void 同一費目期間の複数実績は合算され単価は加重平均になる()
    {
        var plan = ApprovedPlan((Material, Apr, 100m, 500m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Material, Apr, 60m, new Money(500m), "前半", Now),
            ActualCost.Record(Pid, Material, Apr, 40m, new Money(550m), "後半", Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(100m, line.ActualQuantity);
        Assert.Equal(52_000m, line.ActualAmount);
        Assert.Equal(520m, line.ActualUnitPrice); // (60×500 + 40×550) / 100
    }

    [Fact]
    public void 予算のない実績は予定外として報告される()
    {
        var plan = ApprovedPlan((Material, Apr, 100m, 500m));
        var unplannedElement = new CostElementCode("EXP-OTH");
        var actuals = new[]
        {
            ActualCost.Record(Pid, unplannedElement, Apr, 1m, new Money(30_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var unplanned = report.Lines.Single(l => l.ElementCode == "EXP-OTH");
        Assert.True(unplanned.IsUnplanned);
        Assert.Null(unplanned.PriceVariance);
        Assert.Null(unplanned.QuantityVariance);
        Assert.Equal(30_000m, unplanned.TotalVariance);
    }

    [Fact]
    public void 実績のない予算明細も差異として報告される()
    {
        var plan = ApprovedPlan((Material, Apr, 100m, 500m));

        var report = _service.Analyze(plan, []);

        var line = Assert.Single(report.Lines);
        Assert.Equal(-50_000m, line.TotalVariance); // 未消化 = 有利差異
        Assert.False(line.IsAdverse);
    }

    [Fact]
    public void 期間で絞り込みできる()
    {
        var plan = ApprovedPlan(
            (Material, Apr, 100m, 500m),
            (Material, May, 100m, 500m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Material, Apr, 100m, new Money(500m), null, Now),
            ActualCost.Record(Pid, Material, May, 90m, new Money(500m), null, Now),
        };

        var report = _service.Analyze(plan, actuals, from: May, to: May);

        var line = Assert.Single(report.Lines);
        Assert.Equal(May, line.Period);
        Assert.Equal(-5_000m, report.TotalVariance);
    }

    [Fact]
    public void レポート合計は明細の合計と一致する()
    {
        var plan = ApprovedPlan(
            (Material, Apr, 100m, 500m),
            (new CostElementCode("LAB-DIR"), Apr, 50m, 3_000m));
        var actuals = new[]
        {
            ActualCost.Record(Pid, Material, Apr, 110m, new Money(510m), null, Now),
            ActualCost.Record(Pid, new CostElementCode("LAB-DIR"), Apr, 45m, new Money(3_100m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        Assert.Equal(200_000m, report.TotalPlannedAmount);
        Assert.Equal(195_600m, report.TotalActualAmount);
        Assert.Equal(-4_400m, report.TotalVariance);
        Assert.Equal(report.TotalVariance, report.Lines.Sum(l => l.TotalVariance));
    }
}

public class PlanComparisonTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CostElementCode Material = new("MAT-RAW");
    private static readonly AccountingPeriod Apr = new(2026, 4);
    private static readonly AccountingPeriod May = new(2026, 5);

    [Fact]
    public void 予算バージョン間の変動を明細単位で比較できる()
    {
        var pid = ProjectId.New();
        var v1 = CostPlan.CreateInitial(pid, "当初予算", Now);
        v1.UpsertLine(Material, Apr, 100m, new Money(500m));
        v1.UpsertLine(Material, May, 100m, new Money(500m));
        v1.Approve(Now);

        var v2 = CostPlan.ReviseFrom(v1, 2, "第2四半期改定", Now);
        v2.UpsertLine(Material, May, 120m, new Money(510m)); // 5月分を増額改定

        var report = new PlanComparisonService().Compare(v1, v2);

        Assert.Equal(2, report.Lines.Count);
        var may = report.Lines.Single(l => l.Period == May);
        Assert.Equal(50_000m, may.BaseAmount);
        Assert.Equal(61_200m, may.TargetAmount);
        Assert.Equal(11_200m, may.Difference);
        Assert.Equal(11_200m, report.TotalDifference);
    }

    [Fact]
    public void 異なるプロジェクトの予算は比較できない()
    {
        var a = CostPlan.CreateInitial(ProjectId.New(), "A", Now);
        var b = CostPlan.CreateInitial(ProjectId.New(), "B", Now);

        Assert.Throws<DomainException>(() => new PlanComparisonService().Compare(a, b));
    }
}

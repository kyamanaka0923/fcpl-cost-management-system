using CostManagement.Domain.Analysis;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class RevenuePlanTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly AccountingPeriod Apr = new(2026, 4);

    private static RevenuePlan NewDraft() =>
        RevenuePlan.CreateInitial(ProjectId.New(), "当初売上予算", Now);

    [Fact]
    public void 当初売上予算はバージョン1のドラフトとして作成される()
    {
        var plan = NewDraft();

        Assert.Equal(1, plan.Version);
        Assert.Equal(PlanStatus.Draft, plan.Status);
        Assert.Empty(plan.Lines);
    }

    [Fact]
    public void 同一品目同一期間の明細は上書きされる()
    {
        var plan = NewDraft();
        plan.UpsertLine("製品A", Apr, 100m, new Money(2_000m));
        plan.UpsertLine("製品A", Apr, 120m, new Money(1_900m));

        var line = Assert.Single(plan.Lines);
        Assert.Equal(120m, line.Quantity);
        Assert.Equal(228_000m, plan.TotalAmount.Value);
    }

    [Fact]
    public void 承認済みの売上予算は編集できない()
    {
        var plan = NewDraft();
        plan.UpsertLine("製品A", Apr, 100m, new Money(2_000m));
        plan.Approve(Now);

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine("製品A", Apr, 200m, new Money(2_000m)));
    }

    [Fact]
    public void 改定版は明細を引き継いだ新バージョンのドラフトになる()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine("製品A", Apr, 100m, new Money(2_000m));
        basePlan.Approve(Now);

        var revised = RevenuePlan.ReviseFrom(basePlan, 2, "第2四半期改定", Now);

        Assert.Equal(2, revised.Version);
        Assert.Equal(PlanStatus.Draft, revised.Status);
        var line = Assert.Single(revised.Lines);
        Assert.Equal(100m, line.Quantity);
        Assert.NotEqual(basePlan.Lines[0].Id, line.Id);
    }

    [Fact]
    public void 品目名は必須で前後の空白は除去される()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine("  ", Apr, 1m, new Money(100m)));

        plan.UpsertLine(" 製品A ", Apr, 1m, new Money(100m));
        Assert.Equal("製品A", plan.Lines[0].ItemName);
    }
}

public class RevenueVarianceAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ProjectId Pid = ProjectId.New();
    private static readonly AccountingPeriod Apr = new(2026, 4);

    private readonly RevenueVarianceAnalysisService _service = new();

    private static RevenuePlan ApprovedPlan(params (string Item, AccountingPeriod Period,
        decimal Qty, decimal Price)[] lines)
    {
        var plan = RevenuePlan.CreateInitial(Pid, "当初売上予算", Now);
        foreach (var (item, period, qty, price) in lines)
            plan.UpsertLine(item, period, qty, new Money(price));
        plan.Approve(Now);
        return plan;
    }

    [Fact]
    public void 総差異は販売価格差異と販売数量差異に分解される()
    {
        // 予定: 100個 × 2,000円 = 200,000円
        // 実績: 110個 × 2,100円 = 231,000円
        var plan = ApprovedPlan(("製品A", Apr, 100m, 2_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "製品A", Apr, 110m, new Money(2_100m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(31_000m, line.TotalVariance);
        // 販売価格差異 = (2,100 - 2,000) × 110 = 11,000
        Assert.Equal(11_000m, line.PriceVariance);
        // 販売数量差異 = (110 - 100) × 2,000 = 20,000
        Assert.Equal(20_000m, line.QuantityVariance);
        Assert.Equal(line.TotalVariance, line.PriceVariance + line.QuantityVariance);
        // 売上は実績超過が有利差異
        Assert.True(line.IsFavorable);
    }

    [Fact]
    public void 売上未達は不利差異として報告される()
    {
        var plan = ApprovedPlan(("製品A", Apr, 100m, 2_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "製品A", Apr, 80m, new Money(2_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(-40_000m, line.TotalVariance);
        Assert.False(line.IsFavorable);
    }

    [Fact]
    public void 同一品目期間の複数実績は合算され単価は加重平均になる()
    {
        var plan = ApprovedPlan(("製品A", Apr, 100m, 2_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "製品A", Apr, 60m, new Money(2_000m), "上旬", Now),
            ActualRevenue.Record(Pid, "製品A", Apr, 40m, new Money(2_200m), "下旬", Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(100m, line.ActualQuantity);
        Assert.Equal(208_000m, line.ActualAmount);
        Assert.Equal(2_080m, line.ActualUnitPrice);
    }

    [Fact]
    public void 予算のない品目の実績は予定外として報告される()
    {
        var plan = ApprovedPlan(("製品A", Apr, 100m, 2_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "スポット販売", Apr, 10m, new Money(1_500m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var unplanned = report.Lines.Single(l => l.ItemName == "スポット販売");
        Assert.True(unplanned.IsUnplanned);
        Assert.Null(unplanned.PriceVariance);
        Assert.Equal(15_000m, unplanned.TotalVariance);
    }
}

public class RevenuePlanComparisonTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly AccountingPeriod Apr = new(2026, 4);

    [Fact]
    public void 売上予算バージョン間の変動を品目単位で比較できる()
    {
        var pid = ProjectId.New();
        var v1 = RevenuePlan.CreateInitial(pid, "当初売上予算", Now);
        v1.UpsertLine("製品A", Apr, 100m, new Money(2_000m));
        v1.Approve(Now);

        var v2 = RevenuePlan.ReviseFrom(v1, 2, "上方修正", Now);
        v2.UpsertLine("製品A", Apr, 120m, new Money(2_000m));

        var report = new RevenuePlanComparisonService().Compare(v1, v2);

        var line = Assert.Single(report.Lines);
        Assert.Equal(200_000m, line.BaseAmount);
        Assert.Equal(240_000m, line.TargetAmount);
        Assert.Equal(40_000m, report.TotalDifference);
    }
}

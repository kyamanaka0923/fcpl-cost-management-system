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
        plan.UpsertLine("案件A", Apr, new Money(2_000_000m));
        plan.UpsertLine("案件A", Apr, new Money(2_200_000m));

        var line = Assert.Single(plan.Lines);
        Assert.Equal(2_200_000m, line.Amount.Value);
        Assert.Equal(2_200_000m, plan.TotalAmount.Value);
    }

    [Fact]
    public void 承認済みの売上予算は編集できない()
    {
        var plan = NewDraft();
        plan.UpsertLine("案件A", Apr, new Money(2_000_000m));
        plan.Approve(Now);

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine("案件A", Apr, new Money(2_500_000m)));
    }

    [Fact]
    public void 改定版は明細を引き継いだ新バージョンのドラフトになる()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine("案件A", Apr, new Money(2_000_000m));
        basePlan.Approve(Now);

        var revised = RevenuePlan.ReviseFrom(basePlan, 2, "第2四半期改定", Now);

        Assert.Equal(2, revised.Version);
        Assert.Equal(PlanStatus.Draft, revised.Status);
        var line = Assert.Single(revised.Lines);
        Assert.Equal(2_000_000m, line.Amount.Value);
        Assert.NotEqual(basePlan.Lines[0].Id, line.Id);
    }

    [Fact]
    public void 品目名は必須で前後の空白は除去される()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine("  ", Apr, new Money(100m)));

        plan.UpsertLine(" 案件A ", Apr, new Money(100m));
        Assert.Equal("案件A", plan.Lines[0].ItemName);
    }

    [Fact]
    public void 負の金額は登録できない()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine("案件A", Apr, new Money(-1m)));
    }
}

public class RevenueVarianceAnalysisTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ProjectId Pid = ProjectId.New();
    private static readonly AccountingPeriod Apr = new(2026, 4);

    private readonly RevenueVarianceAnalysisService _service = new();

    private static RevenuePlan ApprovedPlan(params (string Item, AccountingPeriod Period,
        decimal Amount)[] lines)
    {
        var plan = RevenuePlan.CreateInitial(Pid, "当初売上予算", Now);
        foreach (var (item, period, amount) in lines)
            plan.UpsertLine(item, period, new Money(amount));
        plan.Approve(Now);
        return plan;
    }

    [Fact]
    public void 売上超過は有利差異として報告される()
    {
        var plan = ApprovedPlan(("案件A", Apr, 2_000_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "案件A", Apr, new Money(2_310_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(310_000m, line.TotalVariance);
        Assert.True(line.IsFavorable);
    }

    [Fact]
    public void 売上未達は不利差異として報告される()
    {
        var plan = ApprovedPlan(("案件A", Apr, 2_000_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "案件A", Apr, new Money(1_600_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(-400_000m, line.TotalVariance);
        Assert.False(line.IsFavorable);
    }

    [Fact]
    public void 同一品目期間の複数実績は合算される()
    {
        var plan = ApprovedPlan(("案件A", Apr, 2_000_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "案件A", Apr, new Money(1_200_000m), "検収1", Now),
            ActualRevenue.Record(Pid, "案件A", Apr, new Money(880_000m), "検収2", Now),
        };

        var report = _service.Analyze(plan, actuals);

        var line = Assert.Single(report.Lines);
        Assert.Equal(2_080_000m, line.ActualAmount);
        Assert.Equal(80_000m, line.TotalVariance);
    }

    [Fact]
    public void 予算のない品目の実績は予定外として報告される()
    {
        var plan = ApprovedPlan(("案件A", Apr, 2_000_000m));
        var actuals = new[]
        {
            ActualRevenue.Record(Pid, "スポット案件", Apr, new Money(150_000m), null, Now),
        };

        var report = _service.Analyze(plan, actuals);

        var unplanned = report.Lines.Single(l => l.ItemName == "スポット案件");
        Assert.True(unplanned.IsUnplanned);
        Assert.Equal(150_000m, unplanned.TotalVariance);
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
        v1.UpsertLine("案件A", Apr, new Money(2_000_000m));
        v1.Approve(Now);

        var v2 = RevenuePlan.ReviseFrom(v1, 2, "上方修正", Now);
        v2.UpsertLine("案件A", Apr, new Money(2_400_000m));

        var report = new RevenuePlanComparisonService().Compare(v1, v2);

        var line = Assert.Single(report.Lines);
        Assert.Equal(2_000_000m, line.BaseAmount);
        Assert.Equal(2_400_000m, line.TargetAmount);
        Assert.Equal(400_000m, report.TotalDifference);
    }
}

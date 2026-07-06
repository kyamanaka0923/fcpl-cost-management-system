using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class CostPlanTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CostElementCode Material = new("MAT-RAW");
    private static readonly AccountingPeriod Apr = new(2026, 4);

    private static CostPlan NewDraft() =>
        CostPlan.CreateInitial(ProjectId.New(), "当初予算", Now);

    [Fact]
    public void 当初予算はバージョン1のドラフトとして作成される()
    {
        var plan = NewDraft();

        Assert.Equal(1, plan.Version);
        Assert.Equal(PlanStatus.Draft, plan.Status);
        Assert.Empty(plan.Lines);
    }

    [Fact]
    public void 同一費目同一期間の明細は上書きされる()
    {
        var plan = NewDraft();
        plan.UpsertLine(Material, Apr, 100m, new Money(500m));
        plan.UpsertLine(Material, Apr, 120m, new Money(480m));

        var line = Assert.Single(plan.Lines);
        Assert.Equal(120m, line.Quantity);
        Assert.Equal(480m, line.UnitPrice.Value);
        Assert.Equal(57_600m, plan.TotalAmount.Value);
    }

    [Fact]
    public void 明細のない予算は承認できない()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() => plan.Approve(Now));
    }

    [Fact]
    public void 承認済みの予算は編集できない()
    {
        var plan = NewDraft();
        plan.UpsertLine(Material, Apr, 100m, new Money(500m));
        plan.Approve(Now);

        Assert.Equal(PlanStatus.Approved, plan.Status);
        Assert.Throws<DomainException>(() =>
            plan.UpsertLine(Material, Apr, 200m, new Money(500m)));
        Assert.Throws<DomainException>(() => plan.RemoveLine(Material, Apr));
    }

    [Fact]
    public void 改定版は明細を引き継いだ新バージョンのドラフトになる()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine(Material, Apr, 100m, new Money(500m));
        basePlan.Approve(Now);

        var revised = CostPlan.ReviseFrom(basePlan, 2, "第2四半期改定", Now);

        Assert.Equal(2, revised.Version);
        Assert.Equal(PlanStatus.Draft, revised.Status);
        Assert.Equal(basePlan.ProjectId, revised.ProjectId);
        var line = Assert.Single(revised.Lines);
        Assert.Equal(100m, line.Quantity);
        Assert.Equal(500m, line.UnitPrice.Value);
        // 明細は複製であり、基の予算とは独立している。
        Assert.NotEqual(basePlan.Lines[0].Id, line.Id);
    }

    [Fact]
    public void 改定版のバージョンは基より大きくなければならない()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine(Material, Apr, 100m, new Money(500m));

        Assert.Throws<DomainException>(() =>
            CostPlan.ReviseFrom(basePlan, 1, "改定", Now));
    }

    [Fact]
    public void 承認済みの予算のみ失効にできる()
    {
        var plan = NewDraft();
        plan.UpsertLine(Material, Apr, 100m, new Money(500m));

        Assert.Throws<DomainException>(plan.Supersede);

        plan.Approve(Now);
        plan.Supersede();
        Assert.Equal(PlanStatus.Superseded, plan.Status);
    }

    [Fact]
    public void 負の数量や単価は登録できない()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine(Material, Apr, -1m, new Money(500m)));
        Assert.Throws<DomainException>(() =>
            plan.UpsertLine(Material, Apr, 1m, new Money(-500m)));
    }
}

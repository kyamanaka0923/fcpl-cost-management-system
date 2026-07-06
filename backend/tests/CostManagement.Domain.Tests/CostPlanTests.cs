using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Tests;

public class CostPlanTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CostElementCode Labor = new("LAB-SE");
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
    public void 同一費目同一品目同一期間の明細は上書きされる()
    {
        var plan = NewDraft();
        plan.UpsertLine(Labor, "案件A", Apr, new Money(500_000m));
        plan.UpsertLine(Labor, "案件A", Apr, new Money(550_000m));

        var line = Assert.Single(plan.Lines);
        Assert.Equal(550_000m, line.Amount.Value);
        Assert.Equal(550_000m, plan.TotalAmount.Value);
    }

    [Fact]
    public void 売上対応品目が異なれば別明細として管理される()
    {
        var plan = NewDraft();
        plan.UpsertLine(Labor, "案件A", Apr, new Money(500_000m));
        plan.UpsertLine(Labor, "案件B", Apr, new Money(300_000m));
        plan.UpsertLine(Labor, null, Apr, new Money(100_000m)); // 共通費

        Assert.Equal(3, plan.Lines.Count);
        Assert.Equal(900_000m, plan.TotalAmount.Value);
    }

    [Fact]
    public void 売上対応品目は空白なら共通費として正規化される()
    {
        var plan = NewDraft();
        plan.UpsertLine(Labor, "  ", Apr, new Money(100_000m));
        plan.UpsertLine(Labor, null, Apr, new Money(200_000m)); // 同一キー(共通費)として上書き

        var line = Assert.Single(plan.Lines);
        Assert.Null(line.RevenueItem);
        Assert.Equal(200_000m, line.Amount.Value);
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
        plan.UpsertLine(Labor, "案件A", Apr, new Money(500_000m));
        plan.Approve(Now);

        Assert.Equal(PlanStatus.Approved, plan.Status);
        Assert.Throws<DomainException>(() =>
            plan.UpsertLine(Labor, "案件A", Apr, new Money(600_000m)));
        Assert.Throws<DomainException>(() => plan.RemoveLine(Labor, "案件A", Apr));
    }

    [Fact]
    public void 改定版は明細を引き継いだ新バージョンのドラフトになる()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine(Labor, "案件A", Apr, new Money(500_000m));
        basePlan.Approve(Now);

        var revised = CostPlan.ReviseFrom(basePlan, 2, "第2四半期改定", Now);

        Assert.Equal(2, revised.Version);
        Assert.Equal(PlanStatus.Draft, revised.Status);
        Assert.Equal(basePlan.ProjectId, revised.ProjectId);
        var line = Assert.Single(revised.Lines);
        Assert.Equal(500_000m, line.Amount.Value);
        Assert.Equal("案件A", line.RevenueItem);
        // 明細は複製であり、基の予算とは独立している。
        Assert.NotEqual(basePlan.Lines[0].Id, line.Id);
    }

    [Fact]
    public void 改定版のバージョンは基より大きくなければならない()
    {
        var basePlan = NewDraft();
        basePlan.UpsertLine(Labor, null, Apr, new Money(500_000m));

        Assert.Throws<DomainException>(() =>
            CostPlan.ReviseFrom(basePlan, 1, "改定", Now));
    }

    [Fact]
    public void 承認済みの予算のみ失効にできる()
    {
        var plan = NewDraft();
        plan.UpsertLine(Labor, null, Apr, new Money(500_000m));

        Assert.Throws<DomainException>(plan.Supersede);

        plan.Approve(Now);
        plan.Supersede();
        Assert.Equal(PlanStatus.Superseded, plan.Status);
    }

    [Fact]
    public void 負の金額は登録できない()
    {
        var plan = NewDraft();

        Assert.Throws<DomainException>(() =>
            plan.UpsertLine(Labor, null, Apr, new Money(-1m)));
    }
}

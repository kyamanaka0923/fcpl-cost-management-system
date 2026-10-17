using Reqnroll;
using static CostManagement.Application.Tests.Features.業務コンテキスト;

namespace CostManagement.Application.Tests.Features.Steps;

/// <summary>実績計上・予実差異・損益・部の予実サマリと部承認のステップ。</summary>
[Binding]
public sealed class 実績と分析のステップ(業務コンテキスト ctx)
{
    // ---- 実績 ----

    [StepDefinition("課 {string} の {string} に次の実績を計上する:")]
    public async Task 実績を計上する(string department, string half, DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var project = 空ならnull(row["案件"]) is { } code ? ctx.案件Id(department, code) : (Guid?)null;
            var month = 空ならnull(row["月"]) is { } m ? 整数(m) : (int?)null;
            await ctx.Fx.Actuals.RecordAsync(ctx.課Id(department), new RecordActualRequest(half,
                区分(row["区分"]), project, 空ならnull(row["費目"]), 金額(row["金額"]), month,
                null, 空ならnull(row["明細名"])));
        }
    }

    // ---- 予実差異 ----

    [When("課 {string} の {string} の予実差異を分析する")]
    public async Task 予実差異を分析する(string department, string half)
    {
        ctx.現在の課 = department;
        ctx.差異 = await ctx.Fx.Analysis.AnalyzeVarianceAsync(ctx.課Id(department), half, null);
    }

    [Then("予実差異の明細は次のとおりである:")]
    public void 予実差異の明細は(DataTable table)
    {
        var lines = 差異().Categories.SelectMany(c => c.Lines).ToList();
        Assert.Equal(table.RowCount, lines.Count);
        foreach (var row in table.Rows)
        {
            var line = 明細を探す(lines, row["区分"], row["案件"], row["費目"], row["明細名"]);
            var 利益への影響 = line.IsFavorable ? "利益が増える" : line.IsAdverse ? "利益が減る" : "影響なし";
            Assert.Equal(
                (金額(row["予算"]), 金額(row["実績"]), 金額(row["差異"]), row["利益への影響"]),
                (line.PlannedAmount, line.ActualAmount, line.Variance, 利益への影響));
        }
    }

    [Then("期間費用 {string} の明細名 {string} は予定外で実績は {long} である")]
    public void 予定外の明細(string element, string detail, long actual)
    {
        var line = 明細を探す(差異().Categories.SelectMany(c => c.Lines).ToList(), "期間費用", "", element, detail);
        Assert.True(line.IsUnplanned, "予定外として扱われていません。");
        Assert.Equal((0m, (decimal)actual), (line.PlannedAmount, line.ActualAmount));
    }

    [Then("予実差異の売上高は予算 {long} 実績 {long} でコストは予算 {long} 実績 {long} である")]
    public void 予実差異の合計(long plannedRevenue, long actualRevenue, long plannedCost, long actualCost)
    {
        var r = 差異();
        Assert.Equal((plannedRevenue, actualRevenue, plannedCost, actualCost),
            ((long)r.PlannedRevenue, (long)r.ActualRevenue, (long)r.PlannedCost, (long)r.ActualCost));
    }

    // ---- 損益 ----

    [When("課 {string} の {string} の損益を分析する")]
    public async Task 損益を分析する(string department, string half)
    {
        ctx.現在の課 = department;
        ctx.損益 = await ctx.Fx.Analysis.GetProfitSummaryAsync(ctx.課Id(department), half, null);
    }

    [Then("案件別損益は次のとおりである:")]
    public void 案件別損益は(DataTable table)
    {
        var lines = 損益().ProjectLines;
        Assert.Equal(table.RowCount, lines.Count);
        foreach (var row in table.Rows)
        {
            var line = lines.Single(l => l.ProjectId == ctx.案件Id(ctx.現在の課!, row["案件"]));
            Assert.Equal((金額(row["計画損益"]), 金額(row["実績損益"])),
                (line.PlannedProfit, line.ActualProfit));
        }
    }

    [Then("期間費用は計画 {long} 実績 {long} である")]
    public void 期間費用は(long planned, long actual) =>
        Assert.Equal((planned, actual), ((long)損益().PlannedPeriodCost, (long)損益().ActualPeriodCost));

    [Then("課全体の損益は計画 {long} 実績 {long} である")]
    public void 課全体の損益は(long planned, long actual)
    {
        var p = 損益();
        Assert.Equal((planned, actual), ((long)p.PlannedProfit, (long)p.ActualProfit));
        // 不変条件: 案件別損益の合計 − 期間費用 = 課全体の損益
        Assert.Equal(p.PlannedProfit, p.ProjectLines.Sum(l => l.PlannedProfit) - p.PlannedPeriodCost);
        Assert.Equal(p.ActualProfit, p.ProjectLines.Sum(l => l.ActualProfit) - p.ActualPeriodCost);
    }

    // ---- 部 ----

    [When("部 {string} の {string} の予実サマリを確認する")]
    public async Task 部の予実サマリを確認する(string division, string half) =>
        ctx.部サマリ = await ctx.Fx.Analysis.GetDivisionBudgetSummaryAsync(ctx.部[division], half);

    [Then("部サマリの課別内訳は次のとおりである:")]
    public void 課別内訳は(DataTable table)
    {
        var lines = 部サマリ().DepartmentLines;
        Assert.Equal(table.Rows.Select(r => r["課"]), lines.Select(l => l.DepartmentName));
        foreach (var row in table.Rows)
        {
            var line = lines.Single(l => l.DepartmentName == row["課"]);
            Assert.Equal(
                (row["予算"], 金額(row["計画売上高"]), 金額(row["実績売上高"]), 金額(row["計画コスト"])),
                (line.HasApprovedBudget ? "承認済" : "未策定", line.PlannedRevenue, line.ActualRevenue, line.PlannedCost));
        }
    }

    [Then("部の合計は売上高が計画 {long} 実績 {long} でコストが計画 {long} である")]
    public void 部の合計は(long plannedRevenue, long actualRevenue, long plannedCost)
    {
        var s = 部サマリ();
        Assert.Equal((plannedRevenue, actualRevenue, plannedCost),
            ((long)s.PlannedRevenue, (long)s.ActualRevenue, (long)s.PlannedCost));
    }

    [Then("部は承認できる状態である")]
    public void 部は承認できる() => Assert.True(部サマリ().CanApprove);

    [Then("部は承認できない状態である")]
    public void 部は承認できない() => Assert.False(部サマリ().CanApprove);

    [Then("部は承認済みである")]
    public void 部は承認済み() => Assert.True(部サマリ().IsApproved);

    [Then("部は未承認である")]
    public void 部は未承認() => Assert.False(部サマリ().IsApproved);

    [When("部 {string} の {string} を承認する")]
    public Task 部を承認する(string division, string half) =>
        ctx.Fx.DivisionApprovals.ApproveAsync(ctx.部[division], half);

    [When("部 {string} の {string} を承認しようとする")]
    public Task 部を承認しようとする(string division, string half) =>
        ctx.試行(() => 部を承認する(division, half));

    [When("部 {string} の {string} の承認を取り消す")]
    public Task 部承認を取り消す(string division, string half) =>
        ctx.Fx.DivisionApprovals.RevokeAsync(ctx.部[division], half);

    // ---- 補助 ----

    private VarianceReportDto 差異() =>
        ctx.差異 ?? throw new InvalidOperationException("先に予実差異を分析してください。");

    private ProfitSummaryDto 損益() =>
        ctx.損益 ?? throw new InvalidOperationException("先に損益を分析してください。");

    private DivisionBudgetSummaryDto 部サマリ() =>
        ctx.部サマリ ?? throw new InvalidOperationException("先に部の予実サマリを確認してください。");

    private VarianceLineDto 明細を探す(List<VarianceLineDto> lines, string category, string? project,
        string? element, string? detail)
    {
        var projectId = 空ならnull(project) is { } code ? ctx.案件Id(ctx.現在の課!, code) : (Guid?)null;
        var key = (区分(category), projectId, 空ならnull(element), 空ならnull(detail));
        return lines.SingleOrDefault(l => (l.Category, l.ProjectId, l.ElementCode, l.PeriodDetail) == key)
            ?? throw new Xunit.Sdk.XunitException($"差異の明細が見つかりません: {category} {project} {element} {detail}");
    }
}

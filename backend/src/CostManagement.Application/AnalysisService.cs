using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>予実差異分析・予算バージョン間比較・損益サマリのユースケース。</summary>
public sealed class AnalysisService
{
    private readonly ICostPlanRepository _plans;
    private readonly IActualCostRepository _actuals;
    private readonly IRevenuePlanRepository _revenuePlans;
    private readonly IActualRevenueRepository _actualRevenues;
    private readonly VarianceAnalysisService _varianceAnalysis;
    private readonly PlanComparisonService _planComparison;
    private readonly RevenueVarianceAnalysisService _revenueVarianceAnalysis;
    private readonly RevenuePlanComparisonService _revenuePlanComparison;
    private readonly ProfitAnalysisService _profitAnalysis;

    public AnalysisService(ICostPlanRepository plans, IActualCostRepository actuals,
        IRevenuePlanRepository revenuePlans, IActualRevenueRepository actualRevenues,
        VarianceAnalysisService varianceAnalysis, PlanComparisonService planComparison,
        RevenueVarianceAnalysisService revenueVarianceAnalysis,
        RevenuePlanComparisonService revenuePlanComparison,
        ProfitAnalysisService profitAnalysis)
    {
        _plans = plans;
        _actuals = actuals;
        _revenuePlans = revenuePlans;
        _actualRevenues = actualRevenues;
        _varianceAnalysis = varianceAnalysis;
        _planComparison = planComparison;
        _revenueVarianceAnalysis = revenueVarianceAnalysis;
        _revenuePlanComparison = revenuePlanComparison;
        _profitAnalysis = profitAnalysis;
    }

    /// <summary>
    /// 予実差異分析。planId 未指定時は最新の承認済み予算を基準とする。
    /// from/to("yyyy-MM")で対象期間を絞り込める。
    /// </summary>
    public async Task<VarianceReportDto> AnalyzeVarianceAsync(Guid projectId, Guid? planId,
        string? from, string? to, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);

        CostPlan plan;
        if (planId is { } id)
        {
            plan = await _plans.FindByIdAsync(new CostPlanId(id), ct)
                ?? throw new NotFoundException($"予算が見つかりません: {id}");
            if (plan.ProjectId != pid)
                throw new DomainException("指定された予算はこのプロジェクトのものではありません。");
        }
        else
        {
            plan = await _plans.FindLatestApprovedAsync(pid, ct)
                ?? throw new NotFoundException("承認済みの予算が存在しません。先に予算を承認してください。");
        }

        var actuals = await _actuals.ListByProjectAsync(pid, ct);
        var report = _varianceAnalysis.Analyze(plan, actuals,
            from is null ? null : AccountingPeriod.Parse(from),
            to is null ? null : AccountingPeriod.Parse(to));

        return new VarianceReportDto(
            plan.Id.Value, plan.Version, plan.Label,
            report.Lines.Select(l => new VarianceLineDto(
                l.ElementCode, l.RevenueItem, l.Period.ToString(),
                l.PlannedAmount, l.ActualAmount, l.TotalVariance,
                l.IsUnplanned, l.IsAdverse)).ToList(),
            report.TotalPlannedAmount, report.TotalActualAmount, report.TotalVariance);
    }

    /// <summary>予算バージョン間の変動比較(例: 当初予算 vs 第2四半期改定)。</summary>
    public async Task<PlanComparisonDto> ComparePlansAsync(Guid projectId, int baseVersion,
        int targetVersion, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        var plans = await _plans.ListByProjectAsync(pid, ct);

        var basePlan = plans.FirstOrDefault(p => p.Version == baseVersion)
            ?? throw new NotFoundException($"バージョン {baseVersion} の予算が見つかりません。");
        var targetPlan = plans.FirstOrDefault(p => p.Version == targetVersion)
            ?? throw new NotFoundException($"バージョン {targetVersion} の予算が見つかりません。");

        var report = _planComparison.Compare(basePlan, targetPlan);
        return new PlanComparisonDto(
            report.BaseVersion, report.BaseLabel, report.TargetVersion, report.TargetLabel,
            report.Lines.Select(l => new PlanComparisonLineDto(
                l.ElementCode, l.RevenueItem, l.Period.ToString(),
                l.BaseAmount, l.TargetAmount, l.Difference))
                .ToList(),
            report.BaseTotalAmount, report.TargetTotalAmount, report.TotalDifference);
    }

    /// <summary>売上の予実差異分析。planId 未指定時は最新の承認済み売上予算を基準とする。</summary>
    public async Task<RevenueVarianceReportDto> AnalyzeRevenueVarianceAsync(Guid projectId,
        Guid? planId, string? from, string? to, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);

        RevenuePlan plan;
        if (planId is { } id)
        {
            plan = await _revenuePlans.FindByIdAsync(new RevenuePlanId(id), ct)
                ?? throw new NotFoundException($"売上予算が見つかりません: {id}");
            if (plan.ProjectId != pid)
                throw new DomainException("指定された予算はこのプロジェクトのものではありません。");
        }
        else
        {
            plan = await _revenuePlans.FindLatestApprovedAsync(pid, ct)
                ?? throw new NotFoundException("承認済みの売上予算が存在しません。先に売上予算を承認してください。");
        }

        var actuals = await _actualRevenues.ListByProjectAsync(pid, ct);
        var report = _revenueVarianceAnalysis.Analyze(plan, actuals,
            from is null ? null : AccountingPeriod.Parse(from),
            to is null ? null : AccountingPeriod.Parse(to));

        return new RevenueVarianceReportDto(
            plan.Id.Value, plan.Version, plan.Label,
            report.Lines.Select(l => new RevenueVarianceLineDto(
                l.ItemName, l.Period.ToString(),
                l.PlannedAmount, l.ActualAmount, l.TotalVariance,
                l.IsUnplanned, l.IsFavorable)).ToList(),
            report.TotalPlannedAmount, report.TotalActualAmount, report.TotalVariance);
    }

    /// <summary>売上予算バージョン間の変動比較。</summary>
    public async Task<RevenuePlanComparisonDto> CompareRevenuePlansAsync(Guid projectId,
        int baseVersion, int targetVersion, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        var plans = await _revenuePlans.ListByProjectAsync(pid, ct);

        var basePlan = plans.FirstOrDefault(p => p.Version == baseVersion)
            ?? throw new NotFoundException($"バージョン {baseVersion} の売上予算が見つかりません。");
        var targetPlan = plans.FirstOrDefault(p => p.Version == targetVersion)
            ?? throw new NotFoundException($"バージョン {targetVersion} の売上予算が見つかりません。");

        var report = _revenuePlanComparison.Compare(basePlan, targetPlan);
        return new RevenuePlanComparisonDto(
            report.BaseVersion, report.BaseLabel, report.TargetVersion, report.TargetLabel,
            report.Lines.Select(l => new RevenuePlanComparisonLineDto(
                l.ItemName, l.Period.ToString(), l.BaseAmount, l.TargetAmount, l.Difference))
                .ToList(),
            report.BaseTotalAmount, report.TargetTotalAmount, report.TotalDifference);
    }

    /// <summary>
    /// 損益(粗利)の予実サマリ。最新の承認済み売上予算・原価予算を突き合わせ、
    /// 全体・品目別(売上対応原価との突き合わせ)・月別の粗利予実を返す。
    /// </summary>
    public async Task<ProfitSummaryDto> GetProfitSummaryAsync(Guid projectId,
        string? from, string? to, CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);

        var revenuePlan = await _revenuePlans.FindLatestApprovedAsync(pid, ct)
            ?? throw new NotFoundException("承認済みの売上予算が存在しません。先に売上予算を承認してください。");
        var costPlan = await _plans.FindLatestApprovedAsync(pid, ct)
            ?? throw new NotFoundException("承認済みの原価予算が存在しません。先に原価予算を承認してください。");

        var fromPeriod = from is null ? (AccountingPeriod?)null : AccountingPeriod.Parse(from);
        var toPeriod = to is null ? (AccountingPeriod?)null : AccountingPeriod.Parse(to);

        var revenueReport = _revenueVarianceAnalysis.Analyze(revenuePlan,
            await _actualRevenues.ListByProjectAsync(pid, ct), fromPeriod, toPeriod);
        var costReport = _varianceAnalysis.Analyze(costPlan,
            await _actuals.ListByProjectAsync(pid, ct), fromPeriod, toPeriod);

        var report = _profitAnalysis.Analyze(revenueReport, costReport);

        return new ProfitSummaryDto(
            revenuePlan.Version, revenuePlan.Label,
            costPlan.Version, costPlan.Label,
            report.PlannedRevenue, report.ActualRevenue, report.RevenueVariance,
            report.PlannedCost, report.ActualCost, report.CostVariance,
            report.PlannedProfit, report.ActualProfit, report.ProfitVariance,
            report.PlannedMarginRate, report.ActualMarginRate,
            report.ItemLines.Select(l => new ProfitItemLineDto(l.ItemName,
                l.PlannedRevenue, l.ActualRevenue, l.PlannedCost, l.ActualCost,
                l.PlannedProfit, l.ActualProfit, l.ProfitVariance)).ToList(),
            report.PeriodLines.Select(l => new ProfitPeriodLineDto(l.Period.ToString(),
                l.PlannedRevenue, l.ActualRevenue, l.PlannedCost, l.ActualCost,
                l.PlannedProfit, l.ActualProfit, l.ProfitVariance)).ToList());
    }

    /// <summary>
    /// 売上対応品目の候補一覧。売上予算・売上実績に登場する品目名を重複なく返す
    /// (原価明細の「売上対応品目」入力の補完に利用)。
    /// </summary>
    public async Task<IReadOnlyList<string>> ListRevenueItemsAsync(Guid projectId,
        CancellationToken ct = default)
    {
        var pid = new ProjectId(projectId);
        var planItems = (await _revenuePlans.ListByProjectAsync(pid, ct))
            .SelectMany(p => p.Lines.Select(l => l.ItemName));
        var actualItems = (await _actualRevenues.ListByProjectAsync(pid, ct))
            .Select(a => a.ItemName);
        return planItems.Concat(actualItems).Distinct().OrderBy(x => x).ToList();
    }
}

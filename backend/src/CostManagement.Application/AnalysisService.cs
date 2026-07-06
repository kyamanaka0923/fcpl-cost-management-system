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

    public AnalysisService(ICostPlanRepository plans, IActualCostRepository actuals,
        IRevenuePlanRepository revenuePlans, IActualRevenueRepository actualRevenues,
        VarianceAnalysisService varianceAnalysis, PlanComparisonService planComparison,
        RevenueVarianceAnalysisService revenueVarianceAnalysis,
        RevenuePlanComparisonService revenuePlanComparison)
    {
        _plans = plans;
        _actuals = actuals;
        _revenuePlans = revenuePlans;
        _actualRevenues = actualRevenues;
        _varianceAnalysis = varianceAnalysis;
        _planComparison = planComparison;
        _revenueVarianceAnalysis = revenueVarianceAnalysis;
        _revenuePlanComparison = revenuePlanComparison;
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
                l.ElementCode, l.Period.ToString(),
                l.PlannedQuantity, l.PlannedUnitPrice, l.PlannedAmount,
                l.ActualQuantity, l.ActualUnitPrice, l.ActualAmount,
                l.TotalVariance, l.PriceVariance, l.QuantityVariance,
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
                l.ElementCode, l.Period.ToString(), l.BaseAmount, l.TargetAmount, l.Difference))
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
                l.PlannedQuantity, l.PlannedUnitPrice, l.PlannedAmount,
                l.ActualQuantity, l.ActualUnitPrice, l.ActualAmount,
                l.TotalVariance, l.PriceVariance, l.QuantityVariance,
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
    /// 売上 − 原価 = 粗利 の予実と月別内訳を返す。
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

        // 月別の内訳(売上・原価どちらかに存在する期間をすべて含める)
        var revenueByPeriod = revenueReport.Lines
            .GroupBy(l => l.Period)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));
        var costByPeriod = costReport.Lines
            .GroupBy(l => l.Period)
            .ToDictionary(g => g.Key,
                g => (Planned: g.Sum(l => l.PlannedAmount), Actual: g.Sum(l => l.ActualAmount)));

        var periodLines = revenueByPeriod.Keys.Union(costByPeriod.Keys)
            .OrderBy(p => p)
            .Select(p =>
            {
                var rev = revenueByPeriod.GetValueOrDefault(p);
                var cost = costByPeriod.GetValueOrDefault(p);
                var plannedProfit = rev.Planned - cost.Planned;
                var actualProfit = rev.Actual - cost.Actual;
                return new ProfitPeriodLineDto(p.ToString(),
                    rev.Planned, rev.Actual, cost.Planned, cost.Actual,
                    plannedProfit, actualProfit, actualProfit - plannedProfit);
            })
            .ToList();

        var totalPlannedProfit = revenueReport.TotalPlannedAmount - costReport.TotalPlannedAmount;
        var totalActualProfit = revenueReport.TotalActualAmount - costReport.TotalActualAmount;

        return new ProfitSummaryDto(
            revenuePlan.Version, revenuePlan.Label,
            costPlan.Version, costPlan.Label,
            revenueReport.TotalPlannedAmount, revenueReport.TotalActualAmount,
            revenueReport.TotalVariance,
            costReport.TotalPlannedAmount, costReport.TotalActualAmount,
            costReport.TotalVariance,
            totalPlannedProfit, totalActualProfit, totalActualProfit - totalPlannedProfit,
            revenueReport.TotalPlannedAmount != 0m
                ? totalPlannedProfit / revenueReport.TotalPlannedAmount
                : null,
            revenueReport.TotalActualAmount != 0m
                ? totalActualProfit / revenueReport.TotalActualAmount
                : null,
            periodLines);
    }
}

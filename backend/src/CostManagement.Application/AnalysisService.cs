using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>予実差異分析・予算バージョン間比較のユースケース。</summary>
public sealed class AnalysisService
{
    private readonly ICostPlanRepository _plans;
    private readonly IActualCostRepository _actuals;
    private readonly VarianceAnalysisService _varianceAnalysis;
    private readonly PlanComparisonService _planComparison;

    public AnalysisService(ICostPlanRepository plans, IActualCostRepository actuals,
        VarianceAnalysisService varianceAnalysis, PlanComparisonService planComparison)
    {
        _plans = plans;
        _actuals = actuals;
        _varianceAnalysis = varianceAnalysis;
        _planComparison = planComparison;
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
}

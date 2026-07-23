using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>予実差異分析・予算バージョン間比較・損益サマリ・部集計のユースケース。</summary>
public sealed class AnalysisService
{
    private readonly IDepartmentBudgetRepository _budgets;
    private readonly IActualEntryRepository _actuals;
    private readonly IProjectRepository _projects;
    private readonly ICostElementRepository _elements;
    private readonly IDepartmentRepository _departments;
    private readonly IDivisionRepository _divisions;
    private readonly IDivisionBudgetApprovalRepository _divisionApprovals;
    private readonly BudgetVarianceAnalysisService _varianceAnalysis;
    private readonly BudgetComparisonService _budgetComparison;
    private readonly ProfitAnalysisService _profitAnalysis;
    private readonly DivisionBudgetSummaryService _divisionSummary;

    /// <summary>依存するリポジトリ群と、ステートレスな分析ドメインサービス群を受け取る。</summary>
    public AnalysisService(IDepartmentBudgetRepository budgets, IActualEntryRepository actuals,
        IProjectRepository projects, ICostElementRepository elements,
        IDepartmentRepository departments, IDivisionRepository divisions,
        IDivisionBudgetApprovalRepository divisionApprovals,
        BudgetVarianceAnalysisService varianceAnalysis, BudgetComparisonService budgetComparison,
        ProfitAnalysisService profitAnalysis, DivisionBudgetSummaryService divisionSummary)
    {
        _budgets = budgets;
        _actuals = actuals;
        _projects = projects;
        _elements = elements;
        _departments = departments;
        _divisions = divisions;
        _divisionApprovals = divisionApprovals;
        _varianceAnalysis = varianceAnalysis;
        _budgetComparison = budgetComparison;
        _profitAnalysis = profitAnalysis;
        _divisionSummary = divisionSummary;
    }

    /// <summary>予実差異分析。budgetId 未指定時は最新の承認済み予算を基準とする。</summary>
    public async Task<VarianceReportDto> AnalyzeVarianceAsync(Guid departmentId, string fiscalHalf,
        Guid? budgetId, CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        var half = FiscalHalf.Parse(fiscalHalf);
        var budget = await ResolveBudgetAsync(did, half, budgetId, ct);

        var actuals = await _actuals.ListAsync(did, half, ct);
        var report = _varianceAnalysis.Analyze(budget, actuals);
        var names = await LoadNamesAsync(did, ct);

        return new VarianceReportDto(
            budget.Id.Value, budget.Version, budget.Label,
            report.Categories.Select(c => new CategoryVarianceDto(
                c.Category.ToString(),
                c.Lines.Select(l => new VarianceLineDto(
                    l.Category.ToString(), l.ProjectId, names.ProjectName(l.ProjectId),
                    l.ElementCode, names.ElementName(l.ElementCode), l.PeriodDetail,
                    l.PlannedAmount, l.ActualAmount, l.Variance,
                    l.IsUnplanned, l.IsFavorable, l.IsAdverse)).ToList(),
                c.PlannedAmount, c.ActualAmount, c.Variance)).ToList(),
            report.PlannedRevenue, report.ActualRevenue, report.RevenueVariance,
            report.PlannedCost, report.ActualCost, report.CostVariance);
    }

    /// <summary>予算バージョン間の変動比較(例: 当初予算 vs 下期見直し)。</summary>
    public async Task<BudgetComparisonDto> CompareBudgetsAsync(Guid departmentId, string fiscalHalf,
        int baseVersion, int targetVersion, CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        var budgets = await _budgets.ListAsync(did, FiscalHalf.Parse(fiscalHalf), ct);

        var baseBudget = budgets.FirstOrDefault(b => b.Version == baseVersion)
            ?? throw new NotFoundException($"バージョン {baseVersion} の予算が見つかりません。");
        var targetBudget = budgets.FirstOrDefault(b => b.Version == targetVersion)
            ?? throw new NotFoundException($"バージョン {targetVersion} の予算が見つかりません。");

        var report = _budgetComparison.Compare(baseBudget, targetBudget);
        var names = await LoadNamesAsync(did, ct);

        return new BudgetComparisonDto(
            report.BaseVersion, report.BaseLabel, report.TargetVersion, report.TargetLabel,
            report.Categories.Select(c => new CategoryComparisonDto(
                c.Category.ToString(),
                c.Lines.Select(l => new BudgetComparisonLineDto(
                    l.Category.ToString(), l.ProjectId, names.ProjectName(l.ProjectId),
                    l.ElementCode, names.ElementName(l.ElementCode), l.PeriodDetail,
                    l.BaseAmount, l.TargetAmount, l.Difference)).ToList(),
                c.BaseAmount, c.TargetAmount, c.Difference)).ToList());
    }

    /// <summary>
    /// 損益の予実サマリ。全体(売上高 − 総コスト)と案件別(売上高 − 加工費 − 外注費)の
    /// 損益予実を返す。budgetId 未指定時は最新の承認済み予算を基準とする。
    /// </summary>
    public async Task<ProfitSummaryDto> GetProfitSummaryAsync(Guid departmentId, string fiscalHalf,
        Guid? budgetId, CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        var half = FiscalHalf.Parse(fiscalHalf);
        var budget = await ResolveBudgetAsync(did, half, budgetId, ct);

        var actuals = await _actuals.ListAsync(did, half, ct);
        var report = _profitAnalysis.Analyze(_varianceAnalysis.Analyze(budget, actuals));
        var names = await LoadNamesAsync(did, ct);

        return new ProfitSummaryDto(
            budget.Version, budget.Label,
            report.PlannedRevenue, report.ActualRevenue,
            report.PlannedTotalCost, report.ActualTotalCost,
            report.PlannedPeriodCost, report.ActualPeriodCost,
            report.PlannedProfit, report.ActualProfit, report.ProfitVariance,
            report.PlannedMarginRate, report.ActualMarginRate,
            report.ProjectLines.Select(l => new ProjectProfitLineDto(
                l.ProjectId, names.ProjectCode(l.ProjectId), names.ProjectName(l.ProjectId) ?? "",
                l.PlannedRevenue, l.ActualRevenue,
                l.PlannedProcessing, l.ActualProcessing,
                l.PlannedOutsourcing, l.ActualOutsourcing,
                l.PlannedProfit, l.ActualProfit, l.ProfitVariance)).ToList());
    }

    /// <summary>
    /// 部の予実サマリ。配下課の最新承認済み予算と実績を突き合わせて合計する。
    /// 承認済み予算のない課は合計に含めず、課別内訳に未策定として表示する。
    /// </summary>
    public async Task<DivisionBudgetSummaryDto> GetDivisionBudgetSummaryAsync(Guid divisionId,
        string fiscalHalf, CancellationToken ct = default)
    {
        var divId = new DivisionId(divisionId);
        _ = await _divisions.FindByIdAsync(divId, ct)
            ?? throw new NotFoundException($"部が見つかりません: {divisionId}");
        var half = FiscalHalf.Parse(fiscalHalf);
        var departments = (await _departments.ListByDivisionAsync(divId, ct))
            .OrderBy(d => d.Code)
            .ToList();

        var inputs = new List<DepartmentVarianceInput>();
        var reportByDepartment = new Dictionary<Guid, VarianceReport>();
        foreach (var dept in departments)
        {
            var budget = await _budgets.FindLatestApprovedAsync(dept.Id, half, ct);
            if (budget is null)
                continue;
            var actuals = await _actuals.ListAsync(dept.Id, half, ct);
            var report = _varianceAnalysis.Analyze(budget, actuals);
            inputs.Add(new DepartmentVarianceInput(dept.Id, report));
            reportByDepartment[dept.Id.Value] = report;
        }

        var summary = _divisionSummary.Summarize(inputs);

        var lines = departments
            .Select(d =>
            {
                if (reportByDepartment.TryGetValue(d.Id.Value, out var r))
                {
                    var plannedProfit = r.PlannedRevenue - r.PlannedCost;
                    var actualProfit = r.ActualRevenue - r.ActualCost;
                    var categories = r.Categories
                        .Select(c => new CategorySummaryDto(c.Category.ToString(),
                            c.PlannedAmount, c.ActualAmount, c.Variance))
                        .ToList();
                    return new DepartmentSummaryLineDto(d.Id.Value, d.Code, d.Name, true,
                        r.PlannedRevenue, r.ActualRevenue, r.PlannedCost, r.ActualCost,
                        plannedProfit, actualProfit, actualProfit - plannedProfit, categories);
                }
                return new DepartmentSummaryLineDto(d.Id.Value, d.Code, d.Name, false,
                    0m, 0m, 0m, 0m, 0m, 0m, 0m, []);
            })
            .ToList();

        // 部承認の状態。配下課が1件以上かつ全課が承認済み予算を持つとき部承認が可能。
        var approval = await _divisionApprovals.FindAsync(divId, half, ct);
        var canApprove = departments.Count > 0 && lines.All(l => l.HasApprovedBudget);

        return new DivisionBudgetSummaryDto(
            summary.Categories
                .Select(c => new CategorySummaryDto(c.Category.ToString(),
                    c.PlannedAmount, c.ActualAmount, c.Variance))
                .ToList(),
            summary.PlannedRevenue, summary.ActualRevenue, summary.RevenueVariance,
            summary.PlannedCost, summary.ActualCost, summary.CostVariance,
            summary.PlannedProfit, summary.ActualProfit, summary.ProfitVariance,
            lines,
            approval is not null, approval?.ApprovedAt, canApprove);
    }

    /// <summary>
    /// 分析基準の予算を決定する内部ヘルパ。budgetId 指定時はその予算(課・半期の一致を検証)、
    /// 未指定時は最新の承認済み予算を返す。該当なしは <see cref="NotFoundException"/>。
    /// </summary>
    private async Task<DepartmentBudget> ResolveBudgetAsync(DepartmentId departmentId,
        FiscalHalf fiscalHalf, Guid? budgetId, CancellationToken ct)
    {
        if (budgetId is { } id)
        {
            var budget = await _budgets.FindByIdAsync(new DepartmentBudgetId(id), ct)
                ?? throw new NotFoundException($"予算が見つかりません: {id}");
            if (budget.DepartmentId != departmentId || budget.FiscalHalf != fiscalHalf)
                throw new DomainException("指定された予算はこの課・半期のものではありません。");
            return budget;
        }
        return await _budgets.FindLatestApprovedAsync(departmentId, fiscalHalf, ct)
            ?? throw new NotFoundException("承認済みの予算が存在しません。先に予算を承認してください。");
    }

    /// <summary>案件・費目の名称解決用のルックアップ。</summary>
    private async Task<NameLookup> LoadNamesAsync(DepartmentId departmentId, CancellationToken ct)
    {
        var projects = await _projects.ListByDepartmentAsync(departmentId, ct);
        var elements = await _elements.ListAsync(ct);
        return new NameLookup(
            projects.ToDictionary(p => p.Id.Value, p => (p.Code, p.Name)),
            elements.ToDictionary(e => e.Code.Value, e => e.Name));
    }

    /// <summary>案件ID・費目コードから表示名を引くための内部ルックアップ。</summary>
    private sealed record NameLookup(
        IReadOnlyDictionary<Guid, (string Code, string Name)> Projects,
        IReadOnlyDictionary<string, string> Elements)
    {
        /// <summary>案件IDから案件名を引く。無ければ null。</summary>
        public string? ProjectName(Guid? projectId) =>
            projectId is { } id && Projects.TryGetValue(id, out var p) ? p.Name : null;

        /// <summary>案件IDから案件コードを引く。無ければ空文字。</summary>
        public string ProjectCode(Guid projectId) =>
            Projects.TryGetValue(projectId, out var p) ? p.Code : "";

        /// <summary>費目コードから費目名を引く。無ければ null。</summary>
        public string? ElementName(string? elementCode) =>
            elementCode is { } code && Elements.TryGetValue(code, out var name) ? name : null;
    }
}

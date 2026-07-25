using CostManagement.Application.Common;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>
/// 課の半期予算の策定・改定・承認に関するユースケース。
/// 予算は (課, 年度, 半期) ごとにバージョン管理され、半期の途中でも改定できる。
/// </summary>
public sealed class DepartmentBudgetService
{
    private readonly IDepartmentBudgetRepository _budgets;
    private readonly IDepartmentRepository _departments;
    private readonly IProjectRepository _projects;
    private readonly ICostElementRepository _elements;
    private readonly ISystemClock _clock;

    /// <summary>依存する課予算・課・案件・費目の各リポジトリと時計を受け取る。</summary>
    public DepartmentBudgetService(IDepartmentBudgetRepository budgets,
        IDepartmentRepository departments, IProjectRepository projects,
        ICostElementRepository elements, ISystemClock clock)
    {
        _budgets = budgets;
        _departments = departments;
        _projects = projects;
        _elements = elements;
        _clock = clock;
    }

    /// <summary>
    /// 予算ドラフトを起票する。初回は当初予算(バージョン1)、
    /// 以降は既存バージョン(未指定時は最新承認版)を引き継いだ改定版となる。
    /// </summary>
    public async Task<BudgetDetailDto> CreateDraftAsync(Guid departmentId,
        CreateBudgetRequest request, CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        _ = await _departments.FindByIdAsync(did, ct)
            ?? throw new NotFoundException($"課が見つかりません: {departmentId}");

        var fiscalHalf = FiscalHalf.Parse(request.FiscalHalf);
        var existing = await _budgets.ListAsync(did, fiscalHalf, ct);
        if (existing.Any(b => b.Status == BudgetStatus.Draft))
            throw new DomainException("策定中のドラフトが既に存在します。承認または破棄してから新しい改定版を作成してください。");

        DepartmentBudget draft;
        if (existing.Count == 0)
        {
            draft = DepartmentBudget.CreateInitial(did, fiscalHalf, request.Label, _clock.UtcNow);
        }
        else
        {
            var baseBudget = request.BaseBudgetId is { } baseId
                ? existing.FirstOrDefault(b => b.Id.Value == baseId)
                  ?? throw new NotFoundException($"基となる予算が見つかりません: {baseId}")
                : existing.Where(b => b.Status != BudgetStatus.Draft)
                      .OrderByDescending(b => b.Version).First();
            var nextVersion = await _budgets.GetMaxVersionAsync(did, fiscalHalf, ct) + 1;
            draft = DepartmentBudget.ReviseFrom(baseBudget, nextVersion, request.Label, _clock.UtcNow);
        }

        await _budgets.AddAsync(draft, ct);
        return ToDetailDto(draft);
    }

    /// <summary>(課, 半期)の全バージョンをバージョン降順で取得する。</summary>
    public async Task<IReadOnlyList<BudgetSummaryDto>> ListAsync(Guid departmentId,
        string fiscalHalf, CancellationToken ct = default)
    {
        var budgets = await _budgets.ListAsync(new DepartmentId(departmentId),
            FiscalHalf.Parse(fiscalHalf), ct);
        return budgets.OrderByDescending(b => b.Version).Select(ToSummaryDto).ToList();
    }

    /// <summary>予算を明細込みで取得する。存在しなければ <see cref="NotFoundException"/>。</summary>
    public async Task<BudgetDetailDto> GetAsync(Guid budgetId, CancellationToken ct = default) =>
        ToDetailDto(await RequireAsync(budgetId, ct));

    /// <summary>
    /// 明細を追加または更新する(区分に応じて案件別 or 費目別)。
    /// 承認済み予算は編集不可(ドメイン側で拒否)。
    /// </summary>
    public async Task<BudgetDetailDto> UpsertLineAsync(Guid budgetId,
        UpsertBudgetLineRequest request, CancellationToken ct = default)
    {
        var budget = await RequireAsync(budgetId, ct);
        var category = ParseCategory(request.Category);
        // MonthlyAmounts が来たら月次モード、無ければ半期一括モード。
        var monthly = request.MonthlyAmounts is { } ma
            ? ma.ToDictionary(kv => kv.Key, kv => new Money(kv.Value))
            : null;

        if (category.IsProjectBased())
        {
            var projectId = await RequireProjectInDepartmentAsync(request.ProjectId,
                budget.DepartmentId, ct);
            if (monthly is not null)
                budget.UpsertProjectLineMonthly(category, projectId, monthly);
            else
                budget.UpsertProjectLine(category, projectId, new Money(request.Amount));
        }
        else
        {
            var elementCode = await RequireElementAsync(request.ElementCode, ct);
            if (monthly is not null)
                budget.UpsertPeriodCostLineMonthly(elementCode, monthly, request.PeriodDetail);
            else
                budget.UpsertPeriodCostLine(elementCode, new Money(request.Amount), request.PeriodDetail);
        }

        await _budgets.UpdateAsync(budget, ct);
        return ToDetailDto(budget);
    }

    /// <summary>明細を削除する(区分に応じて案件別 or 費目別)。承認済み予算は編集不可。</summary>
    public async Task<BudgetDetailDto> RemoveLineAsync(Guid budgetId, string category,
        Guid? projectId, string? elementCode, string? periodDetail = null,
        CancellationToken ct = default)
    {
        var budget = await RequireAsync(budgetId, ct);
        var parsed = ParseCategory(category);

        if (parsed.IsProjectBased())
        {
            if (projectId is not { } pid)
                throw new DomainException("売上高・加工費・外注費の明細には案件を指定してください。");
            budget.RemoveProjectLine(parsed, new ProjectId(pid));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(elementCode))
                throw new DomainException("期間費用の明細には費目を指定してください。");
            budget.RemovePeriodCostLine(new CostElementCode(elementCode), periodDetail);
        }

        await _budgets.UpdateAsync(budget, ct);
        return ToDetailDto(budget);
    }

    /// <summary>
    /// 期間費用の明細名を変更する(金額・月次モードは保持)。承認済み予算は編集不可。
    /// 実績は明細名で疎結合のため改名の対象外(旧名の実績は分析で「予定外」になる)。
    /// </summary>
    public async Task<BudgetDetailDto> RenamePeriodDetailAsync(Guid budgetId,
        RenamePeriodDetailRequest request, CancellationToken ct = default)
    {
        var budget = await RequireAsync(budgetId, ct);
        if (string.IsNullOrWhiteSpace(request.ElementCode))
            throw new DomainException("期間費用の明細には費目を指定してください。");
        budget.RenamePeriodCostDetail(new CostElementCode(request.ElementCode),
            request.OldDetail, request.NewDetail);
        await _budgets.UpdateAsync(budget, ct);
        return ToDetailDto(budget);
    }

    /// <summary>予算を承認する。同一 (課, 半期) の承認済みバージョンは失効(Superseded)となる。</summary>
    public async Task<BudgetDetailDto> ApproveAsync(Guid budgetId, CancellationToken ct = default)
    {
        var budget = await RequireAsync(budgetId, ct);
        budget.Approve(_clock.UtcNow);

        var siblings = await _budgets.ListAsync(budget.DepartmentId, budget.FiscalHalf, ct);
        foreach (var sibling in siblings.Where(b =>
                     b.Id != budget.Id && b.Status == BudgetStatus.Approved))
        {
            sibling.Supersede();
            await _budgets.UpdateAsync(sibling, ct);
        }

        await _budgets.UpdateAsync(budget, ct);
        return ToDetailDto(budget);
    }

    /// <summary>区分の文字列を <see cref="BudgetCategory"/> に変換する。不正値は <see cref="DomainException"/>。</summary>
    internal static BudgetCategory ParseCategory(string category)
    {
        if (!Enum.TryParse<BudgetCategory>(category, ignoreCase: false, out var parsed))
            throw new DomainException($"予算区分が不正です: {category}");
        return parsed;
    }

    /// <summary>案件が存在し、かつ指定の課に属していることを検証してIDを返す内部ヘルパ。</summary>
    private async Task<ProjectId> RequireProjectInDepartmentAsync(Guid? projectId,
        DepartmentId departmentId, CancellationToken ct)
    {
        if (projectId is not { } pid)
            throw new DomainException("売上高・加工費・外注費の明細には案件を指定してください。");
        var project = await _projects.FindByIdAsync(new ProjectId(pid), ct)
            ?? throw new NotFoundException($"案件が見つかりません: {pid}");
        if (project.DepartmentId != departmentId)
            throw new DomainException("指定された案件はこの課に属していません。");
        return project.Id;
    }

    /// <summary>費目コードが必須かつマスタに存在することを検証して返す内部ヘルパ。</summary>
    private async Task<CostElementCode> RequireElementAsync(string? elementCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(elementCode))
            throw new DomainException("期間費用の明細には費目を指定してください。");
        var code = new CostElementCode(elementCode);
        _ = await _elements.FindByCodeAsync(code, ct)
            ?? throw new NotFoundException($"費目が見つかりません: {elementCode}");
        return code;
    }

    /// <summary>IDで予算を取得する。無ければ <see cref="NotFoundException"/> を投げる内部ヘルパ。</summary>
    private async Task<DepartmentBudget> RequireAsync(Guid budgetId, CancellationToken ct) =>
        await _budgets.FindByIdAsync(new DepartmentBudgetId(budgetId), ct)
        ?? throw new NotFoundException($"予算が見つかりません: {budgetId}");

    /// <summary>予算を一覧用サマリ DTO(区分合計・計画損益つき)へ変換する。</summary>
    internal static BudgetSummaryDto ToSummaryDto(DepartmentBudget b) =>
        new(b.Id.Value, b.DepartmentId.Value, b.FiscalHalf.ToString(), b.Version, b.Label,
            b.Status.ToString(), b.CreatedAt, b.ApprovedAt,
            b.CategoryTotal(BudgetCategory.Revenue).Value,
            b.CategoryTotal(BudgetCategory.Processing).Value,
            b.CategoryTotal(BudgetCategory.Outsourcing).Value,
            b.CategoryTotal(BudgetCategory.PeriodCost).Value,
            b.PlannedProfit.Value);

    /// <summary>予算を明細つき詳細 DTO(区分順→費目→案件で整列)へ変換する。</summary>
    internal static BudgetDetailDto ToDetailDto(DepartmentBudget b) =>
        new(b.Id.Value, b.DepartmentId.Value, b.FiscalHalf.ToString(), b.Version, b.Label,
            b.Status.ToString(), b.CreatedAt, b.ApprovedAt,
            b.CategoryTotal(BudgetCategory.Revenue).Value,
            b.CategoryTotal(BudgetCategory.Processing).Value,
            b.CategoryTotal(BudgetCategory.Outsourcing).Value,
            b.CategoryTotal(BudgetCategory.PeriodCost).Value,
            b.PlannedProfit.Value,
            b.Lines
                .OrderBy(l => l.Category)
                .ThenBy(l => l.ElementCode?.Value)
                .ThenBy(l => l.PeriodDetail)
                .ThenBy(l => l.ProjectId?.Value)
                .Select(l => new BudgetLineDto(l.Id, l.Category.ToString(),
                    l.ProjectId?.Value, l.ElementCode?.Value, l.PeriodDetail, l.Amount.Value,
                    l.IsMonthly, l.MonthlyAmounts.ToDictionary(m => m.Key, m => m.Value.Value)))
                .ToList());
}

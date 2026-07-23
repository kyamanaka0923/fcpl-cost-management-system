using CostManagement.Application.Common;
using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>実績の計上・参照ユースケース。同一キーに複数回計上でき、分析時に合算される。</summary>
public sealed class ActualEntryService
{
    private readonly IActualEntryRepository _actuals;
    private readonly IDepartmentRepository _departments;
    private readonly IProjectRepository _projects;
    private readonly ICostElementRepository _elements;
    private readonly ISystemClock _clock;

    /// <summary>依存する実績・課・案件・費目の各リポジトリと時計を受け取る。</summary>
    public ActualEntryService(IActualEntryRepository actuals, IDepartmentRepository departments,
        IProjectRepository projects, ICostElementRepository elements, ISystemClock clock)
    {
        _actuals = actuals;
        _departments = departments;
        _projects = projects;
        _elements = elements;
        _clock = clock;
    }

    /// <summary>
    /// 実績を計上する。区分に応じて案件(同一課所属)または費目の指定を検証し、
    /// 未検出は <see cref="NotFoundException"/>、不整合は <see cref="DomainException"/>。
    /// </summary>
    public async Task<ActualEntryDto> RecordAsync(Guid departmentId, RecordActualRequest request,
        CancellationToken ct = default)
    {
        var did = new DepartmentId(departmentId);
        _ = await _departments.FindByIdAsync(did, ct)
            ?? throw new NotFoundException($"課が見つかりません: {departmentId}");

        var fiscalHalf = FiscalHalf.Parse(request.FiscalHalf);
        var category = DepartmentBudgetService.ParseCategory(request.Category);

        ProjectId? projectId = null;
        CostElementCode? elementCode = null;
        if (category.IsProjectBased())
        {
            if (request.ProjectId is not { } pid)
                throw new DomainException("売上高・加工費・外注費の実績には案件を指定してください。");
            var project = await _projects.FindByIdAsync(new ProjectId(pid), ct)
                ?? throw new NotFoundException($"案件が見つかりません: {pid}");
            if (project.DepartmentId != did)
                throw new DomainException("指定された案件はこの課に属していません。");
            projectId = project.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ElementCode))
                throw new DomainException("期間費用の実績には費目を指定してください。");
            var code = new CostElementCode(request.ElementCode);
            _ = await _elements.FindByCodeAsync(code, ct)
                ?? throw new NotFoundException($"費目が見つかりません: {request.ElementCode}");
            elementCode = code;
        }

        var entry = ActualEntry.Record(did, fiscalHalf, category, projectId, elementCode,
            request.Month, new Money(request.Amount), request.Note, _clock.UtcNow, request.PeriodDetail);
        await _actuals.AddAsync(entry, ct);
        return ToDto(entry);
    }

    /// <summary>(課, 半期)の実績を計上日時の新しい順で取得する。</summary>
    public async Task<IReadOnlyList<ActualEntryDto>> ListAsync(Guid departmentId,
        string fiscalHalf, CancellationToken ct = default)
    {
        var entries = await _actuals.ListAsync(new DepartmentId(departmentId),
            FiscalHalf.Parse(fiscalHalf), ct);
        return entries
            .OrderByDescending(e => e.RecordedAt)
            .Select(ToDto)
            .ToList();
    }

    /// <summary>実績を1件削除する。存在しなければ <see cref="NotFoundException"/>。</summary>
    public async Task DeleteAsync(Guid actualId, CancellationToken ct = default)
    {
        var id = new ActualEntryId(actualId);
        _ = await _actuals.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"実績が見つかりません: {actualId}");
        await _actuals.DeleteAsync(id, ct);
    }

    /// <summary>ドメインの実績を応答 DTO へ変換する。</summary>
    internal static ActualEntryDto ToDto(ActualEntry e) =>
        new(e.Id.Value, e.DepartmentId.Value, e.FiscalHalf.ToString(), e.Category.ToString(),
            e.ProjectId?.Value, e.ElementCode?.Value, e.PeriodDetail, e.Month, e.Amount.Value,
            e.Note, e.RecordedAt);
}

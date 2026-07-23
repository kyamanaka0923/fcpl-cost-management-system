using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Actuals;

/// <summary>実績を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct ActualEntryId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static ActualEntryId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 実績。集約ルート。予算明細と同じ軸
/// (課, 半期, 区分, 案件 or 費目)で計上する。
/// 同一キーに複数回計上でき、分析時に合算される。
/// </summary>
public sealed class ActualEntry
{
    /// <summary>実績ID。</summary>
    public ActualEntryId Id { get; }

    /// <summary>計上先の課ID。</summary>
    public DepartmentId DepartmentId { get; }

    /// <summary>計上対象の半期。</summary>
    public FiscalHalf FiscalHalf { get; }

    /// <summary>予算区分(売上高/加工費/外注費/期間費用)。</summary>
    public BudgetCategory Category { get; }

    /// <summary>案件。売上高・加工費・外注費の実績で必須。期間費用では null。</summary>
    public ProjectId? ProjectId { get; }

    /// <summary>費目。期間費用の実績で必須。案件別区分では null。</summary>
    public CostElementCode? ElementCode { get; }

    /// <summary>
    /// 期間費用の明細名(計画明細に対応する実績のときに指定。費目一括や案件別区分では null)。
    /// 計画にない明細名でも計上でき、分析では「予定外」として表示される。
    /// </summary>
    public string? PeriodDetail { get; }

    /// <summary>計上対象の月(半期内の 1..6)。半期一括で計上した場合は null。</summary>
    public int? Month { get; }

    /// <summary>計上金額(0以上)。</summary>
    public Money Amount { get; }

    /// <summary>備考(任意)。</summary>
    public string? Note { get; }

    /// <summary>計上日時(UTC)。</summary>
    public DateTime RecordedAt { get; }

    private ActualEntry(ActualEntryId id, DepartmentId departmentId, FiscalHalf fiscalHalf,
        BudgetCategory category, ProjectId? projectId, CostElementCode? elementCode,
        string? periodDetail, int? month, Money amount, string? note, DateTime recordedAt)
    {
        Id = id;
        DepartmentId = departmentId;
        FiscalHalf = fiscalHalf;
        Category = category;
        ProjectId = projectId;
        ElementCode = elementCode;
        PeriodDetail = periodDetail;
        Month = month;
        Amount = amount;
        Note = note;
        RecordedAt = recordedAt;
    }

    /// <summary>
    /// 実績を計上する。区分と案件/費目の排他(<see cref="BudgetCategories.ValidateKey"/>)を検証し、
    /// 金額は0以上を要求する。month を指定すると特定月の計上(1..6)、null なら半期一括の計上。
    /// periodDetail は期間費用の明細名(費目一括や案件別区分では null)。
    /// </summary>
    public static ActualEntry Record(DepartmentId departmentId, FiscalHalf fiscalHalf,
        BudgetCategory category, ProjectId? projectId, CostElementCode? elementCode,
        int? month, Money amount, string? note, DateTime now, string? periodDetail = null)
    {
        BudgetCategories.ValidateKey(category, projectId, elementCode);
        if (month is { } m)
            HalfMonths.Validate(m);
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
        var detail = BudgetLine.NormalizeDetail(periodDetail);
        if (detail is not null && category != BudgetCategory.PeriodCost)
            throw new DomainException("明細名は期間費用の実績にのみ指定できます。");
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new ActualEntry(ActualEntryId.New(), departmentId, fiscalHalf, category,
            projectId, elementCode, detail, month, amount, trimmedNote, now);
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static ActualEntry Restore(Guid id, Guid departmentId, string fiscalHalf,
        string category, Guid? projectId, string? elementCode, string? periodDetail, int? month,
        decimal amount, string? note, DateTime recordedAt) =>
        new(new ActualEntryId(id), new DepartmentId(departmentId), FiscalHalf.Parse(fiscalHalf),
            Enum.Parse<BudgetCategory>(category),
            projectId is { } pid ? new ProjectId(pid) : null,
            elementCode is { } code ? new CostElementCode(code) : null,
            BudgetLine.NormalizeDetail(periodDetail), month, new Money(amount), note, recordedAt);
}

/// <summary>実績の永続化ポート(実装はインフラ層)。</summary>
public interface IActualEntryRepository
{
    /// <summary>IDで実績を1件取得する。無ければ null。</summary>
    Task<ActualEntry?> FindByIdAsync(ActualEntryId id, CancellationToken ct = default);

    /// <summary>(課, 半期)の実績を全件取得する(分析時に区分・キーで合算する)。</summary>
    Task<IReadOnlyList<ActualEntry>> ListAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);

    /// <summary>実績を1件追加する。</summary>
    Task AddAsync(ActualEntry entry, CancellationToken ct = default);

    /// <summary>実績を1件削除する。</summary>
    Task DeleteAsync(ActualEntryId id, CancellationToken ct = default);
}

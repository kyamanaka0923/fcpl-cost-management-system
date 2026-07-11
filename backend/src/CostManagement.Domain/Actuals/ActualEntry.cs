using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Actuals;

public readonly record struct ActualEntryId(Guid Value)
{
    public static ActualEntryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 実績。集約ルート。予算明細と同じ軸
/// (課, 半期, 区分, 案件 or 費目)で計上する。
/// 同一キーに複数回計上でき、分析時に合算される。
/// </summary>
public sealed class ActualEntry
{
    public ActualEntryId Id { get; }
    public DepartmentId DepartmentId { get; }
    public FiscalHalf FiscalHalf { get; }
    public BudgetCategory Category { get; }

    /// <summary>案件。売上高・加工費・外注費の実績で必須。期間費用では null。</summary>
    public ProjectId? ProjectId { get; }

    /// <summary>費目。期間費用の実績で必須。案件別区分では null。</summary>
    public CostElementCode? ElementCode { get; }

    public Money Amount { get; }
    public string? Note { get; }
    public DateTime RecordedAt { get; }

    private ActualEntry(ActualEntryId id, DepartmentId departmentId, FiscalHalf fiscalHalf,
        BudgetCategory category, ProjectId? projectId, CostElementCode? elementCode,
        Money amount, string? note, DateTime recordedAt)
    {
        Id = id;
        DepartmentId = departmentId;
        FiscalHalf = fiscalHalf;
        Category = category;
        ProjectId = projectId;
        ElementCode = elementCode;
        Amount = amount;
        Note = note;
        RecordedAt = recordedAt;
    }

    public static ActualEntry Record(DepartmentId departmentId, FiscalHalf fiscalHalf,
        BudgetCategory category, ProjectId? projectId, CostElementCode? elementCode,
        Money amount, string? note, DateTime now)
    {
        BudgetCategories.ValidateKey(category, projectId, elementCode);
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new ActualEntry(ActualEntryId.New(), departmentId, fiscalHalf, category,
            projectId, elementCode, amount, trimmedNote, now);
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static ActualEntry Restore(Guid id, Guid departmentId, string fiscalHalf,
        string category, Guid? projectId, string? elementCode, decimal amount,
        string? note, DateTime recordedAt) =>
        new(new ActualEntryId(id), new DepartmentId(departmentId), FiscalHalf.Parse(fiscalHalf),
            Enum.Parse<BudgetCategory>(category),
            projectId is { } pid ? new ProjectId(pid) : null,
            elementCode is { } code ? new CostElementCode(code) : null,
            new Money(amount), note, recordedAt);
}

public interface IActualEntryRepository
{
    Task<ActualEntry?> FindByIdAsync(ActualEntryId id, CancellationToken ct = default);
    Task<IReadOnlyList<ActualEntry>> ListAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);
    Task AddAsync(ActualEntry entry, CancellationToken ct = default);
    Task DeleteAsync(ActualEntryId id, CancellationToken ct = default);
}

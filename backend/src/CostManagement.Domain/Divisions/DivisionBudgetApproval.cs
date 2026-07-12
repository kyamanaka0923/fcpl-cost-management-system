using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Divisions;

public readonly record struct DivisionBudgetApprovalId(Guid Value)
{
    public static DivisionBudgetApprovalId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 部の予算承認。(部, 半期) ごとに1件。レコードが存在する = 承認済み。
/// 課の承認フローとは独立し、取り消し(削除)できる。集約ルート。
/// 承認の可否(配下全課が承認済みか)の判定は Application 層が行う。
/// </summary>
public sealed class DivisionBudgetApproval
{
    public DivisionBudgetApprovalId Id { get; }
    public DivisionId DivisionId { get; }
    public FiscalHalf FiscalHalf { get; }
    public DateTime ApprovedAt { get; }

    private DivisionBudgetApproval(DivisionBudgetApprovalId id, DivisionId divisionId,
        FiscalHalf fiscalHalf, DateTime approvedAt)
    {
        Id = id;
        DivisionId = divisionId;
        FiscalHalf = fiscalHalf;
        ApprovedAt = approvedAt;
    }

    public static DivisionBudgetApproval Approve(DivisionId divisionId, FiscalHalf fiscalHalf,
        DateTime now) =>
        new(DivisionBudgetApprovalId.New(), divisionId, fiscalHalf, now);

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static DivisionBudgetApproval Restore(Guid id, Guid divisionId, string fiscalHalf,
        DateTime approvedAt) =>
        new(new DivisionBudgetApprovalId(id), new DivisionId(divisionId),
            Domain.Shared.FiscalHalf.Parse(fiscalHalf), approvedAt);
}

public interface IDivisionBudgetApprovalRepository
{
    Task<DivisionBudgetApproval?> FindAsync(DivisionId divisionId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);
    Task AddAsync(DivisionBudgetApproval approval, CancellationToken ct = default);
    Task DeleteAsync(DivisionId divisionId, FiscalHalf fiscalHalf, CancellationToken ct = default);
}

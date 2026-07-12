using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Divisions;

/// <summary>部予算承認を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct DivisionBudgetApprovalId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static DivisionBudgetApprovalId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 部の予算承認。(部, 半期) ごとに1件。レコードが存在する = 承認済み。
/// 課の承認フローとは独立し、取り消し(削除)できる。集約ルート。
/// 承認の可否(配下全課が承認済みか)の判定は Application 層が行う。
/// </summary>
public sealed class DivisionBudgetApproval
{
    /// <summary>部予算承認ID。</summary>
    public DivisionBudgetApprovalId Id { get; }

    /// <summary>承認対象の部ID。</summary>
    public DivisionId DivisionId { get; }

    /// <summary>承認対象の半期。</summary>
    public FiscalHalf FiscalHalf { get; }

    /// <summary>承認日時(UTC)。</summary>
    public DateTime ApprovedAt { get; }

    private DivisionBudgetApproval(DivisionBudgetApprovalId id, DivisionId divisionId,
        FiscalHalf fiscalHalf, DateTime approvedAt)
    {
        Id = id;
        DivisionId = divisionId;
        FiscalHalf = fiscalHalf;
        ApprovedAt = approvedAt;
    }

    /// <summary>(部, 半期)の承認レコードを新規に作る。承認可否の判定は Application 層の責務。</summary>
    public static DivisionBudgetApproval Approve(DivisionId divisionId, FiscalHalf fiscalHalf,
        DateTime now) =>
        new(DivisionBudgetApprovalId.New(), divisionId, fiscalHalf, now);

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static DivisionBudgetApproval Restore(Guid id, Guid divisionId, string fiscalHalf,
        DateTime approvedAt) =>
        new(new DivisionBudgetApprovalId(id), new DivisionId(divisionId),
            Domain.Shared.FiscalHalf.Parse(fiscalHalf), approvedAt);
}

/// <summary>部予算承認の永続化ポート(実装はインフラ層)。承認状態はレコードの有無で表す。</summary>
public interface IDivisionBudgetApprovalRepository
{
    /// <summary>(部, 半期)の承認レコードを取得する。無ければ null(= 未承認)。</summary>
    Task<DivisionBudgetApproval?> FindAsync(DivisionId divisionId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);

    /// <summary>承認レコードを追加する(= 承認する)。</summary>
    Task AddAsync(DivisionBudgetApproval approval, CancellationToken ct = default);

    /// <summary>(部, 半期)の承認レコードを削除する(= 承認を取り消す)。</summary>
    Task DeleteAsync(DivisionId divisionId, FiscalHalf fiscalHalf, CancellationToken ct = default);
}

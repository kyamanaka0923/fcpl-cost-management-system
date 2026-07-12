using CostManagement.Application.Common;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>
/// 部の予算承認・取り消しのユースケース。
/// 部承認は配下の全課がその半期の承認済み予算を持つときのみ可能。
/// 課の承認フローとは独立し、部承認後に課が改定しても部承認は残る。
/// </summary>
public sealed class DivisionBudgetApprovalService
{
    private readonly IDivisionBudgetApprovalRepository _approvals;
    private readonly IDivisionRepository _divisions;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentBudgetRepository _budgets;
    private readonly ISystemClock _clock;

    /// <summary>依存する部承認・部・課・課予算の各リポジトリと時計を受け取る。</summary>
    public DivisionBudgetApprovalService(IDivisionBudgetApprovalRepository approvals,
        IDivisionRepository divisions, IDepartmentRepository departments,
        IDepartmentBudgetRepository budgets, ISystemClock clock)
    {
        _approvals = approvals;
        _divisions = divisions;
        _departments = departments;
        _budgets = budgets;
        _clock = clock;
    }

    /// <summary>部予算を承認する。配下の全課が承認済み予算を持つ必要がある。</summary>
    public async Task ApproveAsync(Guid divisionId, string fiscalHalf, CancellationToken ct = default)
    {
        var divId = new DivisionId(divisionId);
        _ = await _divisions.FindByIdAsync(divId, ct)
            ?? throw new NotFoundException($"部が見つかりません: {divisionId}");
        var half = FiscalHalf.Parse(fiscalHalf);

        var departments = await _departments.ListByDivisionAsync(divId, ct);
        if (departments.Count == 0)
            throw new DomainException("承認する課がありません。");

        foreach (var dept in departments)
        {
            var approved = await _budgets.FindLatestApprovedAsync(dept.Id, half, ct);
            if (approved is null)
                throw new DomainException(
                    "未承認の予算がある課があります。先に各課の予算を承認してください。");
        }

        // 既存承認があれば取り消して承認し直す(ApprovedAt を最新にする)。
        var existing = await _approvals.FindAsync(divId, half, ct);
        if (existing is not null)
            await _approvals.DeleteAsync(divId, half, ct);
        await _approvals.AddAsync(DivisionBudgetApproval.Approve(divId, half, _clock.UtcNow), ct);
    }

    /// <summary>部予算の承認を取り消す(未承認に戻す)。</summary>
    public async Task RevokeAsync(Guid divisionId, string fiscalHalf, CancellationToken ct = default)
    {
        var divId = new DivisionId(divisionId);
        _ = await _divisions.FindByIdAsync(divId, ct)
            ?? throw new NotFoundException($"部が見つかりません: {divisionId}");
        await _approvals.DeleteAsync(divId, FiscalHalf.Parse(fiscalHalf), ct);
    }
}

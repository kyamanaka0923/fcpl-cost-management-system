using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Actuals;

public readonly record struct ActualCostId(Guid Value)
{
    public static ActualCostId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 原価実績。集約ルート。金額を直接持つ(数量×単価では管理しない)。
/// 同一の (費目, 売上対応品目, 会計期間) に複数の実績を計上でき、分析時に合算される。
/// </summary>
public sealed class ActualCost
{
    public ActualCostId Id { get; }
    public ProjectId ProjectId { get; }
    public CostElementCode ElementCode { get; }

    /// <summary>売上対応品目。どの売上(品目)に対応する原価かを表す。null = 共通費。</summary>
    public string? RevenueItem { get; }

    public AccountingPeriod Period { get; }
    public Money Amount { get; }
    public string? Note { get; }
    public DateTime RecordedAt { get; }

    private ActualCost(ActualCostId id, ProjectId projectId, CostElementCode elementCode,
        string? revenueItem, AccountingPeriod period, Money amount, string? note,
        DateTime recordedAt)
    {
        Id = id;
        ProjectId = projectId;
        ElementCode = elementCode;
        RevenueItem = revenueItem;
        Period = period;
        Amount = amount;
        Note = note;
        RecordedAt = recordedAt;
    }

    public static ActualCost Record(ProjectId projectId, CostElementCode elementCode,
        string? revenueItem, AccountingPeriod period, Money amount, string? note, DateTime now)
    {
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
        return new ActualCost(ActualCostId.New(), projectId, elementCode,
            CostPlan.NormalizeRevenueItem(revenueItem), period, amount,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now);
    }

    public static ActualCost Restore(Guid id, Guid projectId, string elementCode,
        string? revenueItem, string period, decimal amount, string? note, DateTime recordedAt) =>
        new(new ActualCostId(id), new ProjectId(projectId), new CostElementCode(elementCode),
            CostPlan.NormalizeRevenueItem(revenueItem), AccountingPeriod.Parse(period),
            new Money(amount), note, recordedAt);
}

public interface IActualCostRepository
{
    Task<ActualCost?> FindByIdAsync(ActualCostId id, CancellationToken ct = default);
    Task<IReadOnlyList<ActualCost>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(ActualCost actual, CancellationToken ct = default);
    Task DeleteAsync(ActualCostId id, CancellationToken ct = default);
}

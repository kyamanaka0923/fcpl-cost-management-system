using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Revenue;

public readonly record struct ActualRevenueId(Guid Value)
{
    public static ActualRevenueId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 売上実績。集約ルート。金額を直接持つ(数量×単価では管理しない)。
/// 同一の (品目, 会計期間) に複数の実績を計上でき、分析時に合算される。
/// </summary>
public sealed class ActualRevenue
{
    public ActualRevenueId Id { get; }
    public ProjectId ProjectId { get; }
    public string ItemName { get; }
    public AccountingPeriod Period { get; }
    public Money Amount { get; }
    public string? Note { get; }
    public DateTime RecordedAt { get; }

    private ActualRevenue(ActualRevenueId id, ProjectId projectId, string itemName,
        AccountingPeriod period, Money amount, string? note, DateTime recordedAt)
    {
        Id = id;
        ProjectId = projectId;
        ItemName = itemName;
        Period = period;
        Amount = amount;
        Note = note;
        RecordedAt = recordedAt;
    }

    public static ActualRevenue Record(ProjectId projectId, string itemName,
        AccountingPeriod period, Money amount, string? note, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("品目名は必須です。");
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
        return new ActualRevenue(ActualRevenueId.New(), projectId, itemName.Trim(), period,
            amount, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now);
    }

    public static ActualRevenue Restore(Guid id, Guid projectId, string itemName,
        string period, decimal amount, string? note, DateTime recordedAt) =>
        new(new ActualRevenueId(id), new ProjectId(projectId), itemName,
            AccountingPeriod.Parse(period), new Money(amount), note, recordedAt);
}

public interface IActualRevenueRepository
{
    Task<ActualRevenue?> FindByIdAsync(ActualRevenueId id, CancellationToken ct = default);
    Task<IReadOnlyList<ActualRevenue>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(ActualRevenue actual, CancellationToken ct = default);
    Task DeleteAsync(ActualRevenueId id, CancellationToken ct = default);
}

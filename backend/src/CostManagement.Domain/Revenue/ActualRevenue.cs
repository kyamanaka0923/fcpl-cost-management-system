using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Revenue;

public readonly record struct ActualRevenueId(Guid Value)
{
    public static ActualRevenueId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 売上実績。集約ルート。
/// 同一の (品目, 会計期間) に複数の実績を計上でき、分析時に合算される。
/// </summary>
public sealed class ActualRevenue
{
    public ActualRevenueId Id { get; }
    public ProjectId ProjectId { get; }
    public string ItemName { get; }
    public AccountingPeriod Period { get; }
    public decimal Quantity { get; }
    public Money UnitPrice { get; }
    public string? Note { get; }
    public DateTime RecordedAt { get; }

    public Money Amount => UnitPrice * Quantity;

    private ActualRevenue(ActualRevenueId id, ProjectId projectId, string itemName,
        AccountingPeriod period, decimal quantity, Money unitPrice, string? note,
        DateTime recordedAt)
    {
        Id = id;
        ProjectId = projectId;
        ItemName = itemName;
        Period = period;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Note = note;
        RecordedAt = recordedAt;
    }

    public static ActualRevenue Record(ProjectId projectId, string itemName,
        AccountingPeriod period, decimal quantity, Money unitPrice, string? note, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("品目名は必須です。");
        if (quantity < 0m)
            throw new DomainException("数量は0以上で入力してください。");
        if (unitPrice.IsNegative)
            throw new DomainException("単価は0以上で入力してください。");
        return new ActualRevenue(ActualRevenueId.New(), projectId, itemName.Trim(), period,
            quantity, unitPrice, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now);
    }

    public static ActualRevenue Restore(Guid id, Guid projectId, string itemName,
        string period, decimal quantity, decimal unitPrice, string? note, DateTime recordedAt) =>
        new(new ActualRevenueId(id), new ProjectId(projectId), itemName,
            AccountingPeriod.Parse(period), quantity, new Money(unitPrice), note, recordedAt);
}

public interface IActualRevenueRepository
{
    Task<ActualRevenue?> FindByIdAsync(ActualRevenueId id, CancellationToken ct = default);
    Task<IReadOnlyList<ActualRevenue>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(ActualRevenue actual, CancellationToken ct = default);
    Task DeleteAsync(ActualRevenueId id, CancellationToken ct = default);
}

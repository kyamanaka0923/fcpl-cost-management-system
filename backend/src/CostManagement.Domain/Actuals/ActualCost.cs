using CostManagement.Domain.CostElements;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Actuals;

public readonly record struct ActualCostId(Guid Value)
{
    public static ActualCostId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// 原価実績。集約ルート。
/// 同一の (費目, 会計期間) に複数の実績を計上でき、分析時に合算される。
/// </summary>
public sealed class ActualCost
{
    public ActualCostId Id { get; }
    public ProjectId ProjectId { get; }
    public CostElementCode ElementCode { get; }
    public AccountingPeriod Period { get; }
    public decimal Quantity { get; }
    public Money UnitPrice { get; }
    public string? Note { get; }
    public DateTime RecordedAt { get; }

    public Money Amount => UnitPrice * Quantity;

    private ActualCost(ActualCostId id, ProjectId projectId, CostElementCode elementCode,
        AccountingPeriod period, decimal quantity, Money unitPrice, string? note,
        DateTime recordedAt)
    {
        Id = id;
        ProjectId = projectId;
        ElementCode = elementCode;
        Period = period;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Note = note;
        RecordedAt = recordedAt;
    }

    public static ActualCost Record(ProjectId projectId, CostElementCode elementCode,
        AccountingPeriod period, decimal quantity, Money unitPrice, string? note, DateTime now)
    {
        if (quantity < 0m)
            throw new DomainException("数量は0以上で入力してください。");
        if (unitPrice.IsNegative)
            throw new DomainException("単価は0以上で入力してください。");
        return new ActualCost(ActualCostId.New(), projectId, elementCode, period,
            quantity, unitPrice, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), now);
    }

    public static ActualCost Restore(Guid id, Guid projectId, string elementCode,
        string period, decimal quantity, decimal unitPrice, string? note, DateTime recordedAt) =>
        new(new ActualCostId(id), new ProjectId(projectId), new CostElementCode(elementCode),
            AccountingPeriod.Parse(period), quantity, new Money(unitPrice), note, recordedAt);
}

public interface IActualCostRepository
{
    Task<ActualCost?> FindByIdAsync(ActualCostId id, CancellationToken ct = default);
    Task<IReadOnlyList<ActualCost>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(ActualCost actual, CancellationToken ct = default);
    Task DeleteAsync(ActualCostId id, CancellationToken ct = default);
}

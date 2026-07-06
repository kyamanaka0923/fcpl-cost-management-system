using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Revenue;

public readonly record struct RevenuePlanId(Guid Value)
{
    public static RevenuePlanId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>売上予算の明細。集約内エンティティ。(品目, 会計期間) ごとに一意。</summary>
public sealed class RevenuePlanLine
{
    public Guid Id { get; }

    /// <summary>製品・サービスなどの品目名。</summary>
    public string ItemName { get; }

    public AccountingPeriod Period { get; }
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    public Money Amount => UnitPrice * Quantity;

    internal RevenuePlanLine(Guid id, string itemName, AccountingPeriod period,
        decimal quantity, Money unitPrice)
    {
        Id = id;
        ItemName = itemName;
        Period = period;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    internal void Update(decimal quantity, Money unitPrice)
    {
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    internal RevenuePlanLine Copy() => new(Guid.NewGuid(), ItemName, Period, Quantity, UnitPrice);
}

/// <summary>
/// 売上予算。集約ルート。
/// 原価予算(CostPlan)と同様にプロジェクトごとにバージョン管理され、
/// 四半期などの節目で改定できるが、原価予算とは独立して改定・承認する。
/// </summary>
public sealed class RevenuePlan
{
    private readonly List<RevenuePlanLine> _lines;

    public RevenuePlanId Id { get; }
    public ProjectId ProjectId { get; }

    /// <summary>プロジェクト内で単調増加するバージョン番号(1 が当初予算)。</summary>
    public int Version { get; }

    public string Label { get; }
    public PlanStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyList<RevenuePlanLine> Lines => _lines.AsReadOnly();

    public Money TotalAmount => _lines.Aggregate(Money.Zero, (sum, l) => sum + l.Amount);

    private RevenuePlan(RevenuePlanId id, ProjectId projectId, int version, string label,
        PlanStatus status, DateTime createdAt, DateTime? approvedAt, List<RevenuePlanLine> lines)
    {
        Id = id;
        ProjectId = projectId;
        Version = version;
        Label = label;
        Status = status;
        CreatedAt = createdAt;
        ApprovedAt = approvedAt;
        _lines = lines;
    }

    public static RevenuePlan CreateInitial(ProjectId projectId, string label, DateTime now)
    {
        ValidateLabel(label);
        return new RevenuePlan(RevenuePlanId.New(), projectId, 1, label.Trim(),
            PlanStatus.Draft, now, null, []);
    }

    /// <summary>既存バージョンを基に改定版ドラフトを起票する(明細を引き継ぐ)。</summary>
    public static RevenuePlan ReviseFrom(RevenuePlan basePlan, int nextVersion, string label,
        DateTime now)
    {
        ValidateLabel(label);
        if (nextVersion <= basePlan.Version)
            throw new DomainException("改定版のバージョンは基となる予算より大きい必要があります。");
        var copiedLines = basePlan._lines.Select(l => l.Copy()).ToList();
        return new RevenuePlan(RevenuePlanId.New(), basePlan.ProjectId, nextVersion, label.Trim(),
            PlanStatus.Draft, now, null, copiedLines);
    }

    /// <summary>明細を追加または更新する。(品目, 会計期間) が同じ明細は1件に統合される。</summary>
    public void UpsertLine(string itemName, AccountingPeriod period, decimal quantity,
        Money unitPrice)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("品目名は必須です。");
        if (quantity < 0m)
            throw new DomainException("数量は0以上で入力してください。");
        if (unitPrice.IsNegative)
            throw new DomainException("単価は0以上で入力してください。");

        var normalized = itemName.Trim();
        var existing = _lines.FirstOrDefault(l => l.ItemName == normalized && l.Period == period);
        if (existing is null)
            _lines.Add(new RevenuePlanLine(Guid.NewGuid(), normalized, period, quantity, unitPrice));
        else
            existing.Update(quantity, unitPrice);
    }

    public void RemoveLine(string itemName, AccountingPeriod period)
    {
        EnsureDraft();
        var normalized = itemName.Trim();
        var removed = _lines.RemoveAll(l => l.ItemName == normalized && l.Period == period);
        if (removed == 0)
            throw new DomainException("指定された明細が存在しません。");
    }

    public void Approve(DateTime now)
    {
        if (Status != PlanStatus.Draft)
            throw new DomainException("ドラフト状態の予算のみ承認できます。");
        if (_lines.Count == 0)
            throw new DomainException("明細のない予算は承認できません。");
        Status = PlanStatus.Approved;
        ApprovedAt = now;
    }

    public void Supersede()
    {
        if (Status != PlanStatus.Approved)
            throw new DomainException("承認済みの予算のみ失効にできます。");
        Status = PlanStatus.Superseded;
    }

    private void EnsureDraft()
    {
        if (Status != PlanStatus.Draft)
            throw new DomainException("承認済み・失効済みの予算は編集できません。改定版を作成してください。");
    }

    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new DomainException("予算名は必須です。");
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static RevenuePlan Restore(Guid id, Guid projectId, int version, string label,
        PlanStatus status, DateTime createdAt, DateTime? approvedAt,
        IEnumerable<(Guid Id, string ItemName, string Period, decimal Quantity, decimal UnitPrice)> lines)
    {
        var restored = lines
            .Select(l => new RevenuePlanLine(l.Id, l.ItemName, AccountingPeriod.Parse(l.Period),
                l.Quantity, new Money(l.UnitPrice)))
            .ToList();
        return new RevenuePlan(new RevenuePlanId(id), new ProjectId(projectId), version, label,
            status, createdAt, approvedAt, restored);
    }
}

public interface IRevenuePlanRepository
{
    Task<RevenuePlan?> FindByIdAsync(RevenuePlanId id, CancellationToken ct = default);
    Task<IReadOnlyList<RevenuePlan>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task<RevenuePlan?> FindLatestApprovedAsync(ProjectId projectId, CancellationToken ct = default);
    Task<int> GetMaxVersionAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(RevenuePlan plan, CancellationToken ct = default);
    Task UpdateAsync(RevenuePlan plan, CancellationToken ct = default);
}

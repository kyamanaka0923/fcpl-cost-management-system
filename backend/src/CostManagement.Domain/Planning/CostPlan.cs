using CostManagement.Domain.CostElements;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Planning;

public readonly record struct CostPlanId(Guid Value)
{
    public static CostPlanId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum PlanStatus
{
    /// <summary>策定中。明細の編集が可能。</summary>
    Draft,

    /// <summary>承認済み。予実比較の基準として利用できる。</summary>
    Approved,

    /// <summary>後続バージョンの承認により失効。履歴として参照可能。</summary>
    Superseded,
}

/// <summary>
/// 原価予算の明細。集約内エンティティ。
/// (費目, 売上対応品目, 会計期間) ごとに一意で、金額を直接持つ(数量×単価では管理しない)。
/// </summary>
public sealed class PlanLine
{
    public Guid Id { get; }
    public CostElementCode ElementCode { get; }

    /// <summary>売上対応品目。どの売上(品目)に対応する原価かを表す。null = 共通費。</summary>
    public string? RevenueItem { get; }

    public AccountingPeriod Period { get; }
    public Money Amount { get; private set; }

    internal PlanLine(Guid id, CostElementCode elementCode, string? revenueItem,
        AccountingPeriod period, Money amount)
    {
        Id = id;
        ElementCode = elementCode;
        RevenueItem = revenueItem;
        Period = period;
        Amount = amount;
    }

    internal void Update(Money amount) => Amount = amount;

    internal PlanLine Copy() => new(Guid.NewGuid(), ElementCode, RevenueItem, Period, Amount);
}

/// <summary>
/// 原価予算(予定)。集約ルート。
/// プロジェクトごとにバージョン管理され、四半期などの節目で改定版を策定できる。
/// 承認済みの予算は変更できず、改定は新しいバージョンとして起票する。
/// </summary>
public sealed class CostPlan
{
    private readonly List<PlanLine> _lines;

    public CostPlanId Id { get; }
    public ProjectId ProjectId { get; }

    /// <summary>プロジェクト内で単調増加するバージョン番号(1 が当初予算)。</summary>
    public int Version { get; }

    /// <summary>「当初予算」「第2四半期改定」などの名称。</summary>
    public string Label { get; }

    public PlanStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyList<PlanLine> Lines => _lines.AsReadOnly();

    public Money TotalAmount => _lines.Aggregate(Money.Zero, (sum, l) => sum + l.Amount);

    private CostPlan(CostPlanId id, ProjectId projectId, int version, string label,
        PlanStatus status, DateTime createdAt, DateTime? approvedAt, List<PlanLine> lines)
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

    /// <summary>当初予算(バージョン1)をドラフトとして起票する。</summary>
    public static CostPlan CreateInitial(ProjectId projectId, string label, DateTime now)
    {
        ValidateLabel(label);
        return new CostPlan(CostPlanId.New(), projectId, 1, label.Trim(),
            PlanStatus.Draft, now, null, []);
    }

    /// <summary>
    /// 既存バージョンを基に改定版ドラフトを起票する(明細を引き継ぐ)。
    /// 四半期見直しなどのタイミングで利用する。
    /// </summary>
    public static CostPlan ReviseFrom(CostPlan basePlan, int nextVersion, string label, DateTime now)
    {
        ValidateLabel(label);
        if (nextVersion <= basePlan.Version)
            throw new DomainException("改定版のバージョンは基となる予算より大きい必要があります。");
        var copiedLines = basePlan._lines.Select(l => l.Copy()).ToList();
        return new CostPlan(CostPlanId.New(), basePlan.ProjectId, nextVersion, label.Trim(),
            PlanStatus.Draft, now, null, copiedLines);
    }

    /// <summary>
    /// 明細を追加または更新する。(費目, 売上対応品目, 会計期間) が同じ明細は1件に統合される。
    /// </summary>
    public void UpsertLine(CostElementCode elementCode, string? revenueItem,
        AccountingPeriod period, Money amount)
    {
        EnsureDraft();
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");

        var normalized = NormalizeRevenueItem(revenueItem);
        var existing = _lines.FirstOrDefault(l =>
            l.ElementCode == elementCode && l.RevenueItem == normalized && l.Period == period);
        if (existing is null)
            _lines.Add(new PlanLine(Guid.NewGuid(), elementCode, normalized, period, amount));
        else
            existing.Update(amount);
    }

    public void RemoveLine(CostElementCode elementCode, string? revenueItem, AccountingPeriod period)
    {
        EnsureDraft();
        var normalized = NormalizeRevenueItem(revenueItem);
        var removed = _lines.RemoveAll(l =>
            l.ElementCode == elementCode && l.RevenueItem == normalized && l.Period == period);
        if (removed == 0)
            throw new DomainException("指定された明細が存在しません。");
    }

    /// <summary>予算を承認する。承認後は編集不可となる。</summary>
    public void Approve(DateTime now)
    {
        if (Status != PlanStatus.Draft)
            throw new DomainException("ドラフト状態の予算のみ承認できます。");
        if (_lines.Count == 0)
            throw new DomainException("明細のない予算は承認できません。");
        Status = PlanStatus.Approved;
        ApprovedAt = now;
    }

    /// <summary>後続バージョンの承認に伴い、この予算を失効させる。</summary>
    public void Supersede()
    {
        if (Status != PlanStatus.Approved)
            throw new DomainException("承認済みの予算のみ失効にできます。");
        Status = PlanStatus.Superseded;
    }

    internal static string? NormalizeRevenueItem(string? revenueItem) =>
        string.IsNullOrWhiteSpace(revenueItem) ? null : revenueItem.Trim();

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
    public static CostPlan Restore(Guid id, Guid projectId, int version, string label,
        PlanStatus status, DateTime createdAt, DateTime? approvedAt,
        IEnumerable<(Guid Id, string ElementCode, string? RevenueItem, string Period, decimal Amount)> lines)
    {
        var restored = lines
            .Select(l => new PlanLine(l.Id, new CostElementCode(l.ElementCode),
                NormalizeRevenueItem(l.RevenueItem), AccountingPeriod.Parse(l.Period),
                new Money(l.Amount)))
            .ToList();
        return new CostPlan(new CostPlanId(id), new ProjectId(projectId), version, label,
            status, createdAt, approvedAt, restored);
    }
}

public interface ICostPlanRepository
{
    Task<CostPlan?> FindByIdAsync(CostPlanId id, CancellationToken ct = default);
    Task<IReadOnlyList<CostPlan>> ListByProjectAsync(ProjectId projectId, CancellationToken ct = default);
    Task<CostPlan?> FindLatestApprovedAsync(ProjectId projectId, CancellationToken ct = default);
    Task<int> GetMaxVersionAsync(ProjectId projectId, CancellationToken ct = default);
    Task AddAsync(CostPlan plan, CancellationToken ct = default);
    Task UpdateAsync(CostPlan plan, CancellationToken ct = default);
}

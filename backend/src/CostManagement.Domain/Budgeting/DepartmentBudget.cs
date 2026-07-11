using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Budgeting;

public readonly record struct DepartmentBudgetId(Guid Value)
{
    public static DepartmentBudgetId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum BudgetStatus
{
    /// <summary>策定中。明細の編集が可能。</summary>
    Draft,

    /// <summary>承認済み。予実比較の基準として利用できる。</summary>
    Approved,

    /// <summary>後続バージョンの承認により失効。履歴として参照可能。</summary>
    Superseded,
}

/// <summary>課予算の区分。</summary>
public enum BudgetCategory
{
    /// <summary>売上高(案件別)</summary>
    Revenue,

    /// <summary>加工費(案件別)</summary>
    Processing,

    /// <summary>外注費(案件別)</summary>
    Outsourcing,

    /// <summary>期間費用(費目別、課共通)</summary>
    PeriodCost,
}

/// <summary>区分と明細キー(案件/費目)の整合検証。予算明細と実績の両方から使う。</summary>
public static class BudgetCategories
{
    /// <summary>案件別に明細を持つ区分か(売上高・加工費・外注費)。</summary>
    public static bool IsProjectBased(this BudgetCategory category) =>
        category is BudgetCategory.Revenue or BudgetCategory.Processing or BudgetCategory.Outsourcing;

    /// <summary>区分に応じて案件・費目の排他を検証する。</summary>
    public static void ValidateKey(BudgetCategory category, ProjectId? projectId, CostElementCode? elementCode)
    {
        if (category.IsProjectBased())
        {
            if (projectId is null)
                throw new DomainException("売上高・加工費・外注費の明細には案件を指定してください。");
            if (elementCode is not null)
                throw new DomainException("売上高・加工費・外注費の明細に費目は指定できません。");
        }
        else if (category == BudgetCategory.PeriodCost)
        {
            if (elementCode is null)
                throw new DomainException("期間費用の明細には費目を指定してください。");
            if (projectId is not null)
                throw new DomainException("期間費用の明細に案件は指定できません。");
        }
        else
        {
            throw new DomainException($"予算区分が不正です: {category}");
        }
    }
}

/// <summary>
/// 課予算の明細。集約内エンティティ。
/// 売上高・加工費・外注費は (区分, 案件)、期間費用は (期間費用, 費目) ごとに一意で、
/// 半期一括の金額を直接持つ(数量×単価では管理しない)。
/// </summary>
public sealed class BudgetLine
{
    public Guid Id { get; }
    public BudgetCategory Category { get; }

    /// <summary>案件。売上高・加工費・外注費の明細で必須。期間費用では null。</summary>
    public ProjectId? ProjectId { get; }

    /// <summary>費目。期間費用の明細で必須。案件別区分では null。</summary>
    public CostElementCode? ElementCode { get; }

    public Money Amount { get; private set; }

    internal BudgetLine(Guid id, BudgetCategory category, ProjectId? projectId,
        CostElementCode? elementCode, Money amount)
    {
        BudgetCategories.ValidateKey(category, projectId, elementCode);
        Id = id;
        Category = category;
        ProjectId = projectId;
        ElementCode = elementCode;
        Amount = amount;
    }

    internal void Update(Money amount) => Amount = amount;

    internal BudgetLine Copy() => new(Guid.NewGuid(), Category, ProjectId, ElementCode, Amount);
}

/// <summary>
/// 課の半期予算。集約ルート。
/// (課, 年度, 半期) ごとにバージョン管理され、改定は新しいバージョンとして起票する。
/// 承認済みの予算は変更できない。
/// 売上高・加工費・外注費・期間費用の区分合計はヘッダに持たず、常に明細の合計として導出する
/// (課レベルの直接入力は構造的に不可)。
/// </summary>
public sealed class DepartmentBudget
{
    private readonly List<BudgetLine> _lines;

    public DepartmentBudgetId Id { get; }
    public DepartmentId DepartmentId { get; }
    public FiscalHalf FiscalHalf { get; }

    /// <summary>同一 (課, 半期) 内で単調増加するバージョン番号(1 が当初予算)。</summary>
    public int Version { get; }

    /// <summary>「当初予算」「下期見直し」などの名称。</summary>
    public string Label { get; }

    public BudgetStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ApprovedAt { get; private set; }

    public IReadOnlyList<BudgetLine> Lines => _lines.AsReadOnly();

    /// <summary>区分の合計(= 明細の合計)。</summary>
    public Money CategoryTotal(BudgetCategory category) =>
        _lines.Where(l => l.Category == category)
            .Aggregate(Money.Zero, (sum, l) => sum + l.Amount);

    /// <summary>総コスト(加工費 + 外注費 + 期間費用)。</summary>
    public Money TotalCost =>
        CategoryTotal(BudgetCategory.Processing)
        + CategoryTotal(BudgetCategory.Outsourcing)
        + CategoryTotal(BudgetCategory.PeriodCost);

    /// <summary>計画損益(売上高 − 総コスト)。</summary>
    public Money PlannedProfit => CategoryTotal(BudgetCategory.Revenue) - TotalCost;

    private DepartmentBudget(DepartmentBudgetId id, DepartmentId departmentId, FiscalHalf fiscalHalf,
        int version, string label, BudgetStatus status, DateTime createdAt, DateTime? approvedAt,
        List<BudgetLine> lines)
    {
        Id = id;
        DepartmentId = departmentId;
        FiscalHalf = fiscalHalf;
        Version = version;
        Label = label;
        Status = status;
        CreatedAt = createdAt;
        ApprovedAt = approvedAt;
        _lines = lines;
    }

    /// <summary>当初予算(バージョン1)をドラフトとして起票する。</summary>
    public static DepartmentBudget CreateInitial(DepartmentId departmentId, FiscalHalf fiscalHalf,
        string label, DateTime now)
    {
        ValidateLabel(label);
        return new DepartmentBudget(DepartmentBudgetId.New(), departmentId, fiscalHalf, 1,
            label.Trim(), BudgetStatus.Draft, now, null, []);
    }

    /// <summary>既存バージョンを基に改定版ドラフトを起票する(明細を引き継ぐ)。</summary>
    public static DepartmentBudget ReviseFrom(DepartmentBudget baseBudget, int nextVersion,
        string label, DateTime now)
    {
        ValidateLabel(label);
        if (nextVersion <= baseBudget.Version)
            throw new DomainException("改定版のバージョンは基となる予算より大きい必要があります。");
        var copiedLines = baseBudget._lines.Select(l => l.Copy()).ToList();
        return new DepartmentBudget(DepartmentBudgetId.New(), baseBudget.DepartmentId,
            baseBudget.FiscalHalf, nextVersion, label.Trim(), BudgetStatus.Draft, now, null, copiedLines);
    }

    /// <summary>
    /// 案件別明細(売上高・加工費・外注費)を追加または更新する。(区分, 案件) が同じ明細は1件に統合される。
    /// </summary>
    public void UpsertProjectLine(BudgetCategory category, ProjectId projectId, Money amount)
    {
        EnsureDraft();
        ValidateAmount(amount);
        if (!category.IsProjectBased())
            throw new DomainException("期間費用の明細は費目で指定してください。");

        var existing = _lines.FirstOrDefault(l => l.Category == category && l.ProjectId == projectId);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), category, projectId, null, amount));
        else
            existing.Update(amount);
    }

    /// <summary>期間費用の明細を追加または更新する。同一費目の明細は1件に統合される。</summary>
    public void UpsertPeriodCostLine(CostElementCode elementCode, Money amount)
    {
        EnsureDraft();
        ValidateAmount(amount);

        var existing = _lines.FirstOrDefault(l =>
            l.Category == BudgetCategory.PeriodCost && l.ElementCode == elementCode);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), BudgetCategory.PeriodCost, null, elementCode, amount));
        else
            existing.Update(amount);
    }

    public void RemoveProjectLine(BudgetCategory category, ProjectId projectId)
    {
        EnsureDraft();
        if (!category.IsProjectBased())
            throw new DomainException("期間費用の明細は費目で指定してください。");
        var removed = _lines.RemoveAll(l => l.Category == category && l.ProjectId == projectId);
        if (removed == 0)
            throw new DomainException("指定された明細が存在しません。");
    }

    public void RemovePeriodCostLine(CostElementCode elementCode)
    {
        EnsureDraft();
        var removed = _lines.RemoveAll(l =>
            l.Category == BudgetCategory.PeriodCost && l.ElementCode == elementCode);
        if (removed == 0)
            throw new DomainException("指定された明細が存在しません。");
    }

    /// <summary>予算を承認する。承認後は編集不可となる。</summary>
    public void Approve(DateTime now)
    {
        if (Status != BudgetStatus.Draft)
            throw new DomainException("ドラフト状態の予算のみ承認できます。");
        if (_lines.Count == 0)
            throw new DomainException("明細のない予算は承認できません。");
        Status = BudgetStatus.Approved;
        ApprovedAt = now;
    }

    /// <summary>後続バージョンの承認に伴い、この予算を失効させる。</summary>
    public void Supersede()
    {
        if (Status != BudgetStatus.Approved)
            throw new DomainException("承認済みの予算のみ失効にできます。");
        Status = BudgetStatus.Superseded;
    }

    private void EnsureDraft()
    {
        if (Status != BudgetStatus.Draft)
            throw new DomainException("承認済み・失効済みの予算は編集できません。改定版を作成してください。");
    }

    private static void ValidateAmount(Money amount)
    {
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
    }

    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new DomainException("予算名は必須です。");
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static DepartmentBudget Restore(Guid id, Guid departmentId, string fiscalHalf,
        int version, string label, BudgetStatus status, DateTime createdAt, DateTime? approvedAt,
        IEnumerable<(Guid Id, string Category, Guid? ProjectId, string? ElementCode, decimal Amount)> lines)
    {
        var restored = lines
            .Select(l => new BudgetLine(l.Id, Enum.Parse<BudgetCategory>(l.Category),
                l.ProjectId is { } pid ? new ProjectId(pid) : null,
                l.ElementCode is { } code ? new CostElementCode(code) : null,
                new Money(l.Amount)))
            .ToList();
        return new DepartmentBudget(new DepartmentBudgetId(id), new DepartmentId(departmentId),
            FiscalHalf.Parse(fiscalHalf), version, label, status, createdAt, approvedAt, restored);
    }
}

public interface IDepartmentBudgetRepository
{
    Task<DepartmentBudget?> FindByIdAsync(DepartmentBudgetId id, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentBudget>> ListAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);
    Task<DepartmentBudget?> FindLatestApprovedAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);
    Task<int> GetMaxVersionAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);
    Task AddAsync(DepartmentBudget budget, CancellationToken ct = default);
    Task UpdateAsync(DepartmentBudget budget, CancellationToken ct = default);
}

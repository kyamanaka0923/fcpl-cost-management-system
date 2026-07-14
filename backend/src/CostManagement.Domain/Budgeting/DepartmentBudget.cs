using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Budgeting;

/// <summary>課予算を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct DepartmentBudgetId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static DepartmentBudgetId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>課予算のライフサイクル状態。</summary>
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

/// <summary>半期を構成する月数(H1=4〜9月 / H2=10〜3月 の6ヶ月。月インデックスは 1..6)。</summary>
public static class HalfMonths
{
    /// <summary>1半期の月数。</summary>
    public const int Count = 6;

    /// <summary>月インデックスが 1..6 の範囲かを検証する。</summary>
    public static void Validate(int month)
    {
        if (month is < 1 or > Count)
            throw new DomainException($"月は1〜{Count}で指定してください: {month}");
    }
}

/// <summary>
/// 課予算の明細。集約内エンティティ。
/// 売上高・加工費・外注費は (区分, 案件)、期間費用は (期間費用, 費目) ごとに一意。
/// 金額は「半期一括」または「月次(月インデックス1..6ごと)」で持つ(数量×単価では管理しない)。
/// どちらのモードでも <see cref="Amount"/> は半期合計。月次のときは半期合計 = 月次の合計。
/// </summary>
public sealed class BudgetLine
{
    // 月次モードの月別金額(1..6 → 金額)。0の月は保持しない。空 = 半期一括モード。
    private readonly Dictionary<int, Money> _monthly;

    /// <summary>明細ID(集約内で一意)。</summary>
    public Guid Id { get; }

    /// <summary>予算区分(売上高/加工費/外注費/期間費用)。</summary>
    public BudgetCategory Category { get; }

    /// <summary>案件。売上高・加工費・外注費の明細で必須。期間費用では null。</summary>
    public ProjectId? ProjectId { get; }

    /// <summary>費目。期間費用の明細で必須。案件別区分では null。</summary>
    public CostElementCode? ElementCode { get; }

    /// <summary>半期合計の金額(月次モードでは月別金額の合計)。</summary>
    public Money Amount { get; private set; }

    /// <summary>月次モードの月別金額(1..6 → 金額)。半期一括モードでは空。</summary>
    public IReadOnlyDictionary<int, Money> MonthlyAmounts => _monthly;

    /// <summary>月次モードかどうか(月別金額を持つ = 月次)。</summary>
    public bool IsMonthly => _monthly.Count > 0;

    /// <summary>
    /// 明細を生成する(集約内部からのみ)。区分と案件/費目の排他を
    /// <see cref="BudgetCategories.ValidateKey"/> で検証する(復元経路でも通る)。
    /// monthly を渡すと月次モード、null/空だと amount による半期一括モードになる。
    /// </summary>
    internal BudgetLine(Guid id, BudgetCategory category, ProjectId? projectId,
        CostElementCode? elementCode, Money amount,
        IReadOnlyDictionary<int, Money>? monthly = null)
    {
        BudgetCategories.ValidateKey(category, projectId, elementCode);
        Id = id;
        Category = category;
        ProjectId = projectId;
        ElementCode = elementCode;
        _monthly = new Dictionary<int, Money>();
        if (monthly is { Count: > 0 })
            SetMonthly(monthly);
        else
            Amount = amount;
    }

    /// <summary>半期一括の金額で上書きする(月次モードを解除する)。</summary>
    internal void UpdateHalf(Money amount)
    {
        _monthly.Clear();
        Amount = amount;
    }

    /// <summary>月別金額で上書きする(月次モードにする)。半期合計は月次の合計になる。</summary>
    internal void UpdateMonthly(IReadOnlyDictionary<int, Money> monthly) => SetMonthly(monthly);

    private void SetMonthly(IReadOnlyDictionary<int, Money> monthly)
    {
        _monthly.Clear();
        var total = Money.Zero;
        foreach (var (month, amount) in monthly)
        {
            HalfMonths.Validate(month);
            if (amount.IsNegative)
                throw new DomainException("金額は0以上で入力してください。");
            if (amount.Value != 0m)
            {
                _monthly[month] = amount;
                total += amount;
            }
        }
        Amount = total;
    }

    /// <summary>改定版へ引き継ぐため、新しいIDで明細を複製する(月次モードも引き継ぐ)。</summary>
    internal BudgetLine Copy() =>
        new(Guid.NewGuid(), Category, ProjectId, ElementCode, Amount,
            _monthly.Count > 0 ? _monthly : null);
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

    /// <summary>課予算ID。</summary>
    public DepartmentBudgetId Id { get; }

    /// <summary>予算を策定する課のID。</summary>
    public DepartmentId DepartmentId { get; }

    /// <summary>対象半期。</summary>
    public FiscalHalf FiscalHalf { get; }

    /// <summary>同一 (課, 半期) 内で単調増加するバージョン番号(1 が当初予算)。</summary>
    public int Version { get; }

    /// <summary>「当初予算」「下期見直し」などの名称。</summary>
    public string Label { get; }

    /// <summary>ライフサイクル状態(策定中/承認済/失効)。</summary>
    public BudgetStatus Status { get; private set; }

    /// <summary>作成日時(UTC)。</summary>
    public DateTime CreatedAt { get; }

    /// <summary>承認日時(UTC)。未承認は null。</summary>
    public DateTime? ApprovedAt { get; private set; }

    /// <summary>明細の読み取り専用ビュー。</summary>
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
    /// 案件別明細(売上高・加工費・外注費)を半期一括金額で追加または更新する。
    /// (区分, 案件) が同じ明細は1件に統合され、月次モードだった場合は半期一括に切り替わる。
    /// </summary>
    public void UpsertProjectLine(BudgetCategory category, ProjectId projectId, Money amount)
    {
        EnsureDraft();
        ValidateAmount(amount);
        RequireProjectCategory(category);

        var existing = _lines.FirstOrDefault(l => l.Category == category && l.ProjectId == projectId);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), category, projectId, null, amount));
        else
            existing.UpdateHalf(amount);
    }

    /// <summary>
    /// 案件別明細を月別金額(1..6 → 金額)で追加または更新する(月次モード)。
    /// 半期合計は月次の合計になる。(区分, 案件) が同じ明細は1件に統合される。
    /// </summary>
    public void UpsertProjectLineMonthly(BudgetCategory category, ProjectId projectId,
        IReadOnlyDictionary<int, Money> monthly)
    {
        EnsureDraft();
        RequireProjectCategory(category);

        var existing = _lines.FirstOrDefault(l => l.Category == category && l.ProjectId == projectId);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), category, projectId, null, Money.Zero, monthly));
        else
            existing.UpdateMonthly(monthly);
    }

    /// <summary>期間費用の明細を半期一括金額で追加または更新する。同一費目の明細は1件に統合される。</summary>
    public void UpsertPeriodCostLine(CostElementCode elementCode, Money amount)
    {
        EnsureDraft();
        ValidateAmount(amount);

        var existing = _lines.FirstOrDefault(l =>
            l.Category == BudgetCategory.PeriodCost && l.ElementCode == elementCode);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), BudgetCategory.PeriodCost, null, elementCode, amount));
        else
            existing.UpdateHalf(amount);
    }

    /// <summary>期間費用の明細を月別金額で追加または更新する(月次モード)。同一費目の明細は1件に統合される。</summary>
    public void UpsertPeriodCostLineMonthly(CostElementCode elementCode,
        IReadOnlyDictionary<int, Money> monthly)
    {
        EnsureDraft();

        var existing = _lines.FirstOrDefault(l =>
            l.Category == BudgetCategory.PeriodCost && l.ElementCode == elementCode);
        if (existing is null)
            _lines.Add(new BudgetLine(Guid.NewGuid(), BudgetCategory.PeriodCost, null, elementCode,
                Money.Zero, monthly));
        else
            existing.UpdateMonthly(monthly);
    }

    private static void RequireProjectCategory(BudgetCategory category)
    {
        if (!category.IsProjectBased())
            throw new DomainException("期間費用の明細は費目で指定してください。");
    }

    /// <summary>案件別明細(売上高・加工費・外注費)を削除する。存在しなければ例外。</summary>
    public void RemoveProjectLine(BudgetCategory category, ProjectId projectId)
    {
        EnsureDraft();
        if (!category.IsProjectBased())
            throw new DomainException("期間費用の明細は費目で指定してください。");
        var removed = _lines.RemoveAll(l => l.Category == category && l.ProjectId == projectId);
        if (removed == 0)
            throw new DomainException("指定された明細が存在しません。");
    }

    /// <summary>期間費用の明細を削除する。存在しなければ例外。</summary>
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

    /// <summary>ドラフト状態でなければ編集を拒否する(承認済み予算の不変条件)。</summary>
    private void EnsureDraft()
    {
        if (Status != BudgetStatus.Draft)
            throw new DomainException("承認済み・失効済みの予算は編集できません。改定版を作成してください。");
    }

    /// <summary>金額が0以上であることを検証する。</summary>
    private static void ValidateAmount(Money amount)
    {
        if (amount.IsNegative)
            throw new DomainException("金額は0以上で入力してください。");
    }

    /// <summary>予算名が空でないことを検証する。</summary>
    private static void ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new DomainException("予算名は必須です。");
    }

    /// <summary>
    /// 永続化層からの復元用ファクトリ。各明細の <c>Monthly</c> は月別金額(1..6 → 金額)で、
    /// 空なら半期一括モード、非空なら月次モードとして復元する。
    /// </summary>
    public static DepartmentBudget Restore(Guid id, Guid departmentId, string fiscalHalf,
        int version, string label, BudgetStatus status, DateTime createdAt, DateTime? approvedAt,
        IEnumerable<(Guid Id, string Category, Guid? ProjectId, string? ElementCode, decimal Amount,
            IReadOnlyDictionary<int, decimal> Monthly)> lines)
    {
        var restored = lines
            .Select(l => new BudgetLine(l.Id, Enum.Parse<BudgetCategory>(l.Category),
                l.ProjectId is { } pid ? new ProjectId(pid) : null,
                l.ElementCode is { } code ? new CostElementCode(code) : null,
                new Money(l.Amount),
                l.Monthly.Count > 0
                    ? l.Monthly.ToDictionary(m => m.Key, m => new Money(m.Value))
                    : null))
            .ToList();
        return new DepartmentBudget(new DepartmentBudgetId(id), new DepartmentId(departmentId),
            FiscalHalf.Parse(fiscalHalf), version, label, status, createdAt, approvedAt, restored);
    }
}

/// <summary>課予算の永続化ポート(実装はインフラ層)。</summary>
public interface IDepartmentBudgetRepository
{
    /// <summary>IDで予算を1件取得する(明細を含む)。無ければ null。</summary>
    Task<DepartmentBudget?> FindByIdAsync(DepartmentBudgetId id, CancellationToken ct = default);

    /// <summary>(課, 半期)の全バージョンを取得する。</summary>
    Task<IReadOnlyList<DepartmentBudget>> ListAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);

    /// <summary>(課, 半期)の最新の承認済みバージョンを取得する。無ければ null。</summary>
    Task<DepartmentBudget?> FindLatestApprovedAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);

    /// <summary>(課, 半期)の最大バージョン番号を取得する(改定版の採番に使う。無ければ0)。</summary>
    Task<int> GetMaxVersionAsync(DepartmentId departmentId, FiscalHalf fiscalHalf,
        CancellationToken ct = default);

    /// <summary>予算を新規追加する(ヘッダ + 明細)。</summary>
    Task AddAsync(DepartmentBudget budget, CancellationToken ct = default);

    /// <summary>予算を更新する(ヘッダ更新 + 明細の洗い替え)。</summary>
    Task UpdateAsync(DepartmentBudget budget, CancellationToken ct = default);
}

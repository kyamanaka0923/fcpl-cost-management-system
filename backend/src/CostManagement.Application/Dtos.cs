namespace CostManagement.Application;

// ---- 応答 DTO ----

/// <summary>部の応答 DTO。</summary>
public sealed record DivisionDto(
    Guid Id,
    string Code,
    string Name,
    DateTime CreatedAt);

/// <summary>課の応答 DTO(所属する部のIDを含む)。</summary>
public sealed record DepartmentDto(
    Guid Id,
    Guid DivisionId,
    string Code,
    string Name,
    DateTime CreatedAt);

/// <summary>案件の応答 DTO(所属する課のIDを含む)。</summary>
public sealed record ProjectDto(
    Guid Id,
    Guid DepartmentId,
    string Code,
    string Name,
    DateTime CreatedAt);

/// <summary>費目マスタの応答 DTO。</summary>
public sealed record CostElementDto(
    string Code,
    string Name);

/// <summary>
/// 予算明細の応答 DTO(案件別は ProjectId、期間費用は ElementCode を持つ)。
/// IsMonthly=true のとき MonthlyAmounts に月別金額(1..6 → 金額)を持ち、Amount はその合計。
/// </summary>
public sealed record BudgetLineDto(
    Guid Id,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    string? PeriodDetail,
    decimal Amount,
    bool IsMonthly,
    IReadOnlyDictionary<int, decimal> MonthlyAmounts);

/// <summary>予算バージョンの一覧用サマリ DTO(区分合計・計画損益つき)。</summary>
public sealed record BudgetSummaryDto(
    Guid Id,
    Guid DepartmentId,
    string FiscalHalf,
    int Version,
    string Label,
    string Status,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    decimal RevenueTotal,
    decimal ProcessingTotal,
    decimal OutsourcingTotal,
    decimal PeriodCostTotal,
    decimal PlannedProfit);

/// <summary>予算バージョンの詳細 DTO(サマリ + 明細一覧)。</summary>
public sealed record BudgetDetailDto(
    Guid Id,
    Guid DepartmentId,
    string FiscalHalf,
    int Version,
    string Label,
    string Status,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    decimal RevenueTotal,
    decimal ProcessingTotal,
    decimal OutsourcingTotal,
    decimal PeriodCostTotal,
    decimal PlannedProfit,
    IReadOnlyList<BudgetLineDto> Lines);

/// <summary>実績の応答 DTO。Month は計上対象の月(1..6)。半期一括の計上は null。</summary>
public sealed record ActualEntryDto(
    Guid Id,
    Guid DepartmentId,
    string FiscalHalf,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    string? PeriodDetail,
    int? Month,
    decimal Amount,
    string? Note,
    DateTime RecordedAt);

/// <summary>予実差異の明細 DTO(案件/費目の表示名・有利/不利フラグつき)。</summary>
public sealed record VarianceLineDto(
    string Category,
    Guid? ProjectId,
    string? ProjectName,
    string? ElementCode,
    string? ElementName,
    string? PeriodDetail,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance,
    bool IsUnplanned,
    bool IsFavorable,
    bool IsAdverse);

/// <summary>区分ごとの予実差異 DTO(明細 + 区分サブトータル)。</summary>
public sealed record CategoryVarianceDto(
    string Category,
    IReadOnlyList<VarianceLineDto> Lines,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

/// <summary>課予算の予実差異分析レポート DTO。</summary>
public sealed record VarianceReportDto(
    Guid BudgetId,
    int BudgetVersion,
    string BudgetLabel,
    IReadOnlyList<CategoryVarianceDto> Categories,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal RevenueVariance,
    decimal PlannedCost,
    decimal ActualCost,
    decimal CostVariance);

/// <summary>予算バージョン間比較の明細 DTO(基準版 vs 対象版の差分)。</summary>
public sealed record BudgetComparisonLineDto(
    string Category,
    Guid? ProjectId,
    string? ProjectName,
    string? ElementCode,
    string? ElementName,
    string? PeriodDetail,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>区分ごとのバージョン間比較 DTO(明細 + 区分サブトータル)。</summary>
public sealed record CategoryComparisonDto(
    string Category,
    IReadOnlyList<BudgetComparisonLineDto> Lines,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

/// <summary>予算バージョン間比較レポート DTO。</summary>
public sealed record BudgetComparisonDto(
    int BaseVersion,
    string BaseLabel,
    int TargetVersion,
    string TargetLabel,
    IReadOnlyList<CategoryComparisonDto> Categories);

/// <summary>案件別損益の DTO(売上・加工費・外注費と損益の予実)。</summary>
public sealed record ProjectProfitLineDto(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedProcessing,
    decimal ActualProcessing,
    decimal PlannedOutsourcing,
    decimal ActualOutsourcing,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance);

/// <summary>課の損益サマリ DTO(全体損益・粗利率 + 案件別損益)。</summary>
public sealed record ProfitSummaryDto(
    int BudgetVersion,
    string BudgetLabel,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedTotalCost,
    decimal ActualTotalCost,
    decimal PlannedPeriodCost,
    decimal ActualPeriodCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    decimal? PlannedMarginRate,
    decimal? ActualMarginRate,
    IReadOnlyList<ProjectProfitLineDto> ProjectLines);

/// <summary>区分ごとの部合計 DTO(予算・実績・差異)。</summary>
public sealed record CategorySummaryDto(
    string Category,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

/// <summary>部サマリの課別内訳 DTO(未策定の課は HasApprovedBudget=false)。</summary>
public sealed record DepartmentSummaryLineDto(
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    bool HasApprovedBudget,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal PlannedCost,
    decimal ActualCost,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    IReadOnlyList<CategorySummaryDto> Categories);

/// <summary>部の予実サマリ DTO(区分別合計・全体損益・課別内訳 + 部承認の状態)。</summary>
public sealed record DivisionBudgetSummaryDto(
    IReadOnlyList<CategorySummaryDto> Categories,
    decimal PlannedRevenue,
    decimal ActualRevenue,
    decimal RevenueVariance,
    decimal PlannedCost,
    decimal ActualCost,
    decimal CostVariance,
    decimal PlannedProfit,
    decimal ActualProfit,
    decimal ProfitVariance,
    IReadOnlyList<DepartmentSummaryLineDto> DepartmentLines,
    bool IsApproved,
    DateTime? ApprovedAt,
    bool CanApprove);

// ---- リクエスト ----

/// <summary>部の新規登録リクエスト。</summary>
public sealed record CreateDivisionRequest(string Code, string Name);

/// <summary>課の新規登録リクエスト。</summary>
public sealed record CreateDepartmentRequest(string Code, string Name);

/// <summary>案件の新規登録リクエスト。</summary>
public sealed record CreateProjectRequest(string Code, string Name);

/// <summary>案件コード・名称の更新リクエスト。</summary>
public sealed record UpdateProjectRequest(string Code, string Name);

/// <summary>費目の新規登録リクエスト。</summary>
public sealed record CreateCostElementRequest(string Code, string Name);

/// <summary>予算ドラフト起票リクエスト(BaseBudgetId 指定時はその版を引き継ぐ改定版)。</summary>
public sealed record CreateBudgetRequest(string FiscalHalf, string Label, Guid? BaseBudgetId = null);

/// <summary>
/// 予算明細の追加・更新リクエスト(案件別は ProjectId、期間費用は ElementCode)。
/// MonthlyAmounts を指定すると月次モード(月別金額 1..6 → 金額。半期合計は自動算出)、
/// 未指定なら Amount による半期一括モード。
/// PeriodDetail は期間費用の明細名(費目内を細分。未指定/空は費目一括)。
/// </summary>
public sealed record UpsertBudgetLineRequest(
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount,
    IReadOnlyDictionary<int, decimal>? MonthlyAmounts = null,
    string? PeriodDetail = null);

/// <summary>
/// 実績計上リクエスト(案件別は ProjectId、期間費用は ElementCode)。
/// Month を指定すると特定月の計上(1..6)、未指定なら半期一括の計上。
/// PeriodDetail は期間費用の明細名(計画明細に対する計上。未指定/空は費目一括)。
/// </summary>
public sealed record RecordActualRequest(
    string FiscalHalf,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount,
    int? Month = null,
    string? Note = null,
    string? PeriodDetail = null);

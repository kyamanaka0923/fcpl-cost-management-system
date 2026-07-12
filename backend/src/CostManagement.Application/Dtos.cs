namespace CostManagement.Application;

// ---- 応答 DTO ----

public sealed record DivisionDto(
    Guid Id,
    string Code,
    string Name,
    DateTime CreatedAt);

public sealed record DepartmentDto(
    Guid Id,
    Guid DivisionId,
    string Code,
    string Name,
    DateTime CreatedAt);

public sealed record ProjectDto(
    Guid Id,
    Guid DepartmentId,
    string Code,
    string Name,
    string Status,
    DateTime CreatedAt);

public sealed record CostElementDto(
    string Code,
    string Name);

public sealed record BudgetLineDto(
    Guid Id,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount);

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

public sealed record ActualEntryDto(
    Guid Id,
    Guid DepartmentId,
    string FiscalHalf,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount,
    string? Note,
    DateTime RecordedAt);

public sealed record VarianceLineDto(
    string Category,
    Guid? ProjectId,
    string? ProjectName,
    string? ElementCode,
    string? ElementName,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance,
    bool IsUnplanned,
    bool IsFavorable,
    bool IsAdverse);

public sealed record CategoryVarianceDto(
    string Category,
    IReadOnlyList<VarianceLineDto> Lines,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

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

public sealed record BudgetComparisonLineDto(
    string Category,
    Guid? ProjectId,
    string? ProjectName,
    string? ElementCode,
    string? ElementName,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

public sealed record CategoryComparisonDto(
    string Category,
    IReadOnlyList<BudgetComparisonLineDto> Lines,
    decimal BaseAmount,
    decimal TargetAmount,
    decimal Difference);

public sealed record BudgetComparisonDto(
    int BaseVersion,
    string BaseLabel,
    int TargetVersion,
    string TargetLabel,
    IReadOnlyList<CategoryComparisonDto> Categories);

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

public sealed record CategorySummaryDto(
    string Category,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal Variance);

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
    decimal ProfitVariance);

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

public sealed record CreateDivisionRequest(string Code, string Name);

public sealed record CreateDepartmentRequest(string Code, string Name);

public sealed record CreateProjectRequest(string Code, string Name);

public sealed record CreateCostElementRequest(string Code, string Name);

public sealed record CreateBudgetRequest(string FiscalHalf, string Label, Guid? BaseBudgetId = null);

public sealed record UpsertBudgetLineRequest(
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount);

public sealed record RecordActualRequest(
    string FiscalHalf,
    string Category,
    Guid? ProjectId,
    string? ElementCode,
    decimal Amount,
    string? Note = null);

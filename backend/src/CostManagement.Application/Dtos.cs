namespace CostManagement.Application;

public sealed record ProjectDto(
    Guid Id, string Code, string Name, int FiscalYear, string Status, DateTime CreatedAt);

public sealed record CostElementDto(
    string Code, string Name, string Type, bool IsQuantityManaged);

public sealed record PlanLineDto(
    Guid Id, string ElementCode, string Period, decimal Quantity, decimal UnitPrice, decimal Amount);

public sealed record CostPlanSummaryDto(
    Guid Id, Guid ProjectId, int Version, string Label, string Status,
    DateTime CreatedAt, DateTime? ApprovedAt, decimal TotalAmount);

public sealed record CostPlanDetailDto(
    Guid Id, Guid ProjectId, int Version, string Label, string Status,
    DateTime CreatedAt, DateTime? ApprovedAt, decimal TotalAmount,
    IReadOnlyList<PlanLineDto> Lines);

public sealed record ActualCostDto(
    Guid Id, Guid ProjectId, string ElementCode, string Period,
    decimal Quantity, decimal UnitPrice, decimal Amount, string? Note, DateTime RecordedAt);

public sealed record VarianceLineDto(
    string ElementCode, string Period,
    decimal PlannedQuantity, decimal PlannedUnitPrice, decimal PlannedAmount,
    decimal ActualQuantity, decimal ActualUnitPrice, decimal ActualAmount,
    decimal TotalVariance, decimal? PriceVariance, decimal? QuantityVariance,
    bool IsUnplanned, bool IsAdverse);

public sealed record VarianceReportDto(
    Guid PlanId, int PlanVersion, string PlanLabel,
    IReadOnlyList<VarianceLineDto> Lines,
    decimal TotalPlannedAmount, decimal TotalActualAmount, decimal TotalVariance);

public sealed record PlanComparisonLineDto(
    string ElementCode, string Period, decimal BaseAmount, decimal TargetAmount, decimal Difference);

public sealed record PlanComparisonDto(
    int BaseVersion, string BaseLabel, int TargetVersion, string TargetLabel,
    IReadOnlyList<PlanComparisonLineDto> Lines,
    decimal BaseTotalAmount, decimal TargetTotalAmount, decimal TotalDifference);

// ---- 売上 ----

public sealed record RevenuePlanLineDto(
    Guid Id, string ItemName, string Period, decimal Quantity, decimal UnitPrice, decimal Amount);

public sealed record RevenuePlanSummaryDto(
    Guid Id, Guid ProjectId, int Version, string Label, string Status,
    DateTime CreatedAt, DateTime? ApprovedAt, decimal TotalAmount);

public sealed record RevenuePlanDetailDto(
    Guid Id, Guid ProjectId, int Version, string Label, string Status,
    DateTime CreatedAt, DateTime? ApprovedAt, decimal TotalAmount,
    IReadOnlyList<RevenuePlanLineDto> Lines);

public sealed record ActualRevenueDto(
    Guid Id, Guid ProjectId, string ItemName, string Period,
    decimal Quantity, decimal UnitPrice, decimal Amount, string? Note, DateTime RecordedAt);

public sealed record RevenueVarianceLineDto(
    string ItemName, string Period,
    decimal PlannedQuantity, decimal PlannedUnitPrice, decimal PlannedAmount,
    decimal ActualQuantity, decimal ActualUnitPrice, decimal ActualAmount,
    decimal TotalVariance, decimal? PriceVariance, decimal? QuantityVariance,
    bool IsUnplanned, bool IsFavorable);

public sealed record RevenueVarianceReportDto(
    Guid PlanId, int PlanVersion, string PlanLabel,
    IReadOnlyList<RevenueVarianceLineDto> Lines,
    decimal TotalPlannedAmount, decimal TotalActualAmount, decimal TotalVariance);

public sealed record RevenuePlanComparisonLineDto(
    string ItemName, string Period, decimal BaseAmount, decimal TargetAmount, decimal Difference);

public sealed record RevenuePlanComparisonDto(
    int BaseVersion, string BaseLabel, int TargetVersion, string TargetLabel,
    IReadOnlyList<RevenuePlanComparisonLineDto> Lines,
    decimal BaseTotalAmount, decimal TargetTotalAmount, decimal TotalDifference);

// ---- 損益(粗利)サマリ ----

public sealed record ProfitPeriodLineDto(
    string Period,
    decimal PlannedRevenue, decimal ActualRevenue,
    decimal PlannedCost, decimal ActualCost,
    decimal PlannedProfit, decimal ActualProfit, decimal ProfitVariance);

public sealed record ProfitSummaryDto(
    int RevenuePlanVersion, string RevenuePlanLabel,
    int CostPlanVersion, string CostPlanLabel,
    decimal PlannedRevenue, decimal ActualRevenue, decimal RevenueVariance,
    decimal PlannedCost, decimal ActualCost, decimal CostVariance,
    decimal PlannedProfit, decimal ActualProfit, decimal ProfitVariance,
    decimal? PlannedMarginRate, decimal? ActualMarginRate,
    IReadOnlyList<ProfitPeriodLineDto> PeriodLines);

// ---- リクエスト ----

public sealed record CreateProjectRequest(string Code, string Name, int FiscalYear);

public sealed record CreateCostElementRequest(
    string Code, string Name, string Type, bool IsQuantityManaged);

public sealed record CreatePlanRequest(string Label, Guid? BasePlanId);

public sealed record UpsertPlanLineRequest(
    string ElementCode, string Period, decimal Quantity, decimal UnitPrice);

public sealed record RecordActualRequest(
    string ElementCode, string Period, decimal Quantity, decimal UnitPrice, string? Note);

public sealed record UpsertRevenuePlanLineRequest(
    string ItemName, string Period, decimal Quantity, decimal UnitPrice);

public sealed record RecordRevenueRequest(
    string ItemName, string Period, decimal Quantity, decimal UnitPrice, string? Note);

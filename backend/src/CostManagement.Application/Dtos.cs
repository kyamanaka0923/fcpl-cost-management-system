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

// ---- リクエスト ----

public sealed record CreateProjectRequest(string Code, string Name, int FiscalYear);

public sealed record CreateCostElementRequest(
    string Code, string Name, string Type, bool IsQuantityManaged);

public sealed record CreatePlanRequest(string Label, Guid? BasePlanId);

public sealed record UpsertPlanLineRequest(
    string ElementCode, string Period, decimal Quantity, decimal UnitPrice);

public sealed record RecordActualRequest(
    string ElementCode, string Period, decimal Quantity, decimal UnitPrice, string? Note);

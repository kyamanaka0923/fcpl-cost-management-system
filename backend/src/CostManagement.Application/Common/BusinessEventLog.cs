using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace CostManagement.Application.Common;

/// <summary>
/// Structured log messages for business events (state changes that matter for auditing).
/// Event IDs are in the 1000 range so they can be filtered in CloudWatch Logs.
/// The acting user is attached by the WebApi request logging scope, not here.
/// </summary>
internal static partial class BusinessEventLog
{
    /// <summary>A budget draft (initial or revision) was created.</summary>
    [LoggerMessage(EventId = 1001, EventName = "BudgetDraftCreated", Level = LogLevel.Information,
        Message = "Budget draft created: budget {BudgetId}, department {DepartmentId}, {FiscalHalf} v{Version}")]
    public static partial void BudgetDraftCreated(this ILogger logger, Guid budgetId, Guid departmentId,
        FiscalHalf fiscalHalf, int version);

    /// <summary>A budget was approved; previously approved versions were superseded.</summary>
    [LoggerMessage(EventId = 1002, EventName = "BudgetApproved", Level = LogLevel.Information,
        Message = "Budget approved: budget {BudgetId}, department {DepartmentId}, {FiscalHalf} v{Version}; superseded versions [{SupersededVersions}]")]
    public static partial void BudgetApproved(this ILogger logger, Guid budgetId, Guid departmentId,
        FiscalHalf fiscalHalf, int version, IReadOnlyList<int> supersededVersions);

    /// <summary>A division budget was approved.</summary>
    [LoggerMessage(EventId = 1003, EventName = "DivisionBudgetApproved", Level = LogLevel.Information,
        Message = "Division budget approved: division {DivisionId}, {FiscalHalf}")]
    public static partial void DivisionBudgetApproved(this ILogger logger, Guid divisionId, FiscalHalf fiscalHalf);

    /// <summary>A division budget approval was revoked.</summary>
    [LoggerMessage(EventId = 1004, EventName = "DivisionBudgetRevoked", Level = LogLevel.Information,
        Message = "Division budget approval revoked: division {DivisionId}, {FiscalHalf}")]
    public static partial void DivisionBudgetRevoked(this ILogger logger, Guid divisionId, FiscalHalf fiscalHalf);

    /// <summary>An actual entry was recorded.</summary>
    [LoggerMessage(EventId = 1005, EventName = "ActualRecorded", Level = LogLevel.Information,
        Message = "Actual recorded: entry {ActualId}, department {DepartmentId}, {FiscalHalf}, {Category}, amount {Amount}")]
    public static partial void ActualRecorded(this ILogger logger, Guid actualId, Guid departmentId,
        FiscalHalf fiscalHalf, BudgetCategory category, decimal amount);

    /// <summary>An actual entry was deleted.</summary>
    [LoggerMessage(EventId = 1006, EventName = "ActualDeleted", Level = LogLevel.Information,
        Message = "Actual deleted: entry {ActualId}")]
    public static partial void ActualDeleted(this ILogger logger, Guid actualId);
}

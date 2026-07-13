using Amazon.Lambda.AspNetCoreServer.Hosting;
using CostManagement.Application;
using CostManagement.Application.Common;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure;
using CostManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=costmanagement.db";

builder.Services.AddCostManagement(connectionString);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// AWS Lambda(API Gateway HTTP API / Function URL のペイロード v2)で実行するときだけ
// Lambda ランタイムに接続する。Lambda 環境でなければ何もしないため、
// ローカル実行・テスト(WebApplicationFactory)には影響しない。
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

app.Services.GetRequiredService<DatabaseInitializer>().Initialize();

app.UseCors();

// ドメイン例外 → 400, 未検出 → 404 に変換する。
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (DomainException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (NotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

var api = app.MapGroup("/api");

// ---- 部 ----
api.MapGet("/divisions", (DivisionService svc, CancellationToken ct) => svc.ListAsync(ct));
api.MapGet("/divisions/{id:guid}", (Guid id, DivisionService svc, CancellationToken ct) =>
    svc.GetAsync(id, ct));
api.MapPost("/divisions",
    async (CreateDivisionRequest req, DivisionService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateAsync(req, ct)));
api.MapGet("/divisions/{id:guid}/budget-summary",
    (Guid id, string fiscalHalf, AnalysisService svc, CancellationToken ct) =>
        svc.GetDivisionBudgetSummaryAsync(id, fiscalHalf, ct));
api.MapPost("/divisions/{id:guid}/budget-approval",
    async (Guid id, string fiscalHalf, DivisionBudgetApprovalService svc, CancellationToken ct) =>
    {
        await svc.ApproveAsync(id, fiscalHalf, ct);
        return Results.NoContent();
    });
api.MapDelete("/divisions/{id:guid}/budget-approval",
    async (Guid id, string fiscalHalf, DivisionBudgetApprovalService svc, CancellationToken ct) =>
    {
        await svc.RevokeAsync(id, fiscalHalf, ct);
        return Results.NoContent();
    });

// ---- 課(部に属する) ----
api.MapGet("/divisions/{id:guid}/departments",
    (Guid id, DepartmentService svc, CancellationToken ct) => svc.ListByDivisionAsync(id, ct));
api.MapPost("/divisions/{id:guid}/departments",
    async (Guid id, CreateDepartmentRequest req, DepartmentService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateAsync(id, req, ct)));
api.MapGet("/departments/{id:guid}", (Guid id, DepartmentService svc, CancellationToken ct) =>
    svc.GetAsync(id, ct));

// ---- 案件(課に属するマスタ) ----
api.MapGet("/departments/{id:guid}/projects",
    (Guid id, ProjectService svc, CancellationToken ct) => svc.ListByDepartmentAsync(id, ct));
api.MapPost("/departments/{id:guid}/projects",
    async (Guid id, CreateProjectRequest req, ProjectService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateAsync(id, req, ct)));
api.MapGet("/projects/{id:guid}", (Guid id, ProjectService svc, CancellationToken ct) =>
    svc.GetAsync(id, ct));
api.MapPut("/projects/{id:guid}",
    (Guid id, UpdateProjectRequest req, ProjectService svc, CancellationToken ct) =>
        svc.UpdateAsync(id, req, ct));

// ---- 費目マスタ(期間費用) ----
api.MapGet("/cost-elements", (CostElementService svc, CancellationToken ct) => svc.ListAsync(ct));
api.MapPost("/cost-elements",
    async (CreateCostElementRequest req, CostElementService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateAsync(req, ct)));

// ---- 課予算(半期単位・バージョン管理・改定) ----
api.MapGet("/departments/{id:guid}/budgets",
    (Guid id, string fiscalHalf, DepartmentBudgetService svc, CancellationToken ct) =>
        svc.ListAsync(id, fiscalHalf, ct));
api.MapPost("/departments/{id:guid}/budgets",
    async (Guid id, CreateBudgetRequest req, DepartmentBudgetService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateDraftAsync(id, req, ct)));
api.MapGet("/budgets/{budgetId:guid}",
    (Guid budgetId, DepartmentBudgetService svc, CancellationToken ct) => svc.GetAsync(budgetId, ct));
api.MapPut("/budgets/{budgetId:guid}/lines",
    (Guid budgetId, UpsertBudgetLineRequest req, DepartmentBudgetService svc, CancellationToken ct) =>
        svc.UpsertLineAsync(budgetId, req, ct));
api.MapDelete("/budgets/{budgetId:guid}/lines",
    (Guid budgetId, string category, Guid? projectId, string? elementCode,
        DepartmentBudgetService svc, CancellationToken ct) =>
        svc.RemoveLineAsync(budgetId, category, projectId, elementCode, ct));
api.MapPost("/budgets/{budgetId:guid}/approve",
    (Guid budgetId, DepartmentBudgetService svc, CancellationToken ct) =>
        svc.ApproveAsync(budgetId, ct));

// ---- 実績 ----
api.MapGet("/departments/{id:guid}/actuals",
    (Guid id, string fiscalHalf, ActualEntryService svc, CancellationToken ct) =>
        svc.ListAsync(id, fiscalHalf, ct));
api.MapPost("/departments/{id:guid}/actuals",
    async (Guid id, RecordActualRequest req, ActualEntryService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.RecordAsync(id, req, ct)));
api.MapDelete("/actuals/{actualId:guid}",
    async (Guid actualId, ActualEntryService svc, CancellationToken ct) =>
    {
        await svc.DeleteAsync(actualId, ct);
        return Results.NoContent();
    });

// ---- 分析(予実差異・予算バージョン間比較・損益) ----
api.MapGet("/departments/{id:guid}/variance",
    (Guid id, string fiscalHalf, Guid? budgetId, AnalysisService svc, CancellationToken ct) =>
        svc.AnalyzeVarianceAsync(id, fiscalHalf, budgetId, ct));
api.MapGet("/departments/{id:guid}/budget-comparison",
    (Guid id, string fiscalHalf, int baseVersion, int targetVersion, AnalysisService svc,
        CancellationToken ct) =>
        svc.CompareBudgetsAsync(id, fiscalHalf, baseVersion, targetVersion, ct));
api.MapGet("/departments/{id:guid}/profit",
    (Guid id, string fiscalHalf, Guid? budgetId, AnalysisService svc, CancellationToken ct) =>
        svc.GetProfitSummaryAsync(id, fiscalHalf, budgetId, ct));

app.Run();

/// <summary>E2E テスト(WebApplicationFactory)からの参照用。</summary>
public partial class Program
{
}

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

// ---- プロジェクト ----
api.MapGet("/projects", (ProjectService svc, CancellationToken ct) => svc.ListAsync(ct));
api.MapGet("/projects/{id:guid}", (Guid id, ProjectService svc, CancellationToken ct) =>
    svc.GetAsync(id, ct));
api.MapPost("/projects", async (CreateProjectRequest req, ProjectService svc, CancellationToken ct) =>
    Results.Created((string?)null, await svc.CreateAsync(req, ct)));
api.MapPost("/projects/{id:guid}/complete", (Guid id, ProjectService svc, CancellationToken ct) =>
    svc.CompleteAsync(id, ct));

// ---- 費目マスタ ----
api.MapGet("/cost-elements", (CostElementService svc, CancellationToken ct) => svc.ListAsync(ct));
api.MapPost("/cost-elements",
    async (CreateCostElementRequest req, CostElementService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateAsync(req, ct)));

// ---- 原価予算(バージョン管理・改定) ----
api.MapGet("/projects/{id:guid}/plans", (Guid id, CostPlanService svc, CancellationToken ct) =>
    svc.ListByProjectAsync(id, ct));
api.MapPost("/projects/{id:guid}/plans",
    async (Guid id, CreatePlanRequest req, CostPlanService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateDraftAsync(id, req, ct)));
api.MapGet("/plans/{planId:guid}", (Guid planId, CostPlanService svc, CancellationToken ct) =>
    svc.GetAsync(planId, ct));
api.MapPut("/plans/{planId:guid}/lines",
    (Guid planId, UpsertPlanLineRequest req, CostPlanService svc, CancellationToken ct) =>
        svc.UpsertLineAsync(planId, req, ct));
api.MapDelete("/plans/{planId:guid}/lines",
    (Guid planId, string elementCode, string period, CostPlanService svc, CancellationToken ct) =>
        svc.RemoveLineAsync(planId, elementCode, period, ct));
api.MapPost("/plans/{planId:guid}/approve",
    (Guid planId, CostPlanService svc, CancellationToken ct) => svc.ApproveAsync(planId, ct));

// ---- 原価実績 ----
api.MapGet("/projects/{id:guid}/actuals", (Guid id, ActualCostService svc, CancellationToken ct) =>
    svc.ListByProjectAsync(id, ct));
api.MapPost("/projects/{id:guid}/actuals",
    async (Guid id, RecordActualRequest req, ActualCostService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.RecordAsync(id, req, ct)));
api.MapDelete("/actuals/{actualId:guid}",
    async (Guid actualId, ActualCostService svc, CancellationToken ct) =>
    {
        await svc.DeleteAsync(actualId, ct);
        return Results.NoContent();
    });

// ---- 売上予算(バージョン管理・改定) ----
api.MapGet("/projects/{id:guid}/revenue-plans",
    (Guid id, RevenuePlanService svc, CancellationToken ct) => svc.ListByProjectAsync(id, ct));
api.MapPost("/projects/{id:guid}/revenue-plans",
    async (Guid id, CreatePlanRequest req, RevenuePlanService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.CreateDraftAsync(id, req, ct)));
api.MapGet("/revenue-plans/{planId:guid}",
    (Guid planId, RevenuePlanService svc, CancellationToken ct) => svc.GetAsync(planId, ct));
api.MapPut("/revenue-plans/{planId:guid}/lines",
    (Guid planId, UpsertRevenuePlanLineRequest req, RevenuePlanService svc, CancellationToken ct) =>
        svc.UpsertLineAsync(planId, req, ct));
api.MapDelete("/revenue-plans/{planId:guid}/lines",
    (Guid planId, string itemName, string period, RevenuePlanService svc, CancellationToken ct) =>
        svc.RemoveLineAsync(planId, itemName, period, ct));
api.MapPost("/revenue-plans/{planId:guid}/approve",
    (Guid planId, RevenuePlanService svc, CancellationToken ct) => svc.ApproveAsync(planId, ct));

// ---- 売上実績 ----
api.MapGet("/projects/{id:guid}/actual-revenues",
    (Guid id, ActualRevenueService svc, CancellationToken ct) => svc.ListByProjectAsync(id, ct));
api.MapPost("/projects/{id:guid}/actual-revenues",
    async (Guid id, RecordRevenueRequest req, ActualRevenueService svc, CancellationToken ct) =>
        Results.Created((string?)null, await svc.RecordAsync(id, req, ct)));
api.MapDelete("/actual-revenues/{actualId:guid}",
    async (Guid actualId, ActualRevenueService svc, CancellationToken ct) =>
    {
        await svc.DeleteAsync(actualId, ct);
        return Results.NoContent();
    });

// ---- 分析(予実差異・予算バージョン間比較) ----
api.MapGet("/projects/{id:guid}/variance",
    (Guid id, Guid? planId, string? from, string? to, AnalysisService svc, CancellationToken ct) =>
        svc.AnalyzeVarianceAsync(id, planId, from, to, ct));
api.MapGet("/projects/{id:guid}/plan-comparison",
    (Guid id, int baseVersion, int targetVersion, AnalysisService svc, CancellationToken ct) =>
        svc.ComparePlansAsync(id, baseVersion, targetVersion, ct));
api.MapGet("/projects/{id:guid}/revenue-variance",
    (Guid id, Guid? planId, string? from, string? to, AnalysisService svc, CancellationToken ct) =>
        svc.AnalyzeRevenueVarianceAsync(id, planId, from, to, ct));
api.MapGet("/projects/{id:guid}/revenue-plan-comparison",
    (Guid id, int baseVersion, int targetVersion, AnalysisService svc, CancellationToken ct) =>
        svc.CompareRevenuePlansAsync(id, baseVersion, targetVersion, ct));
api.MapGet("/projects/{id:guid}/profit",
    (Guid id, string? from, string? to, AnalysisService svc, CancellationToken ct) =>
        svc.GetProfitSummaryAsync(id, from, to, ct));

app.Run();

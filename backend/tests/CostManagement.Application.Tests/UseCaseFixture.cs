using CostManagement.Application;
using CostManagement.Application.Common;
using CostManagement.Domain.Analysis;
using CostManagement.Infrastructure.Persistence;
using CostManagement.Infrastructure.Repositories;

namespace CostManagement.Application.Tests;

/// <summary>テスト用の固定時計。</summary>
public sealed class FixedClock : ISystemClock
{
    public DateTime UtcNow { get; set; } = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// ユースケーステスト用フィクスチャ。
/// テストごとに独立した一時 SQLite データベースを作成し、
/// 本物のリポジトリ実装を注入したアプリケーションサービス一式を提供する。
/// </summary>
public sealed class UseCaseFixture : IDisposable
{
    private readonly string _dbPath;

    public FixedClock Clock { get; } = new();

    public ProjectService Projects { get; }
    public CostElementService CostElements { get; }
    public CostPlanService CostPlans { get; }
    public ActualCostService ActualCosts { get; }
    public RevenuePlanService RevenuePlans { get; }
    public ActualRevenueService ActualRevenues { get; }
    public AnalysisService Analysis { get; }

    public UseCaseFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-usecase-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(factory).Initialize();

        var projects = new ProjectRepository(factory);
        var elements = new CostElementRepository(factory);
        var costPlans = new CostPlanRepository(factory);
        var actualCosts = new ActualCostRepository(factory);
        var revenuePlans = new RevenuePlanRepository(factory);
        var actualRevenues = new ActualRevenueRepository(factory);

        Projects = new ProjectService(projects, Clock);
        CostElements = new CostElementService(elements);
        CostPlans = new CostPlanService(costPlans, projects, elements, Clock);
        ActualCosts = new ActualCostService(actualCosts, projects, elements, Clock);
        RevenuePlans = new RevenuePlanService(revenuePlans, projects, Clock);
        ActualRevenues = new ActualRevenueService(actualRevenues, projects, Clock);
        Analysis = new AnalysisService(costPlans, actualCosts, revenuePlans, actualRevenues,
            new VarianceAnalysisService(), new PlanComparisonService(),
            new RevenueVarianceAnalysisService(), new RevenuePlanComparisonService(),
            new ProfitAnalysisService());
    }

    // ---- よく使う操作のヘルパ(テストを読みやすく保つ) ----

    public async Task<ProjectDto> プロジェクトを作成(string code = "PJ-001",
        string name = "受託開発2026") =>
        await Projects.CreateAsync(new CreateProjectRequest(code, name, 2026));

    public async Task<CostPlanDetailDto> 承認済み原価予算を作成(Guid projectId,
        params (string 費目, string? 品目, string 年月, decimal 金額)[] 明細)
    {
        var plan = await CostPlans.CreateDraftAsync(projectId, new CreatePlanRequest("当初原価予算", null));
        foreach (var (費目, 品目, 年月, 金額) in 明細)
            await CostPlans.UpsertLineAsync(plan.Id,
                new UpsertPlanLineRequest(費目, 品目, 年月, 金額));
        return await CostPlans.ApproveAsync(plan.Id);
    }

    public async Task<RevenuePlanDetailDto> 承認済み売上予算を作成(Guid projectId,
        params (string 品目, string 年月, decimal 金額)[] 明細)
    {
        var plan = await RevenuePlans.CreateDraftAsync(projectId, new CreatePlanRequest("当初売上予算", null));
        foreach (var (品目, 年月, 金額) in 明細)
            await RevenuePlans.UpsertLineAsync(plan.Id,
                new UpsertRevenuePlanLineRequest(品目, 年月, 金額));
        return await RevenuePlans.ApproveAsync(plan.Id);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

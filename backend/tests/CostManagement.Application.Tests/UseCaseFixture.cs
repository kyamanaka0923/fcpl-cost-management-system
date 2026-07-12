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

    public DivisionService Divisions { get; }
    public DivisionBudgetApprovalService DivisionApprovals { get; }
    public DepartmentService Departments { get; }
    public ProjectService Projects { get; }
    public CostElementService CostElements { get; }
    public DepartmentBudgetService Budgets { get; }
    public ActualEntryService Actuals { get; }
    public AnalysisService Analysis { get; }

    public UseCaseFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-usecase-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(factory).Initialize();

        var divisions = new DivisionRepository(factory);
        var divisionApprovals = new DivisionBudgetApprovalRepository(factory);
        var departments = new DepartmentRepository(factory);
        var projects = new ProjectRepository(factory);
        var elements = new CostElementRepository(factory);
        var budgets = new DepartmentBudgetRepository(factory);
        var actuals = new ActualEntryRepository(factory);

        Divisions = new DivisionService(divisions, Clock);
        DivisionApprovals = new DivisionBudgetApprovalService(divisionApprovals, divisions,
            departments, budgets, Clock);
        Departments = new DepartmentService(departments, divisions, Clock);
        Projects = new ProjectService(projects, departments, Clock);
        CostElements = new CostElementService(elements);
        Budgets = new DepartmentBudgetService(budgets, departments, projects, elements, Clock);
        Actuals = new ActualEntryService(actuals, departments, projects, elements, Clock);
        Analysis = new AnalysisService(budgets, actuals, projects, elements, departments, divisions,
            divisionApprovals,
            new BudgetVarianceAnalysisService(), new BudgetComparisonService(),
            new ProfitAnalysisService(), new DivisionBudgetSummaryService());
    }

    // ---- よく使う操作のヘルパ(テストを読みやすく保つ) ----

    public async Task<DivisionDto> 部を作成(string code = "SALES", string name = "営業本部") =>
        await Divisions.CreateAsync(new CreateDivisionRequest(code, name));

    public async Task<DepartmentDto> 課を作成(Guid divisionId, string code = "DEV-1",
        string name = "開発1課") =>
        await Departments.CreateAsync(divisionId, new CreateDepartmentRequest(code, name));

    /// <summary>部を意識しないテスト向けに、課ごとに専用の部を自動生成して課を作る。</summary>
    public async Task<DepartmentDto> 部と課を作成(string code = "DEV-1", string name = "開発1課")
    {
        var division = await 部を作成($"DIV-{code}", $"{name}を含む部");
        return await 課を作成(division.Id, code, name);
    }

    public async Task<ProjectDto> 案件を作成(Guid departmentId, string code = "PJ-001",
        string name = "受託開発A") =>
        await Projects.CreateAsync(departmentId, new CreateProjectRequest(code, name));

    /// <summary>
    /// 4区分の明細を登録して承認済みの課予算を作る。
    /// 案件別区分は 案件Id、期間費用は 費目コード を指定する。
    /// </summary>
    public async Task<BudgetDetailDto> 承認済み予算を作成(Guid departmentId,
        string 半期 = "2026-H1",
        params (string 区分, Guid? 案件, string? 費目, decimal 金額)[] 明細)
    {
        var budget = await Budgets.CreateDraftAsync(departmentId,
            new CreateBudgetRequest(半期, "当初予算"));
        foreach (var (区分, 案件, 費目, 金額) in 明細)
            await Budgets.UpsertLineAsync(budget.Id,
                new UpsertBudgetLineRequest(区分, 案件, 費目, 金額));
        return await Budgets.ApproveAsync(budget.Id);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

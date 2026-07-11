using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Persistence;
using CostManagement.Infrastructure.Repositories;

namespace CostManagement.Infrastructure.Tests;

/// <summary>
/// リポジトリテスト用フィクスチャ。テストごとに独立した一時 SQLite データベースを用意する。
/// </summary>
public sealed class RepositoryFixture : IDisposable
{
    private readonly string _dbPath;

    public SqliteConnectionFactory Factory { get; }
    public DateTime Now { get; } = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public FiscalHalf Half { get; } = new(2026, HalfTerm.H1);

    public RepositoryFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-repo-{Guid.NewGuid():N}.db");
        Factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(Factory).Initialize();
    }

    public async Task<Department> 課を保存(string code = "DEV-1")
    {
        var department = Department.Create(code, "開発1課", Now);
        await new DepartmentRepository(Factory).AddAsync(department);
        return department;
    }

    public async Task<Project> 案件を保存(DepartmentId departmentId, string code = "PJ-001")
    {
        var project = Project.Create(departmentId, code, "テスト案件", Now);
        await new ProjectRepository(Factory).AddAsync(project);
        return project;
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}

/// <summary>永続化アダプタ: 保存した集約が同じ状態で復元されること(ラウンドトリップ)。</summary>
public class リポジトリの永続化ラウンドトリップ : IDisposable
{
    private readonly RepositoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 課は作成時の状態のまま復元される()
    {
        var repo = new DepartmentRepository(_fx.Factory);
        var department = Department.Create("DEV-1", "開発1課", _fx.Now);
        await repo.AddAsync(department);

        var restored = await repo.FindByIdAsync(department.Id);

        Assert.NotNull(restored);
        Assert.Equal(department.Code, restored.Code);
        Assert.Equal(department.Name, restored.Name);
        Assert.Equal(department.CreatedAt, restored.CreatedAt);
    }

    [Fact]
    public async Task 案件は所属する課を含めて復元される()
    {
        var dept = await _fx.課を保存();
        var repo = new ProjectRepository(_fx.Factory);
        var project = Project.Create(dept.Id, "PJ-001", "受託開発A", _fx.Now);
        await repo.AddAsync(project);

        var restored = await repo.FindByIdAsync(project.Id);

        Assert.NotNull(restored);
        Assert.Equal(dept.Id, restored.DepartmentId);
        Assert.Equal(project.Code, restored.Code);
        Assert.Equal(ProjectStatus.Active, restored.Status);
    }

    [Fact]
    public async Task 案件の更新が永続化される()
    {
        var dept = await _fx.課を保存();
        var repo = new ProjectRepository(_fx.Factory);
        var project = await _fx.案件を保存(dept.Id);

        project.Rename("名称変更後");
        project.Complete();
        await repo.UpdateAsync(project);

        var restored = await repo.FindByIdAsync(project.Id);
        Assert.Equal("名称変更後", restored!.Name);
        Assert.Equal(ProjectStatus.Completed, restored.Status);
    }

    [Fact]
    public async Task 課予算は案件別明細と期間費用明細を含めて復元される()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(1_234_567.89m));
        budget.UpsertPeriodCostLine(new CostElementCode("PERSONNEL"), new Money(300_000m));
        budget.Approve(_fx.Now);
        await repo.AddAsync(budget);

        var restored = await repo.FindByIdAsync(budget.Id);

        Assert.NotNull(restored);
        Assert.Equal(BudgetStatus.Approved, restored.Status);
        Assert.Equal(_fx.Now, restored.ApprovedAt);
        Assert.Equal(_fx.Half, restored.FiscalHalf);
        Assert.Equal(2, restored.Lines.Count);

        var revenueLine = restored.Lines.Single(l => l.Category == BudgetCategory.Revenue);
        Assert.Equal(project.Id, revenueLine.ProjectId);
        Assert.Null(revenueLine.ElementCode);
        Assert.Equal(1_234_567.89m, revenueLine.Amount.Value); // 小数金額の往復

        var periodLine = restored.Lines.Single(l => l.Category == BudgetCategory.PeriodCost);
        Assert.Null(periodLine.ProjectId);
        Assert.Equal("PERSONNEL", periodLine.ElementCode!.Value.Value);
    }

    [Fact]
    public async Task 課予算の更新は明細の洗い替えとして永続化される()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(1_000_000m));
        await repo.AddAsync(budget);

        budget.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(1_500_000m));
        budget.UpsertProjectLine(BudgetCategory.Processing, project.Id, new Money(600_000m));
        await repo.UpdateAsync(budget);

        var restored = await repo.FindByIdAsync(budget.Id);
        Assert.Equal(2, restored!.Lines.Count);
        Assert.Equal(1_500_000m, restored.CategoryTotal(BudgetCategory.Revenue).Value);
    }

    [Fact]
    public async Task 最新の承認済み予算とバージョン最大値を取得できる()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var v1 = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        v1.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(1_000_000m));
        v1.Approve(_fx.Now);
        await repo.AddAsync(v1);

        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", _fx.Now);
        v2.Approve(_fx.Now);
        await repo.AddAsync(v2);
        v1.Supersede();
        await repo.UpdateAsync(v1);

        var latest = await repo.FindLatestApprovedAsync(dept.Id, _fx.Half);
        Assert.Equal(2, latest!.Version);
        Assert.Equal(2, await repo.GetMaxVersionAsync(dept.Id, _fx.Half));
        // 別の半期には影響しない
        Assert.Null(await repo.FindLatestApprovedAsync(dept.Id, new FiscalHalf(2026, HalfTerm.H2)));
        Assert.Equal(0, await repo.GetMaxVersionAsync(dept.Id, new FiscalHalf(2026, HalfTerm.H2)));
    }

    [Fact]
    public async Task 実績は案件と費目のどちらのキーでも復元される()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new ActualEntryRepository(_fx.Factory);

        var projectEntry = ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.Processing,
            project.Id, null, new Money(800_000m), "4月分", _fx.Now);
        var periodEntry = ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.PeriodCost,
            null, new CostElementCode("PERSONNEL"), new Money(500_000m), null, _fx.Now);
        await repo.AddAsync(projectEntry);
        await repo.AddAsync(periodEntry);

        var entries = await repo.ListAsync(dept.Id, _fx.Half);
        Assert.Equal(2, entries.Count);

        var restoredProject = entries.Single(e => e.Category == BudgetCategory.Processing);
        Assert.Equal(project.Id, restoredProject.ProjectId);
        Assert.Null(restoredProject.ElementCode);
        Assert.Equal("4月分", restoredProject.Note);

        var restoredPeriod = entries.Single(e => e.Category == BudgetCategory.PeriodCost);
        Assert.Null(restoredPeriod.ProjectId);
        Assert.Equal("PERSONNEL", restoredPeriod.ElementCode!.Value.Value);
    }

    [Fact]
    public async Task 実績を削除できる()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new ActualEntryRepository(_fx.Factory);

        var entry = ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.Revenue,
            project.Id, null, new Money(100_000m), null, _fx.Now);
        await repo.AddAsync(entry);
        await repo.DeleteAsync(entry.Id);

        Assert.Null(await repo.FindByIdAsync(entry.Id));
    }

    [Fact]
    public async Task 費目マスタにはシード済みの標準費目が存在する()
    {
        var repo = new CostElementRepository(_fx.Factory);

        var elements = await repo.ListAsync();

        Assert.Contains(elements, e => e.Code.Value == "PERSONNEL" && e.Name == "人件費");
        Assert.Contains(elements, e => e.Code.Value == "LICENSE" && e.Name == "ライセンス費");
    }
}

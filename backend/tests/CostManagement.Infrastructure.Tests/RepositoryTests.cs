using CostManagement.Domain.Actuals;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Planning;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Revenue;
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

    public RepositoryFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cm-repo-{Guid.NewGuid():N}.db");
        Factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(Factory).Initialize();
    }

    public async Task<Project> プロジェクトを保存(string code = "PJ-001")
    {
        var project = Project.Create(code, "テストプロジェクト", 2026, Now);
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
    public async Task プロジェクトは作成時の状態のまま復元される()
    {
        var repo = new ProjectRepository(_fx.Factory);
        var project = Project.Create("PJ-001", "受託開発2026", 2026, _fx.Now);
        await repo.AddAsync(project);

        var restored = await repo.FindByIdAsync(project.Id);

        Assert.NotNull(restored);
        Assert.Equal(project.Code, restored.Code);
        Assert.Equal(project.Name, restored.Name);
        Assert.Equal(project.FiscalYear, restored.FiscalYear);
        Assert.Equal(ProjectStatus.Active, restored.Status);
        Assert.Equal(project.CreatedAt, restored.CreatedAt);
    }

    [Fact]
    public async Task プロジェクトの更新が永続化される()
    {
        var repo = new ProjectRepository(_fx.Factory);
        var project = await _fx.プロジェクトを保存();

        project.Rename("名称変更後");
        project.Complete();
        await repo.UpdateAsync(project);

        var restored = await repo.FindByIdAsync(project.Id);
        Assert.Equal("名称変更後", restored!.Name);
        Assert.Equal(ProjectStatus.Completed, restored.Status);
    }

    [Fact]
    public async Task 原価予算は明細と売上対応品目を含めて復元される()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new CostPlanRepository(_fx.Factory);

        var plan = CostPlan.CreateInitial(project.Id, "当初原価予算", _fx.Now);
        plan.UpsertLine(new CostElementCode("LAB-SE"), "案件A",
            AccountingPeriod.Parse("2026-04"), new Money(1_234_567.89m));
        plan.UpsertLine(new CostElementCode("OVH-COM"), null, // 共通費(品目なし)
            AccountingPeriod.Parse("2026-05"), new Money(300_000m));
        plan.Approve(_fx.Now);
        await repo.AddAsync(plan);

        var restored = await repo.FindByIdAsync(plan.Id);

        Assert.NotNull(restored);
        Assert.Equal(PlanStatus.Approved, restored.Status);
        Assert.Equal(_fx.Now, restored.ApprovedAt);
        Assert.Equal(2, restored.Lines.Count);

        var 案件A = restored.Lines.Single(l => l.RevenueItem == "案件A");
        Assert.Equal(1_234_567.89m, 案件A.Amount.Value); // 小数を含む金額が正確に往復する
        Assert.Equal("LAB-SE", 案件A.ElementCode.Value);

        var 共通費 = restored.Lines.Single(l => l.RevenueItem is null);
        Assert.Equal(300_000m, 共通費.Amount.Value);
    }

    [Fact]
    public async Task 原価予算の更新は明細の洗い替えとして永続化される()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new CostPlanRepository(_fx.Factory);

        var plan = CostPlan.CreateInitial(project.Id, "当初原価予算", _fx.Now);
        plan.UpsertLine(new CostElementCode("LAB-SE"), "案件A",
            AccountingPeriod.Parse("2026-04"), new Money(100_000m));
        await repo.AddAsync(plan);

        plan.RemoveLine(new CostElementCode("LAB-SE"), "案件A", AccountingPeriod.Parse("2026-04"));
        plan.UpsertLine(new CostElementCode("SUB-DEV"), "案件B",
            AccountingPeriod.Parse("2026-05"), new Money(200_000m));
        await repo.UpdateAsync(plan);

        var restored = await repo.FindByIdAsync(plan.Id);
        var line = Assert.Single(restored!.Lines);
        Assert.Equal("SUB-DEV", line.ElementCode.Value);
    }

    [Fact]
    public async Task 最新承認版と最大バージョンを正しく取得できる()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new CostPlanRepository(_fx.Factory);

        var v1 = CostPlan.CreateInitial(project.Id, "当初", _fx.Now);
        v1.UpsertLine(new CostElementCode("LAB-SE"), null,
            AccountingPeriod.Parse("2026-04"), new Money(1m));
        v1.Approve(_fx.Now);
        await repo.AddAsync(v1);

        var v2 = CostPlan.ReviseFrom(v1, 2, "改定", _fx.Now);
        v2.Approve(_fx.Now);
        await repo.AddAsync(v2);
        v1.Supersede();
        await repo.UpdateAsync(v1);

        var latest = await repo.FindLatestApprovedAsync(project.Id);
        Assert.Equal(2, latest!.Version);
        Assert.Equal(2, await repo.GetMaxVersionAsync(project.Id));
        Assert.Equal(0, await repo.GetMaxVersionAsync(new ProjectId(Guid.NewGuid())));
    }

    [Fact]
    public async Task 売上予算は明細を含めて復元される()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new RevenuePlanRepository(_fx.Factory);

        var plan = RevenuePlan.CreateInitial(project.Id, "当初売上予算", _fx.Now);
        plan.UpsertLine("案件A", AccountingPeriod.Parse("2026-04"), new Money(2_000_000m));
        plan.Approve(_fx.Now);
        await repo.AddAsync(plan);

        var restored = await repo.FindByIdAsync(plan.Id);

        Assert.Equal(PlanStatus.Approved, restored!.Status);
        var line = Assert.Single(restored.Lines);
        Assert.Equal("案件A", line.ItemName);
        Assert.Equal(2_000_000m, line.Amount.Value);
    }

    [Fact]
    public async Task 原価実績は摘要と売上対応品目を含めて復元され削除できる()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new ActualCostRepository(_fx.Factory);

        var actual = ActualCost.Record(project.Id, new CostElementCode("LAB-SE"), "案件A",
            AccountingPeriod.Parse("2026-04"), new Money(480_000m), "SE 0.8人月", _fx.Now);
        var common = ActualCost.Record(project.Id, new CostElementCode("OVH-COM"), null,
            AccountingPeriod.Parse("2026-04"), new Money(50_000m), null, _fx.Now);
        await repo.AddAsync(actual);
        await repo.AddAsync(common);

        var listed = await repo.ListByProjectAsync(project.Id);
        Assert.Equal(2, listed.Count);
        Assert.Equal("SE 0.8人月", listed.Single(a => a.RevenueItem == "案件A").Note);
        Assert.Null(listed.Single(a => a.RevenueItem is null).RevenueItem);

        await repo.DeleteAsync(actual.Id);
        Assert.Single(await repo.ListByProjectAsync(project.Id));
    }

    [Fact]
    public async Task 売上実績は復元され削除できる()
    {
        var project = await _fx.プロジェクトを保存();
        var repo = new ActualRevenueRepository(_fx.Factory);

        var actual = ActualRevenue.Record(project.Id, "案件A",
            AccountingPeriod.Parse("2026-04"), new Money(2_000_000m), "検収", _fx.Now);
        await repo.AddAsync(actual);

        var restored = await repo.FindByIdAsync(actual.Id);
        Assert.Equal("案件A", restored!.ItemName);
        Assert.Equal("検収", restored.Note);

        await repo.DeleteAsync(actual.Id);
        Assert.Null(await repo.FindByIdAsync(actual.Id));
    }

    [Fact]
    public async Task 費目マスタを追加してコードで取得できる()
    {
        var repo = new CostElementRepository(_fx.Factory);
        await repo.AddAsync(CostElement.Create("EXP-EDU", "教育研修費", CostElementType.Expense));

        var restored = await repo.FindByCodeAsync(new CostElementCode("EXP-EDU"));

        Assert.Equal("教育研修費", restored!.Name);
        Assert.Equal(CostElementType.Expense, restored.Type);
    }
}

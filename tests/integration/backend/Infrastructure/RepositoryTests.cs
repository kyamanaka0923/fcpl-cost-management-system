using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
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

    public async Task<Division> 部を保存(string code = "SALES")
    {
        var division = Division.Create(code, "営業本部", Now);
        await new DivisionRepository(Factory).AddAsync(division);
        return division;
    }

    public async Task<Department> 課を保存(string code = "DEV-1")
    {
        var division = await 部を保存($"DIV-{code}");
        var department = Department.Create(division.Id, code, "開発1課", Now);
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
    public async Task 部は作成時の状態のまま復元される()
    {
        var repo = new DivisionRepository(_fx.Factory);
        var division = Division.Create("SALES", "営業本部", _fx.Now);
        await repo.AddAsync(division);

        var restored = await repo.FindByIdAsync(division.Id);

        Assert.NotNull(restored);
        Assert.Equal(division.Code, restored.Code);
        Assert.Equal(division.Name, restored.Name);
        Assert.Equal(division.CreatedAt, restored.CreatedAt);
    }

    [Fact]
    public async Task 課は所属する部を含めて復元される()
    {
        var division = await _fx.部を保存();
        var repo = new DepartmentRepository(_fx.Factory);
        var department = Department.Create(division.Id, "DEV-1", "開発1課", _fx.Now);
        await repo.AddAsync(department);

        var restored = await repo.FindByIdAsync(department.Id);

        Assert.NotNull(restored);
        Assert.Equal(division.Id, restored.DivisionId);
        Assert.Equal(department.Code, restored.Code);

        var listed = await repo.ListByDivisionAsync(division.Id);
        Assert.Contains(listed, d => d.Id == department.Id);
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
    }

    [Fact]
    public async Task 案件の更新が永続化される()
    {
        var dept = await _fx.課を保存();
        var repo = new ProjectRepository(_fx.Factory);
        var project = await _fx.案件を保存(dept.Id);

        project.Edit("PJ-CHG", "名称変更後");
        await repo.UpdateAsync(project);

        var restored = await repo.FindByIdAsync(project.Id);
        Assert.Equal("名称変更後", restored!.Name);
        Assert.Equal("PJ-CHG", restored.Code);
    }

    [Fact]
    public async Task 案件コードの検索は課ごとにスコープされ別の課で同じコードを持てる()
    {
        var div = await _fx.部を保存();
        var deptRepo = new DepartmentRepository(_fx.Factory);
        var 課1 = Department.Create(div.Id, "DEV-1", "開発1課", _fx.Now);
        var 課2 = Department.Create(div.Id, "DEV-2", "開発2課", _fx.Now);
        await deptRepo.AddAsync(課1);
        await deptRepo.AddAsync(課2);

        var repo = new ProjectRepository(_fx.Factory);
        // 別の課で同じコードを登録できる(UNIQUE(department_id, code))
        await repo.AddAsync(Project.Create(課1.Id, "PJ-001", "課1の案件", _fx.Now));
        await repo.AddAsync(Project.Create(課2.Id, "PJ-001", "課2の案件", _fx.Now));

        // 検索は課ごとにスコープされる
        Assert.Equal("課1の案件", (await repo.FindByCodeAsync(課1.Id, "PJ-001"))!.Name);
        Assert.Equal("課2の案件", (await repo.FindByCodeAsync(課2.Id, "PJ-001"))!.Name);
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
    public async Task 月次明細は月別金額を保持したままラウンドトリップする()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLineMonthly(BudgetCategory.Revenue, project.Id,
            new Dictionary<int, Money> { [1] = new(1_000_000m), [3] = new(500_000m) });
        await repo.AddAsync(budget);

        var restored = await repo.FindByIdAsync(budget.Id);

        var line = Assert.Single(restored!.Lines);
        Assert.True(line.IsMonthly);
        Assert.Equal(1_500_000m, line.Amount.Value);
        Assert.Equal(2, line.MonthlyAmounts.Count);
        Assert.Equal(1_000_000m, line.MonthlyAmounts[1].Value);
        Assert.Equal(500_000m, line.MonthlyAmounts[3].Value);
    }

    [Fact]
    public async Task 実績は計上月を保持したままラウンドトリップする()
    {
        var dept = await _fx.課を保存();
        var project = await _fx.案件を保存(dept.Id);
        var repo = new ActualEntryRepository(_fx.Factory);

        await repo.AddAsync(ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.Revenue,
            project.Id, null, 5, new Money(700_000m), null, _fx.Now));

        var restored = Assert.Single(await repo.ListAsync(dept.Id, _fx.Half));
        Assert.Equal(5, restored.Month);
    }

    [Fact]
    public async Task 期間費用の明細名は保持したままラウンドトリップする()
    {
        var dept = await _fx.課を保存();
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertPeriodCostLine(new CostElementCode("LICENSE"), new Money(300_000m), "AWS");
        budget.UpsertPeriodCostLine(new CostElementCode("LICENSE"), new Money(200_000m), "GitHub");
        await repo.AddAsync(budget);

        var restored = await repo.FindByIdAsync(budget.Id);

        Assert.Equal(2, restored!.Lines.Count);
        Assert.Equal(500_000m, restored.CategoryTotal(BudgetCategory.PeriodCost).Value);
        var aws = restored.Lines.Single(l => l.PeriodDetail == "AWS");
        Assert.Equal("LICENSE", aws.ElementCode!.Value.Value);
        Assert.Equal(300_000m, aws.Amount.Value);
    }

    [Fact]
    public async Task 実績は期間費用の明細名を保持したままラウンドトリップする()
    {
        var dept = await _fx.課を保存();
        var repo = new ActualEntryRepository(_fx.Factory);

        await repo.AddAsync(ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.PeriodCost,
            null, new CostElementCode("LICENSE"), null, new Money(320_000m), null, _fx.Now,
            periodDetail: "AWS"));

        var restored = Assert.Single(await repo.ListAsync(dept.Id, _fx.Half));
        Assert.Equal("AWS", restored.PeriodDetail);
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
            project.Id, null, null, new Money(800_000m), "4月分", _fx.Now);
        var periodEntry = ActualEntry.Record(dept.Id, _fx.Half, BudgetCategory.PeriodCost,
            null, new CostElementCode("PERSONNEL"), null, new Money(500_000m), null, _fx.Now);
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
            project.Id, null, null, new Money(100_000m), null, _fx.Now);
        await repo.AddAsync(entry);
        await repo.DeleteAsync(entry.Id);

        Assert.Null(await repo.FindByIdAsync(entry.Id));
    }

    [Fact]
    public async Task 部承認は保存と取り消しができる()
    {
        var division = await _fx.部を保存();
        var repo = new DivisionBudgetApprovalRepository(_fx.Factory);

        var approval = DivisionBudgetApproval.Approve(division.Id, _fx.Half, _fx.Now);
        await repo.AddAsync(approval);

        var restored = await repo.FindAsync(division.Id, _fx.Half);
        Assert.NotNull(restored);
        Assert.Equal(division.Id, restored.DivisionId);
        Assert.Equal(_fx.Half, restored.FiscalHalf);
        Assert.Equal(_fx.Now, restored.ApprovedAt);

        await repo.DeleteAsync(division.Id, _fx.Half);
        Assert.Null(await repo.FindAsync(division.Id, _fx.Half));
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

/// <summary>
/// 部集計用の一括取得: 配下課ぶんの (課, 区分) 別合計を、課ごとに問い合わせずまとめて引けること。
/// 課数に比例してクエリが増える N+1 と、集計に不要な明細・月別金額の復元を避けるための経路。
/// </summary>
public class 複数課の区分別合計の一括取得 : IDisposable
{
    private readonly RepositoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    /// <summary>指定額の売上明細を1件持つ承認済み予算を保存し、その課を返す。</summary>
    private async Task<Department> 承認済み予算のある課を保存(string code, decimal 売上)
    {
        var dept = await _fx.課を保存(code);
        var project = await _fx.案件を保存(dept.Id, $"PJ-{code}");
        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(売上));
        budget.Approve(_fx.Now);
        await new DepartmentBudgetRepository(_fx.Factory).AddAsync(budget);
        return dept;
    }

    private static decimal 合計(IReadOnlyList<DepartmentCategoryAmount> amounts,
        DepartmentId dept, BudgetCategory category) =>
        amounts.SingleOrDefault(a => a.DepartmentId == dept && a.Category == category)
            .Amount.Value;

    [Fact]
    public async Task 複数課の予算合計を区分別にまとめて取得できる()
    {
        var dept1 = await 承認済み予算のある課を保存("DEV-1", 1_000_000m);
        var dept2 = await 承認済み予算のある課を保存("DEV-2", 2_000_000m);
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var totals = await repo.SumLatestApprovedByCategoryAsync([dept1.Id, dept2.Id], _fx.Half);

        Assert.Equal(1_000_000m, 合計(totals, dept1.Id, BudgetCategory.Revenue));
        Assert.Equal(2_000_000m, 合計(totals, dept2.Id, BudgetCategory.Revenue));
        // 明細のない区分は結果に含まれない
        Assert.DoesNotContain(totals, a => a.Category == BudgetCategory.Outsourcing);
    }

    [Fact]
    public async Task 予算合計は同じ区分の明細をすべて足し合わせる()
    {
        var dept = await _fx.課を保存("DEV-1");
        var pj1 = await _fx.案件を保存(dept.Id, "PJ-1");
        var pj2 = await _fx.案件を保存(dept.Id, "PJ-2");
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var budget = DepartmentBudget.CreateInitial(dept.Id, _fx.Half, "当初予算", _fx.Now);
        budget.UpsertProjectLine(BudgetCategory.Revenue, pj1.Id, new Money(1_000_000m));
        budget.UpsertProjectLine(BudgetCategory.Revenue, pj2.Id, new Money(1_500_000m));
        // 月次入力の明細は半期合計(月別の合計)で数える
        budget.UpsertProjectLineMonthly(BudgetCategory.Processing, pj1.Id,
            new Dictionary<int, Money> { [1] = new(100_000m), [2] = new(200_000m) });
        // 期間費用は明細名で細分できる
        budget.UpsertPeriodCostLine(new CostElementCode("PERSONNEL"), new Money(400_000m), "正社員");
        budget.UpsertPeriodCostLine(new CostElementCode("PERSONNEL"), new Money(150_000m), "派遣");
        budget.Approve(_fx.Now);
        await repo.AddAsync(budget);

        var totals = await repo.SumLatestApprovedByCategoryAsync([dept.Id], _fx.Half);

        Assert.Equal(2_500_000m, 合計(totals, dept.Id, BudgetCategory.Revenue));
        Assert.Equal(300_000m, 合計(totals, dept.Id, BudgetCategory.Processing));
        Assert.Equal(550_000m, 合計(totals, dept.Id, BudgetCategory.PeriodCost));
    }

    [Fact]
    public async Task 承認済み予算のない課は予算合計に含まれない()
    {
        var 承認済み = await 承認済み予算のある課を保存("DEV-1", 1_000_000m);
        var 下書きのみ = await _fx.課を保存("DEV-2");
        var project = await _fx.案件を保存(下書きのみ.Id, "PJ-DRAFT");
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var draft = DepartmentBudget.CreateInitial(下書きのみ.Id, _fx.Half, "当初予算", _fx.Now);
        draft.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(5_000_000m));
        await repo.AddAsync(draft);
        var 予算なし = await _fx.課を保存("DEV-3");

        var totals = await repo.SumLatestApprovedByCategoryAsync(
            [承認済み.Id, 下書きのみ.Id, 予算なし.Id], _fx.Half);

        var only = Assert.Single(totals);
        Assert.Equal(承認済み.Id, only.DepartmentId);
    }

    [Fact]
    public async Task 予算合計は課ごとに最新の承認済みバージョンだけを使う()
    {
        var dept = await 承認済み予算のある課を保存("DEV-1", 1_000_000m);
        var other = await 承認済み予算のある課を保存("DEV-2", 3_000_000m);
        var repo = new DepartmentBudgetRepository(_fx.Factory);
        var project = (await new ProjectRepository(_fx.Factory).ListByDepartmentAsync(dept.Id))
            .Single();

        var v1 = (await repo.ListAsync(dept.Id, _fx.Half)).Single();
        var v2 = DepartmentBudget.ReviseFrom(v1, 2, "見直し", _fx.Now);
        v2.UpsertProjectLine(BudgetCategory.Revenue, project.Id, new Money(4_000_000m));
        v2.Approve(_fx.Now);
        await repo.AddAsync(v2);
        v1.Supersede();
        await repo.UpdateAsync(v1);

        var totals = await repo.SumLatestApprovedByCategoryAsync([dept.Id, other.Id], _fx.Half);

        // 旧バージョンの100万は混ざらない
        Assert.Equal(4_000_000m, 合計(totals, dept.Id, BudgetCategory.Revenue));
        Assert.Equal(3_000_000m, 合計(totals, other.Id, BudgetCategory.Revenue));
        // 別の半期は取得されない
        Assert.Empty(await repo.SumLatestApprovedByCategoryAsync([dept.Id, other.Id],
            new FiscalHalf(2026, HalfTerm.H2)));
    }

    [Fact]
    public async Task 承認済み予算を持つ課のIDだけを取得できる()
    {
        var 承認済み = await 承認済み予算のある課を保存("DEV-1", 1_000_000m);
        var 予算なし = await _fx.課を保存("DEV-2");
        var repo = new DepartmentBudgetRepository(_fx.Factory);

        var ids = await repo.ListDepartmentIdsWithApprovedAsync(
            [承認済み.Id, 予算なし.Id], _fx.Half);

        Assert.Equal([承認済み.Id], ids);
        Assert.Empty(await repo.ListDepartmentIdsWithApprovedAsync(
            [承認済み.Id, 予算なし.Id], new FiscalHalf(2026, HalfTerm.H2)));
    }

    [Fact]
    public async Task 複数課の実績合計を区分別にまとめて取得できる()
    {
        var dept1 = await _fx.課を保存("DEV-1");
        var dept2 = await _fx.課を保存("DEV-2");
        var project1 = await _fx.案件を保存(dept1.Id, "PJ-1");
        var project2 = await _fx.案件を保存(dept2.Id, "PJ-2");
        var repo = new ActualEntryRepository(_fx.Factory);

        // 同一区分に複数計上した実績は合算される
        await repo.AddAsync(ActualEntry.Record(dept1.Id, _fx.Half, BudgetCategory.Revenue,
            project1.Id, null, null, new Money(300_000m), null, _fx.Now));
        await repo.AddAsync(ActualEntry.Record(dept1.Id, _fx.Half, BudgetCategory.Revenue,
            project1.Id, null, null, new Money(200_000m), null, _fx.Now));
        await repo.AddAsync(ActualEntry.Record(dept2.Id, _fx.Half, BudgetCategory.PeriodCost,
            null, new CostElementCode("PERSONNEL"), null, new Money(120_000m), null, _fx.Now));
        // 別半期の実績は混ざらない
        await repo.AddAsync(ActualEntry.Record(dept1.Id, new FiscalHalf(2026, HalfTerm.H2),
            BudgetCategory.Revenue, project1.Id, null, null, new Money(999_000m), null, _fx.Now));

        var totals = await repo.SumByCategoryAsync([dept1.Id, dept2.Id], _fx.Half);

        Assert.Equal(500_000m, 合計(totals, dept1.Id, BudgetCategory.Revenue));
        Assert.Equal(120_000m, 合計(totals, dept2.Id, BudgetCategory.PeriodCost));
        Assert.Equal(2, totals.Count);
    }

    [Fact]
    public async Task 課を1件も指定しなければ空を返す()
    {
        var budgets = new DepartmentBudgetRepository(_fx.Factory);
        var actuals = new ActualEntryRepository(_fx.Factory);

        // IN () は SQL エラーになるため、DB に触れずに空を返すこと。
        Assert.Empty(await budgets.SumLatestApprovedByCategoryAsync([], _fx.Half));
        Assert.Empty(await budgets.ListDepartmentIdsWithApprovedAsync([], _fx.Half));
        Assert.Empty(await actuals.SumByCategoryAsync([], _fx.Half));
    }
}

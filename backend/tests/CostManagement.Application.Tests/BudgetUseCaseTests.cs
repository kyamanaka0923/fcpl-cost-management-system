using CostManagement.Application.Common;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 課予算の策定・改定・承認。</summary>
public class 課予算の策定と改定 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 課と案件を登録して予算を策定できる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id);

        var budget = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));
        await _fx.Budgets.UpsertLineAsync(budget.Id,
            new UpsertBudgetLineRequest("Revenue", project.Id, null, 5_000_000m));
        await _fx.Budgets.UpsertLineAsync(budget.Id,
            new UpsertBudgetLineRequest("Processing", project.Id, null, 2_000_000m));
        var updated = await _fx.Budgets.UpsertLineAsync(budget.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "PERSONNEL", 1_000_000m));

        Assert.Equal(5_000_000m, updated.RevenueTotal);
        Assert.Equal(2_000_000m, updated.ProcessingTotal);
        Assert.Equal(1_000_000m, updated.PeriodCostTotal);
        Assert.Equal(2_000_000m, updated.PlannedProfit);
        Assert.Equal(3, updated.Lines.Count);
    }

    [Fact]
    public async Task 策定中のドラフトがあると新しいドラフトは起票できない()
    {
        var dept = await _fx.部と課を作成();
        await _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "当初予算"));

        await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "重複ドラフト")));
    }

    [Fact]
    public async Task 半期が異なれば同じ課でも独立して予算を策定できる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id);
        await _fx.承認済み予算を作成(dept.Id, "2026-H1",
            ("Revenue", project.Id, null, 5_000_000m));

        var h2 = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H2", "下期当初予算"));

        Assert.Equal(1, h2.Version); // 半期ごとに独立したバージョン採番
        Assert.Empty(h2.Lines);      // 別半期の明細は引き継がれない
    }

    [Fact]
    public async Task 他の課の案件は明細に登録できない()
    {
        var dept1 = await _fx.部と課を作成("DEV-1", "開発1課");
        var dept2 = await _fx.部と課を作成("DEV-2", "開発2課");
        var otherProject = await _fx.案件を作成(dept2.Id, "PJ-OTHER", "他課の案件");

        var budget = await _fx.Budgets.CreateDraftAsync(dept1.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Budgets.UpsertLineAsync(budget.Id,
                new UpsertBudgetLineRequest("Revenue", otherProject.Id, null, 1_000_000m)));
        Assert.Contains("この課に属していません", ex.Message);
    }

    [Fact]
    public async Task 存在しない費目は期間費用に登録できない()
    {
        var dept = await _fx.部と課を作成();
        var budget = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Budgets.UpsertLineAsync(budget.Id,
                new UpsertBudgetLineRequest("PeriodCost", null, "UNKNOWN", 1_000_000m)));
    }

    [Fact]
    public async Task 改定版の承認で旧バージョンは失効する()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id);
        var v1 = await _fx.承認済み予算を作成(dept.Id, "2026-H1",
            ("Revenue", project.Id, null, 5_000_000m));

        var v2 = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H1", "下期見直し"));
        Assert.Equal(2, v2.Version);
        Assert.Single(v2.Lines); // 明細は最新承認版から引き継がれる

        await _fx.Budgets.ApproveAsync(v2.Id);

        var budgets = await _fx.Budgets.ListAsync(dept.Id, "2026-H1");
        Assert.Equal("Superseded", budgets.Single(b => b.Id == v1.Id).Status);
        Assert.Equal("Approved", budgets.Single(b => b.Id == v2.Id).Status);
    }

    [Fact]
    public async Task 費目マスタに費目を追加して期間費用に使える()
    {
        var dept = await _fx.部と課を作成();
        await _fx.CostElements.CreateAsync(new CreateCostElementRequest("TRAINING", "教育研修費"));

        var budget = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));
        var updated = await _fx.Budgets.UpsertLineAsync(budget.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "TRAINING", 300_000m));

        Assert.Equal(300_000m, updated.PeriodCostTotal);
    }

    [Fact]
    public async Task 実績は同一キーに複数計上できる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id);

        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Processing", project.Id, null, 400_000m, "4月分"));
        await _fx.Actuals.RecordAsync(dept.Id,
            new RecordActualRequest("2026-H1", "Processing", project.Id, null, 500_000m, "5月分"));

        var entries = await _fx.Actuals.ListAsync(dept.Id, "2026-H1");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task 他の課の案件に実績は計上できない()
    {
        var dept1 = await _fx.部と課を作成("DEV-1", "開発1課");
        var dept2 = await _fx.部と課を作成("DEV-2", "開発2課");
        var otherProject = await _fx.案件を作成(dept2.Id, "PJ-OTHER", "他課の案件");

        await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Actuals.RecordAsync(dept1.Id,
                new RecordActualRequest("2026-H1", "Revenue", otherProject.Id, null, 100_000m, null)));
    }

    [Fact]
    public async Task 課コードは重複できず同一課では案件コードも重複できない()
    {
        var div = await _fx.部を作成();
        var dept = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        await _fx.案件を作成(dept.Id, "PJ-001", "案件A");

        await Assert.ThrowsAsync<DomainException>(() => _fx.課を作成(div.Id, "DEV-1", "別の課"));
        await Assert.ThrowsAsync<DomainException>(() => _fx.案件を作成(dept.Id, "PJ-001", "別の案件"));
    }

    [Fact]
    public async Task 案件コードは課ごとに一意で別の課では同じコードを使える()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");

        await _fx.案件を作成(課1.Id, "PJ-001", "課1の案件");
        // 別の課なら同じ案件コードを登録できる
        var 課2案件 = await _fx.案件を作成(課2.Id, "PJ-001", "課2の案件");
        Assert.Equal("PJ-001", 課2案件.Code);

        // 同じ課で同じコードは不可
        await Assert.ThrowsAsync<DomainException>(() => _fx.案件を作成(課1.Id, "PJ-001", "課1の別案件"));
    }

    [Fact]
    public async Task 案件のコードと名称を後から編集できる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id, "PJ-001", "受託開発A");

        var updated = await _fx.Projects.UpdateAsync(project.Id,
            new UpdateProjectRequest("PJ-100", "受託開発A（改称）"));

        Assert.Equal("PJ-100", updated.Code);
        Assert.Equal("受託開発A（改称）", updated.Name);

        // 取得し直しても反映されている
        var reloaded = await _fx.Projects.GetAsync(project.Id);
        Assert.Equal("PJ-100", reloaded.Code);
    }

    [Fact]
    public async Task 案件コードを変更しても予算明細の案件参照は保たれる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id, "PJ-001", "受託開発A");
        var budget = await _fx.Budgets.CreateDraftAsync(dept.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));
        await _fx.Budgets.UpsertLineAsync(budget.Id,
            new UpsertBudgetLineRequest("Revenue", project.Id, null, 5_000_000m));

        // 案件コードを変更(参照は案件Id=GUIDのため明細は壊れない)
        await _fx.Projects.UpdateAsync(project.Id, new UpdateProjectRequest("PJ-999", "受託開発A"));

        var reloaded = await _fx.Budgets.GetAsync(budget.Id);
        var line = Assert.Single(reloaded.Lines);
        Assert.Equal(project.Id, line.ProjectId);
        Assert.Equal(5_000_000m, reloaded.RevenueTotal);
    }

    [Fact]
    public async Task 編集で同一課の別案件とコードが重複するとエラーになる()
    {
        var dept = await _fx.部と課を作成();
        await _fx.案件を作成(dept.Id, "PJ-001", "案件A");
        var b = await _fx.案件を作成(dept.Id, "PJ-002", "案件B");

        await Assert.ThrowsAsync<DomainException>(() =>
            _fx.Projects.UpdateAsync(b.Id, new UpdateProjectRequest("PJ-001", "案件B")));
    }

    [Fact]
    public async Task 編集では別の課の案件と同じコードに変更できる()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        await _fx.案件を作成(課1.Id, "PJ-001", "課1の案件");
        var 課2案件 = await _fx.案件を作成(課2.Id, "PJ-XXX", "課2の案件");

        // 別の課なら同じコードに変更できる
        var updated = await _fx.Projects.UpdateAsync(課2案件.Id,
            new UpdateProjectRequest("PJ-001", "課2の案件"));
        Assert.Equal("PJ-001", updated.Code);
    }

    [Fact]
    public async Task 同じ案件を同じコードのまま名称だけ編集できる()
    {
        var dept = await _fx.部と課を作成();
        var project = await _fx.案件を作成(dept.Id, "PJ-001", "旧名称");

        // 自分自身のコードは重複扱いにならない
        var updated = await _fx.Projects.UpdateAsync(project.Id,
            new UpdateProjectRequest("PJ-001", "新名称"));
        Assert.Equal("PJ-001", updated.Code);
        Assert.Equal("新名称", updated.Name);
    }
}

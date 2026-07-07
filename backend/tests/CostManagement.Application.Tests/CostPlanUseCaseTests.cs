using CostManagement.Application.Common;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 原価予算の策定・改定・承認。</summary>
public class 原価予算の策定と改定 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 最初の予算は当初予算としてバージョン1のドラフトになる()
    {
        var project = await _fx.プロジェクトを作成();

        var plan = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("当初原価予算", null));

        Assert.Equal(1, plan.Version);
        Assert.Equal("Draft", plan.Status);
        Assert.Empty(plan.Lines);
    }

    [Fact]
    public async Task 存在しないプロジェクトには予算を作成できない()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.CostPlans.CreateDraftAsync(Guid.NewGuid(),
                new CreatePlanRequest("当初原価予算", null)));
    }

    [Fact]
    public async Task 策定中のドラフトがあるうちは新しい改定版を作成できない()
    {
        var project = await _fx.プロジェクトを作成();
        await _fx.CostPlans.CreateDraftAsync(project.Id, new CreatePlanRequest("当初原価予算", null));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _fx.CostPlans.CreateDraftAsync(project.Id, new CreatePlanRequest("二重ドラフト", null)));
        Assert.Contains("ドラフト", ex.Message);
    }

    [Fact]
    public async Task 存在しない費目では明細を登録できない()
    {
        var project = await _fx.プロジェクトを作成();
        var plan = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("当初原価予算", null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.CostPlans.UpsertLineAsync(plan.Id,
                new UpsertPlanLineRequest("存在しない費目", "案件A", "2026-04", 100_000m)));
    }

    [Fact]
    public async Task 改定版を承認すると旧バージョンは自動的に失効する()
    {
        var project = await _fx.プロジェクトを作成();
        var v1 = await _fx.承認済み原価予算を作成(project.Id,
            ("LAB-SE", "案件A", "2026-04", 500_000m));

        // 四半期改定: 明細を引き継いだ v2 ドラフトを作成し、金額を変更して承認
        var v2 = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("第2四半期改定", null));
        Assert.Equal(2, v2.Version);
        Assert.Single(v2.Lines); // v1 の明細を引き継いでいる

        await _fx.CostPlans.UpsertLineAsync(v2.Id,
            new UpsertPlanLineRequest("LAB-SE", "案件A", "2026-04", 550_000m));
        await _fx.CostPlans.ApproveAsync(v2.Id);

        var plans = await _fx.CostPlans.ListByProjectAsync(project.Id);
        Assert.Equal("Approved", plans.Single(p => p.Version == 2).Status);
        Assert.Equal("Superseded", plans.Single(p => p.Version == 1).Status);
    }

    [Fact]
    public async Task 指定した旧バージョンを基に改定版を作成できる()
    {
        var project = await _fx.プロジェクトを作成();
        var v1 = await _fx.承認済み原価予算を作成(project.Id,
            ("LAB-SE", "案件A", "2026-04", 500_000m));

        var v2 = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("v1を基に改定", v1.Id));

        Assert.Equal(2, v2.Version);
        Assert.Equal(500_000m, v2.Lines.Single().Amount);
    }

    [Fact]
    public async Task 明細の削除と上書きができる()
    {
        var project = await _fx.プロジェクトを作成();
        var plan = await _fx.CostPlans.CreateDraftAsync(project.Id,
            new CreatePlanRequest("当初原価予算", null));

        await _fx.CostPlans.UpsertLineAsync(plan.Id,
            new UpsertPlanLineRequest("LAB-SE", "案件A", "2026-04", 100_000m));
        await _fx.CostPlans.UpsertLineAsync(plan.Id,
            new UpsertPlanLineRequest("LAB-SE", "案件A", "2026-04", 120_000m)); // 上書き
        await _fx.CostPlans.UpsertLineAsync(plan.Id,
            new UpsertPlanLineRequest("LAB-SE", null, "2026-04", 30_000m)); // 共通費は別明細

        var updated = await _fx.CostPlans.RemoveLineAsync(plan.Id, "LAB-SE", null, "2026-04");

        var line = Assert.Single(updated.Lines);
        Assert.Equal(120_000m, line.Amount);
        Assert.Equal("案件A", line.RevenueItem);
    }
}

/// <summary>ユースケース: プロジェクトと費目マスタ。</summary>
public class プロジェクトと費目マスタ : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task プロジェクトを作成して一覧と詳細で参照できる()
    {
        var created = await _fx.プロジェクトを作成("PJ-100", "基幹システム刷新");

        var listed = await _fx.Projects.ListAsync();
        var fetched = await _fx.Projects.GetAsync(created.Id);

        Assert.Contains(listed, p => p.Code == "PJ-100");
        Assert.Equal("基幹システム刷新", fetched.Name);
        Assert.Equal("Active", fetched.Status);
    }

    [Fact]
    public async Task プロジェクトコードは重複できない()
    {
        await _fx.プロジェクトを作成("PJ-100");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _fx.プロジェクトを作成("PJ-100"));
        Assert.Contains("既に使用", ex.Message);
    }

    [Fact]
    public async Task 標準費目がシードされている()
    {
        var elements = await _fx.CostElements.ListAsync();

        Assert.Contains(elements, e => e.Code == "LAB-SE" && e.Type == "Labor");
        Assert.Contains(elements, e => e.Code == "SUB-DEV" && e.Type == "Expense");
    }

    [Fact]
    public async Task 費目を追加できるがコード重複と不正な分類は拒否される()
    {
        var added = await _fx.CostElements.CreateAsync(
            new CreateCostElementRequest("EXP-EDU", "教育研修費", "Expense"));
        Assert.Equal("EXP-EDU", added.Code);

        await Assert.ThrowsAsync<DomainException>(() => _fx.CostElements.CreateAsync(
            new CreateCostElementRequest("EXP-EDU", "重複コード", "Expense")));
        await Assert.ThrowsAsync<DomainException>(() => _fx.CostElements.CreateAsync(
            new CreateCostElementRequest("XX-1", "不正な分類", "Unknown")));
    }
}

/// <summary>ユースケース: 実績の計上。</summary>
public class 実績の計上 : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 原価実績を計上し一覧で参照し削除できる()
    {
        var project = await _fx.プロジェクトを作成();

        var actual = await _fx.ActualCosts.RecordAsync(project.Id,
            new RecordActualRequest("LAB-SE", "案件A", "2026-04", 480_000m, "SE 0.8人月"));
        var listed = await _fx.ActualCosts.ListByProjectAsync(project.Id);

        Assert.Single(listed);
        Assert.Equal(480_000m, listed[0].Amount);
        Assert.Equal("SE 0.8人月", listed[0].Note);

        await _fx.ActualCosts.DeleteAsync(actual.Id);
        Assert.Empty(await _fx.ActualCosts.ListByProjectAsync(project.Id));
    }

    [Fact]
    public async Task 売上実績を計上し一覧で参照し削除できる()
    {
        var project = await _fx.プロジェクトを作成();

        var actual = await _fx.ActualRevenues.RecordAsync(project.Id,
            new RecordRevenueRequest("案件A", "2026-04", 2_000_000m, "検収"));
        var listed = await _fx.ActualRevenues.ListByProjectAsync(project.Id);

        Assert.Single(listed);
        Assert.Equal(2_000_000m, listed[0].Amount);

        await _fx.ActualRevenues.DeleteAsync(actual.Id);
        Assert.Empty(await _fx.ActualRevenues.ListByProjectAsync(project.Id));
    }

    [Fact]
    public async Task 存在しないプロジェクトや費目への計上は拒否される()
    {
        var project = await _fx.プロジェクトを作成();

        await Assert.ThrowsAsync<NotFoundException>(() => _fx.ActualCosts.RecordAsync(
            Guid.NewGuid(), new RecordActualRequest("LAB-SE", null, "2026-04", 1m, null)));
        await Assert.ThrowsAsync<NotFoundException>(() => _fx.ActualCosts.RecordAsync(
            project.Id, new RecordActualRequest("存在しない費目", null, "2026-04", 1m, null)));
    }
}

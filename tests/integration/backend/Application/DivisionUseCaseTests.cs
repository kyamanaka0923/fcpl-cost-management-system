using CostManagement.Application.Common;
using CostManagement.Domain.Shared;

namespace CostManagement.Application.Tests;

/// <summary>ユースケース: 部の予実サマリ(配下課の合計)。</summary>
public class 部の予実サマリ : IDisposable
{
    private readonly UseCaseFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task 配下課の予実を合計して部サマリを返す()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        var pj2 = await _fx.案件を作成(課2.Id, "PJ-2", "案件2");

        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m),
            ("Processing", pj1.Id, null, 1_000_000m),
            ("PeriodCost", null, "PERSONNEL", 300_000m));
        await _fx.承認済み予算を作成(課2.Id, "2026-H1",
            ("Revenue", pj2.Id, null, 2_000_000m),
            ("Outsourcing", pj2.Id, null, 800_000m));

        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj1.Id, null, 3_200_000m, null));
        await _fx.Actuals.RecordAsync(課2.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj2.Id, null, 1_800_000m, null));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(5_000_000m, summary.PlannedRevenue);
        Assert.Equal(5_000_000m, summary.ActualRevenue);    // 320万 + 180万
        Assert.Equal(2_100_000m, summary.PlannedCost);      // (100+30) + 80 万
        Assert.Equal(2_900_000m, summary.PlannedProfit);    // 500万 − 210万

        Assert.Equal(2, summary.DepartmentLines.Count);
        Assert.All(summary.DepartmentLines, l => Assert.True(l.HasApprovedBudget));
        // 部合計 = 課別内訳の合計
        Assert.Equal(summary.PlannedRevenue, summary.DepartmentLines.Sum(l => l.PlannedRevenue));
        Assert.Equal(summary.ActualProfit, summary.DepartmentLines.Sum(l => l.ActualProfit));
    }

    [Fact]
    public async Task 課別内訳には区分別の予算内訳が含まれる()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m),
            ("Processing", pj1.Id, null, 1_000_000m),
            ("Outsourcing", pj1.Id, null, 500_000m),
            ("PeriodCost", null, "PERSONNEL", 300_000m));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        var line = summary.DepartmentLines.Single(l => l.DepartmentCode == "DEV-1");
        Assert.Equal(4, line.Categories.Count); // 4区分すべて
        Assert.Equal(1_000_000m,
            line.Categories.Single(c => c.Category == "Processing").PlannedAmount);
        Assert.Equal(500_000m,
            line.Categories.Single(c => c.Category == "Outsourcing").PlannedAmount);
        Assert.Equal(300_000m,
            line.Categories.Single(c => c.Category == "PeriodCost").PlannedAmount);
        // 区分別内訳の合計 = その課のコスト予算
        var costCategories = line.Categories.Where(c => c.Category != "Revenue");
        Assert.Equal(line.PlannedCost, costCategories.Sum(c => c.PlannedAmount));
    }

    [Fact]
    public async Task 未策定の課の区分別内訳は空になる()
    {
        var div = await _fx.部を作成();
        _ = await _fx.課を作成(div.Id, "DEV-1", "開発1課"); // 予算未策定

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Empty(summary.DepartmentLines.Single().Categories);
    }

    [Fact]
    public async Task 承認済み予算のない課は未策定として内訳に出て合計に含まれない()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        _ = await _fx.課を作成(div.Id, "DEV-2", "開発2課"); // 予算未策定
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(3_000_000m, summary.PlannedRevenue); // 課1のみ
        Assert.Equal(2, summary.DepartmentLines.Count);
        var 未策定 = summary.DepartmentLines.Single(l => l.DepartmentCode == "DEV-2");
        Assert.False(未策定.HasApprovedBudget);
        Assert.Equal(0m, 未策定.PlannedRevenue);
    }

    [Fact]
    public async Task 部サマリの数字は課ごとの予実差異分析と一致する()
    {
        // 部サマリは明細を読まずに (課 × 区分) の合計だけを引く経路を通る。
        // 明細まで突き合わせる予実差異分析(AnalyzeVarianceAsync)と数字がずれていないことを固定する。
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        var pj1b = await _fx.案件を作成(課1.Id, "PJ-1B", "案件1B");
        var pj2 = await _fx.案件を作成(課2.Id, "PJ-2", "案件2");

        // 課1: 月次入力の明細・期間費用の明細名・複数案件を混在させる。
        var 予算1 = await _fx.Budgets.CreateDraftAsync(課1.Id,
            new CreateBudgetRequest("2026-H1", "当初予算"));
        await _fx.Budgets.UpsertLineAsync(予算1.Id,
            new UpsertBudgetLineRequest("Revenue", pj1.Id, null, 0m,
                new Dictionary<int, decimal> { [1] = 500_000m, [2] = 700_000m, [3] = 300_000m }));
        await _fx.Budgets.UpsertLineAsync(予算1.Id,
            new UpsertBudgetLineRequest("Revenue", pj1b.Id, null, 900_000m));
        await _fx.Budgets.UpsertLineAsync(予算1.Id,
            new UpsertBudgetLineRequest("Processing", pj1.Id, null, 600_000m));
        await _fx.Budgets.UpsertLineAsync(予算1.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "PERSONNEL", 400_000m,
                PeriodDetail: "正社員"));
        await _fx.Budgets.UpsertLineAsync(予算1.Id,
            new UpsertBudgetLineRequest("PeriodCost", null, "PERSONNEL", 150_000m,
                PeriodDetail: "派遣"));
        await _fx.Budgets.ApproveAsync(予算1.Id);

        // 課2 は当初予算を承認したうえで改定版も承認する(最新版だけが集計対象)。
        await _fx.承認済み予算を作成(課2.Id, "2026-H1",
            ("Revenue", pj2.Id, null, 1_000_000m),
            ("Outsourcing", pj2.Id, null, 500_000m));
        var 改定 = await _fx.Budgets.CreateDraftAsync(課2.Id,
            new CreateBudgetRequest("2026-H1", "下期見直し"));
        await _fx.Budgets.UpsertLineAsync(改定.Id,
            new UpsertBudgetLineRequest("Revenue", pj2.Id, null, 2_200_000m));
        await _fx.Budgets.ApproveAsync(改定.Id);

        // 実績: 同一キーの複数計上、計上月あり、予算にない案件(予定外)を含む。
        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj1.Id, null, 800_000m, Month: 1));
        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj1.Id, null, 400_000m, Month: 2));
        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "Revenue", pj1b.Id, null, 100_000m));
        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "PeriodCost", null, "PERSONNEL", 380_000m,
                PeriodDetail: "正社員"));
        // 計画にない明細名(予定外)も合計に入る
        await _fx.Actuals.RecordAsync(課1.Id,
            new RecordActualRequest("2026-H1", "PeriodCost", null, "PERSONNEL", 70_000m,
                PeriodDetail: "業務委託"));
        await _fx.Actuals.RecordAsync(課2.Id,
            new RecordActualRequest("2026-H1", "Outsourcing", pj2.Id, null, 620_000m));

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        decimal 部予算売上 = 0m, 部実績売上 = 0m, 部予算コスト = 0m, 部実績コスト = 0m;
        foreach (var 課 in new[] { 課1, 課2 })
        {
            var variance = await _fx.Analysis.AnalyzeVarianceAsync(課.Id, "2026-H1", null);
            var line = summary.DepartmentLines.Single(l => l.DepartmentId == 課.Id);

            Assert.Equal(variance.PlannedRevenue, line.PlannedRevenue);
            Assert.Equal(variance.ActualRevenue, line.ActualRevenue);
            Assert.Equal(variance.PlannedCost, line.PlannedCost);
            Assert.Equal(variance.ActualCost, line.ActualCost);
            foreach (var c in variance.Categories)
            {
                var 内訳 = line.Categories.Single(x => x.Category == c.Category);
                Assert.Equal(c.PlannedAmount, 内訳.PlannedAmount);
                Assert.Equal(c.ActualAmount, 内訳.ActualAmount);
                Assert.Equal(c.Variance, 内訳.Variance);
            }

            部予算売上 += variance.PlannedRevenue;
            部実績売上 += variance.ActualRevenue;
            部予算コスト += variance.PlannedCost;
            部実績コスト += variance.ActualCost;
        }

        Assert.Equal(部予算売上, summary.PlannedRevenue);
        Assert.Equal(部実績売上, summary.ActualRevenue);
        Assert.Equal(部予算コスト, summary.PlannedCost);
        Assert.Equal(部実績コスト, summary.ActualCost);
        Assert.Equal(部予算売上 - 部予算コスト, summary.PlannedProfit);
        Assert.Equal(部実績売上 - 部実績コスト, summary.ActualProfit);
    }

    [Fact]
    public async Task 配下課がない部のサマリは全て0になる()
    {
        var div = await _fx.部を作成();

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Empty(summary.DepartmentLines);
        Assert.Equal(0m, summary.PlannedRevenue);
        Assert.Equal(0m, summary.ActualProfit);
        Assert.All(summary.Categories, c => Assert.Equal(0m, c.PlannedAmount));
        Assert.False(summary.CanApprove); // 課が1件もなければ部承認はできない
    }

    [Fact]
    public async Task 部サマリは課ごとの最新の承認済み改定版を集計する()
    {
        var div = await _fx.部を作成();
        var 課1 = await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        var 課2 = await _fx.課を作成(div.Id, "DEV-2", "開発2課");
        var pj1 = await _fx.案件を作成(課1.Id, "PJ-1", "案件1");
        var pj2 = await _fx.案件を作成(課2.Id, "PJ-2", "案件2");

        await _fx.承認済み予算を作成(課1.Id, "2026-H1",
            ("Revenue", pj1.Id, null, 3_000_000m));
        await _fx.承認済み予算を作成(課2.Id, "2026-H1",
            ("Revenue", pj2.Id, null, 2_000_000m));

        // 課1 だけ改定版を起票して承認する(課2 は当初予算のまま)。
        var v2 = await _fx.Budgets.CreateDraftAsync(課1.Id,
            new CreateBudgetRequest("2026-H1", "下期見直し"));
        await _fx.Budgets.UpsertLineAsync(v2.Id,
            new UpsertBudgetLineRequest("Revenue", pj1.Id, null, 4_000_000m));
        await _fx.Budgets.ApproveAsync(v2.Id);

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(6_000_000m, summary.PlannedRevenue); // 改定後400万 + 200万
        Assert.Equal(4_000_000m,
            summary.DepartmentLines.Single(l => l.DepartmentCode == "DEV-1").PlannedRevenue);
    }

    [Fact]
    public async Task 課別内訳は課コード順に並ぶ()
    {
        var div = await _fx.部を作成();
        // 登録順とコード順が違う状態にする。
        await _fx.課を作成(div.Id, "DEV-3", "開発3課");
        await _fx.課を作成(div.Id, "DEV-1", "開発1課");
        await _fx.課を作成(div.Id, "DEV-2", "開発2課");

        var summary = await _fx.Analysis.GetDivisionBudgetSummaryAsync(div.Id, "2026-H1");

        Assert.Equal(["DEV-1", "DEV-2", "DEV-3"],
            summary.DepartmentLines.Select(l => l.DepartmentCode));
    }

    [Fact]
    public async Task 存在しない部のサマリはエラーになる()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _fx.Analysis.GetDivisionBudgetSummaryAsync(Guid.NewGuid(), "2026-H1"));
    }

    [Fact]
    public async Task 部コードは重複できない()
    {
        await _fx.部を作成("SALES", "営業本部");
        await Assert.ThrowsAsync<DomainException>(() => _fx.部を作成("SALES", "別の部"));
    }
}

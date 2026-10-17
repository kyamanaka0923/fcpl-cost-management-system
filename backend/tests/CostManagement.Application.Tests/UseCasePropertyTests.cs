using CostManagement.Domain.Shared;
using CsCheck;

namespace CostManagement.Application.Tests;

/// <summary>
/// ユースケース(アプリケーションサービス)のプロパティベーステスト。
/// 試行ごとに独立した一時 SQLite の <see cref="UseCaseFixture"/> を作り、任意の操作列を
/// 本物のリポジトリ経由で実行して、永続化を挟んでも集計の整合性が保たれることを確かめる。
/// DB を作るぶん重いので、ドメインの性質テストより試行回数を絞っている。
/// </summary>
public class ユースケースの性質
{
    private const int 試行回数 = 25;

    private static readonly string[] 案件別区分 = ["Revenue", "Processing", "Outsourcing"];
    private static readonly string[] 費目 = ["PERSONNEL", "LICENSE"];
    private static readonly string?[] 明細名 = [null, "AWS", "GitHub"];

    private static readonly Gen<decimal> 金額 = Gen.Long[0, 1_000_000_000L].Select(v => (decimal)v);

    private static readonly Gen<Dictionary<int, decimal>> 月別金額 =
        Gen.Select(Gen.Int[1, 6], 金額).List[1, 6]
            .Select(pairs => pairs.GroupBy(p => p.Item1).ToDictionary(g => g.Key, g => g.Last().Item2));

    /// <summary>予算明細の登録・削除リクエスト(案件は課内の案件の添字で指定)。</summary>
    private sealed record 明細操作(bool 削除, string 区分, int 案件添字, string? 費目, string? 明細名,
        decimal 金額, Dictionary<int, decimal>? 月別)
    {
        public override string ToString() =>
            $"{(削除 ? "削除" : "登録")} {区分} 案件{案件添字} {費目} {明細名} {金額} " +
            (月別 is null ? "" : "{" + string.Join(",", 月別.Select(kv => $"{kv.Key}={kv.Value}")) + "}");
    }

    private static readonly Gen<明細操作> 任意の明細操作 = Gen.Frequency(
        (3, Gen.Select(Gen.OneOfConst(案件別区分), Gen.Int[0, 2], 金額, 月別金額.Null(0.6))
            .Select((c, p, a, m) => new 明細操作(false, c, p, null, null, a, m))),
        (3, Gen.Select(Gen.OneOfConst(費目), Gen.OneOfConst(明細名), 金額, 月別金額.Null(0.6))
            .Select((e, d, a, m) => new 明細操作(false, "PeriodCost", 0, e, d, a, m))),
        (1, Gen.Select(Gen.OneOfConst(案件別区分), Gen.Int[0, 2])
            .Select((c, p) => new 明細操作(true, c, p, null, null, 0m, null))),
        (1, Gen.Select(Gen.OneOfConst(費目), Gen.OneOfConst(明細名))
            .Select((e, d) => new 明細操作(true, "PeriodCost", 0, e, d, 0m, null))));

    /// <summary>実績計上リクエスト(案件は課内の案件の添字で指定)。</summary>
    private static readonly Gen<(string 区分, int 案件添字, string? 費目, string? 明細名, decimal 金額, int? 月)> 任意の実績 =
        Gen.Frequency(
            (3, Gen.Select(Gen.OneOfConst(案件別区分), Gen.Int[0, 2], 金額, Gen.Int[1, 6].Nullable(0.5))
                .Select((c, p, a, m) => (c, p, (string?)null, (string?)null, a, m))),
            (1, Gen.Select(Gen.OneOfConst(費目), Gen.OneOfConst(明細名), 金額, Gen.Int[1, 6].Nullable(0.5))
                .Select((e, d, a, m) => ("PeriodCost", 0, (string?)e, d, a, m))));

    private static async Task<(DepartmentDto 課, List<Guid> 案件)> 案件つきの課を準備(UseCaseFixture fx,
        Guid? divisionId = null, string code = "DEV-1")
    {
        var dept = divisionId is { } div
            ? await fx.課を作成(div, code, $"{code}課")
            : await fx.部と課を作成(code, $"{code}課");
        var projects = new List<Guid>();
        for (var i = 0; i < 3; i++)
            projects.Add((await fx.案件を作成(dept.Id, $"PJ-{i}", $"案件{i}")).Id);
        return (dept, projects);
    }

    private static async Task<BudgetDetailDto?> 明細操作を実行(UseCaseFixture fx, Guid budgetId,
        List<Guid> projects, 明細操作 op)
    {
        var projectId = op.区分 == "PeriodCost" ? (Guid?)null : projects[op.案件添字];
        try
        {
            return op.削除
                ? await fx.Budgets.RemoveLineAsync(budgetId, op.区分, projectId, op.費目, op.明細名)
                : await fx.Budgets.UpsertLineAsync(budgetId,
                    new UpsertBudgetLineRequest(op.区分, projectId, op.費目, op.金額, op.月別, op.明細名));
        }
        catch (DomainException)
        {
            return null; // 存在しない明細の削除・費目一括と明細の混在などは仕様どおり拒否される
        }
    }

    private static void 区分合計が明細合計と一致する(BudgetDetailDto dto)
    {
        decimal Sum(string category) => dto.Lines.Where(l => l.Category == category).Sum(l => l.Amount);
        Assert.Equal(Sum("Revenue"), dto.RevenueTotal);
        Assert.Equal(Sum("Processing"), dto.ProcessingTotal);
        Assert.Equal(Sum("Outsourcing"), dto.OutsourcingTotal);
        Assert.Equal(Sum("PeriodCost"), dto.PeriodCostTotal);
        Assert.Equal(dto.RevenueTotal - dto.ProcessingTotal - dto.OutsourcingTotal - dto.PeriodCostTotal,
            dto.PlannedProfit);
        Assert.All(dto.Lines.Where(l => l.IsMonthly),
            l => Assert.Equal(l.MonthlyAmounts.Values.Sum(), l.Amount));
    }

    private static string 明細の要約(BudgetDetailDto dto) => string.Join(" | ", dto.Lines.Select(l =>
        $"{l.Id}:{l.Category}:{l.ProjectId}:{l.ElementCode}:{l.PeriodDetail}:{数値(l.Amount)}:" +
        string.Join(",", l.MonthlyAmounts.OrderBy(m => m.Key).Select(m => $"{m.Key}={数値(m.Value)}"))));

    /// <summary>
    /// 金額を数値として比較できる表記にする。SQLite へは decimal が "0.0###" 形式の TEXT で
    /// 保存されるため、読み戻すと値は同じでも小数部の桁数(scale)が変わることがある。
    /// </summary>
    private static string 数値(decimal value) =>
        value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task 任意の明細操作の後も保存した予算は応答と同じ内容で読み戻せ区分合計は明細合計と一致する()
    {
        await 任意の明細操作.List[1, 15].SampleAsync(async ops =>
        {
            using var fx = new UseCaseFixture();
            var (dept, projects) = await 案件つきの課を準備(fx);
            var draft = await fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "当初予算"));

            var last = draft;
            foreach (var op in ops)
            {
                var result = await 明細操作を実行(fx, draft.Id, projects, op);
                var reloaded = await fx.Budgets.GetAsync(draft.Id);

                // 成功なら応答どおりに保存され、拒否なら直前の状態のまま
                Assert.Equal(明細の要約(result ?? last), 明細の要約(reloaded));
                区分合計が明細合計と一致する(reloaded);
                last = reloaded;
            }
        }, iter: 試行回数);
    }

    [Fact]
    public async Task 承認と改定を何度繰り返しても承認済みの版は常に最新の承認版1件だけになる()
    {
        // 各回: ドラフトを起票(2回目以降は改定版) → 金額を変更 → 承認
        await Gen.Select(金額, 金額).List[1, 5].SampleAsync(async rounds =>
        {
            using var fx = new UseCaseFixture();
            var (dept, projects) = await 案件つきの課を準備(fx);

            var approvedVersions = new List<int>();
            foreach (var (revenue, cost) in rounds)
            {
                var draft = await fx.Budgets.CreateDraftAsync(dept.Id,
                    new CreateBudgetRequest("2026-H1", $"第{approvedVersions.Count + 1}版"));
                await fx.Budgets.UpsertLineAsync(draft.Id,
                    new UpsertBudgetLineRequest("Revenue", projects[0], null, revenue));
                await fx.Budgets.UpsertLineAsync(draft.Id,
                    new UpsertBudgetLineRequest("PeriodCost", null, "PERSONNEL", cost));
                var approved = await fx.Budgets.ApproveAsync(draft.Id);
                approvedVersions.Add(approved.Version);

                var versions = await fx.Budgets.ListAsync(dept.Id, "2026-H1");
                var current = Assert.Single(versions, v => v.Status == "Approved");
                Assert.Equal(approved.Version, current.Version);
                Assert.All(versions.Where(v => v.Id != current.Id), v => Assert.Equal("Superseded", v.Status));

                // 差異分析の基準は常に最新の承認版
                var variance = await fx.Analysis.AnalyzeVarianceAsync(dept.Id, "2026-H1", null);
                Assert.Equal(approved.Version, variance.BudgetVersion);
                Assert.Equal(revenue, variance.PlannedRevenue);
                Assert.Equal(cost, variance.PlannedCost);
            }
            Assert.Equal(Enumerable.Range(1, rounds.Count), approvedVersions);
        }, iter: 試行回数);
    }

    [Fact]
    public async Task 計上した実績は差異分析と損益の実績合計にそのまま反映される()
    {
        await Gen.Select(任意の明細操作.List[1, 10], 任意の実績.List[0, 12]).SampleAsync(async (ops, actuals) =>
        {
            using var fx = new UseCaseFixture();
            var (dept, projects) = await 案件つきの課を準備(fx);
            var draft = await fx.Budgets.CreateDraftAsync(dept.Id, new CreateBudgetRequest("2026-H1", "当初予算"));
            foreach (var op in ops)
                await 明細操作を実行(fx, draft.Id, projects, op);
            // 明細なしでは承認できないため、操作の結果が空なら1件だけ補う
            if ((await fx.Budgets.GetAsync(draft.Id)).Lines.Count == 0)
                await fx.Budgets.UpsertLineAsync(draft.Id, new UpsertBudgetLineRequest("Revenue", projects[0], null, 1m));
            var budget = await fx.Budgets.ApproveAsync(draft.Id);

            foreach (var (category, p, element, detail, amount, month) in actuals)
                await fx.Actuals.RecordAsync(dept.Id, new RecordActualRequest("2026-H1", category,
                    category == "PeriodCost" ? null : projects[p], element, amount, month, null, detail));

            var variance = await fx.Analysis.AnalyzeVarianceAsync(dept.Id, "2026-H1", null);
            var profit = await fx.Analysis.GetProfitSummaryAsync(dept.Id, "2026-H1", null);

            var actualRevenue = actuals.Where(a => a.区分 == "Revenue").Sum(a => a.金額);
            var actualCost = actuals.Where(a => a.区分 != "Revenue").Sum(a => a.金額);
            var actualPeriod = actuals.Where(a => a.区分 == "PeriodCost").Sum(a => a.金額);

            Assert.Equal(actualRevenue, variance.ActualRevenue);
            Assert.Equal(actualCost, variance.ActualCost);
            Assert.Equal(budget.RevenueTotal, variance.PlannedRevenue);
            Assert.Equal(budget.ProcessingTotal + budget.OutsourcingTotal + budget.PeriodCostTotal, variance.PlannedCost);
            Assert.All(variance.Categories, c => Assert.Equal(c.Lines.Sum(l => l.ActualAmount), c.ActualAmount));

            // 損益: 案件別損益の合計 − 期間費用 = 課全体の損益
            Assert.Equal(actualRevenue - actualCost, profit.ActualProfit);
            Assert.Equal(budget.PlannedProfit, profit.PlannedProfit);
            Assert.Equal(actualPeriod, profit.ActualPeriodCost);
            Assert.Equal(profit.ActualProfit, profit.ProjectLines.Sum(l => l.ActualProfit) - profit.ActualPeriodCost);
            Assert.Equal(profit.PlannedProfit, profit.ProjectLines.Sum(l => l.PlannedProfit) - profit.PlannedPeriodCost);
        }, iter: 試行回数);
    }

    [Fact]
    public async Task 部合計は承認済み予算を持つ課の内訳の合計で未策定の課は含まれない()
    {
        // 課ごとに (承認済み予算を持つか, 売上予算, 期間費用予算, 売上実績, 期間費用実績)
        var 課の状況 = Gen.Select(Gen.Bool, 金額, 金額, 金額, 金額);

        await 課の状況.List[1, 4].SampleAsync(async departments =>
        {
            using var fx = new UseCaseFixture();
            var division = await fx.部を作成();

            for (var i = 0; i < departments.Count; i++)
            {
                var (approved, plannedRevenue, plannedCost, actualRevenue, actualCost) = departments[i];
                var (dept, projects) = await 案件つきの課を準備(fx, division.Id, $"DEV-{i}");
                if (approved)
                    await fx.承認済み予算を作成(dept.Id, "2026-H1",
                        ("Revenue", projects[0], null, plannedRevenue),
                        ("PeriodCost", null, "PERSONNEL", plannedCost));
                await fx.Actuals.RecordAsync(dept.Id,
                    new RecordActualRequest("2026-H1", "Revenue", projects[1], null, actualRevenue));
                await fx.Actuals.RecordAsync(dept.Id,
                    new RecordActualRequest("2026-H1", "PeriodCost", null, "LICENSE", actualCost));
            }

            var summary = await fx.Analysis.GetDivisionBudgetSummaryAsync(division.Id, "2026-H1");
            var included = summary.DepartmentLines.Where(l => l.HasApprovedBudget).ToList();

            Assert.Equal(departments.Count, summary.DepartmentLines.Count);
            Assert.Equal(departments.Count(d => d.Item1), included.Count);
            Assert.Equal(included.Sum(l => l.PlannedRevenue), summary.PlannedRevenue);
            Assert.Equal(included.Sum(l => l.ActualRevenue), summary.ActualRevenue);
            Assert.Equal(included.Sum(l => l.PlannedCost), summary.PlannedCost);
            Assert.Equal(included.Sum(l => l.ActualCost), summary.ActualCost);
            Assert.Equal(included.Sum(l => l.ActualProfit), summary.ActualProfit);
            Assert.Equal(departments.Where(d => d.Item1).Sum(d => d.Item2), summary.PlannedRevenue);
            Assert.Equal(departments.Where(d => d.Item1).Sum(d => d.Item4), summary.ActualRevenue);
            Assert.All(summary.DepartmentLines.Where(l => !l.HasApprovedBudget), l =>
                Assert.Equal((0m, 0m, 0m, 0m), (l.PlannedRevenue, l.ActualRevenue, l.PlannedCost, l.ActualCost)));
            Assert.Equal(departments.All(d => d.Item1), summary.CanApprove);
        }, iter: 試行回数);
    }
}

using CostManagement.Domain.Actuals;
using CostManagement.Domain.Analysis;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;
using CsCheck;
using static CostManagement.Domain.Tests.PropertyBased.ドメイン生成器;

namespace CostManagement.Domain.Tests.PropertyBased;

/// <summary>
/// 分析ドメインサービス(予実差異・バージョン比較・損益・部集計)の性質。
/// 任意の予算・実績に対して、集計の整合性(内訳の合計 = 合計)と入力順への非依存を確かめる。
/// </summary>
public class 分析サービスの性質
{
    private static readonly BudgetVarianceAnalysisService 差異分析 = new();
    private static readonly BudgetComparisonService 版比較 = new();
    private static readonly ProfitAnalysisService 損益分析 = new();
    private static readonly DivisionBudgetSummaryService 部集計 = new();

    private static (BudgetCategory, Guid?, string?, string?) キー(ActualEntry a) =>
        (a.Category, a.ProjectId?.Value, a.ElementCode?.Value, a.PeriodDetail);

    private static (BudgetCategory, Guid?, string?, string?) キー(BudgetLine l) =>
        (l.Category, l.ProjectId?.Value, l.ElementCode?.Value, l.PeriodDetail);

    private static (BudgetCategory, Guid?, string?, string?) キー(VarianceLine l) =>
        (l.Category, l.ProjectId, l.ElementCode, l.PeriodDetail);

    // ---- 予実差異分析 ----

    [Fact]
    public void 差異分析の各明細は実績と予算の差で予算にないキーだけが予定外になる()
    {
        Gen.Select(承認済み課予算, 実績一覧).Sample((budget, actuals) =>
        {
            var report = 差異分析.Analyze(budget, actuals);
            var lines = report.Categories.SelectMany(c => c.Lines).ToList();
            var planned = budget.Lines.ToDictionary(キー, l => l.Amount.Value);

            // 明細は予算キーと実績キーの和集合をちょうど1回ずつ含む
            Assert.Equal(lines.Count, lines.Select(キー).Distinct().Count());
            Assert.Equal(planned.Keys.Union(actuals.Select(キー)).ToHashSet(), lines.Select(キー).ToHashSet());

            foreach (var line in lines)
            {
                Assert.Equal(line.ActualAmount - line.PlannedAmount, line.Variance);
                Assert.Equal(!planned.ContainsKey(キー(line)), line.IsUnplanned);
                Assert.Equal(planned.GetValueOrDefault(キー(line)), line.PlannedAmount);
                Assert.Equal(actuals.Where(a => キー(a) == キー(line)).Sum(a => a.Amount.Value), line.ActualAmount);
                Assert.False(line.IsFavorable && line.IsAdverse);
                Assert.Equal(line.Variance == 0m, !line.IsFavorable && !line.IsAdverse);
            }
        });
    }

    [Fact]
    public void 差異分析の区分合計と全体合計は明細と予算と実績の合計に一致する()
    {
        Gen.Select(承認済み課予算, 実績一覧).Sample((budget, actuals) =>
        {
            var report = 差異分析.Analyze(budget, actuals);

            Assert.Equal(Enum.GetValues<BudgetCategory>(), report.Categories.Select(c => c.Category));
            foreach (var c in report.Categories)
            {
                Assert.Equal(c.Lines.Sum(l => l.PlannedAmount), c.PlannedAmount);
                Assert.Equal(c.Lines.Sum(l => l.ActualAmount), c.ActualAmount);
                Assert.Equal(c.ActualAmount - c.PlannedAmount, c.Variance);
                Assert.Equal(budget.CategoryTotal(c.Category).Value, c.PlannedAmount);
                Assert.Equal(actuals.Where(a => a.Category == c.Category).Sum(a => a.Amount.Value), c.ActualAmount);
            }

            Assert.Equal(budget.CategoryTotal(BudgetCategory.Revenue).Value, report.PlannedRevenue);
            Assert.Equal(budget.TotalCost.Value, report.PlannedCost);
            Assert.Equal(actuals.Where(a => a.Category == BudgetCategory.Revenue).Sum(a => a.Amount.Value),
                report.ActualRevenue);
            Assert.Equal(actuals.Where(a => a.Category != BudgetCategory.Revenue).Sum(a => a.Amount.Value),
                report.ActualCost);
            Assert.Equal(report.ActualRevenue - report.PlannedRevenue, report.RevenueVariance);
            Assert.Equal(report.ActualCost - report.PlannedCost, report.CostVariance);
        });
    }

    [Fact]
    public void 差異分析の結果は実績の並び順に依存しない()
    {
        Gen.Select(承認済み課予算, 実績一覧)
            .SelectMany((budget, actuals) => Gen.Shuffle(actuals.ToList())
                .Select(shuffled => (budget, actuals, shuffled)))
            .Sample((budget, actuals, shuffled) =>
            {
                var original = 差異分析.Analyze(budget, actuals);
                var reordered = 差異分析.Analyze(budget, shuffled);
                Assert.Equal(要約(original), 要約(reordered));
            });

        static string 要約(VarianceReport r) => string.Join(" | ",
            r.Categories.SelectMany(c => c.Lines).Select(l => l.ToString()))
            + $" / {r.PlannedRevenue} {r.ActualRevenue} {r.PlannedCost} {r.ActualCost}";
    }

    // ---- 予算バージョン比較 ----

    [Fact]
    public void 同じ予算同士を比較すると差分はすべて0になる()
    {
        承認済み課予算.Sample(budget =>
        {
            var report = 版比較.Compare(budget, budget);
            Assert.All(report.Categories.SelectMany(c => c.Lines), l => Assert.Equal(0m, l.Difference));
            Assert.All(report.Categories, c => Assert.Equal(0m, c.Difference));
        });
    }

    [Fact]
    public void 比較の向きを入れ替えると差分の符号が反転し区分合計は各予算の区分合計になる()
    {
        Gen.Select(課予算, 課予算).Sample((a, b) =>
        {
            var forward = 版比較.Compare(a, b);
            var backward = 版比較.Compare(b, a);

            foreach (var (f, r) in forward.Categories.Zip(backward.Categories))
            {
                Assert.Equal(f.Category, r.Category);
                Assert.Equal(-f.Difference, r.Difference);
                Assert.Equal(a.CategoryTotal(f.Category).Value, f.BaseAmount);
                Assert.Equal(b.CategoryTotal(f.Category).Value, f.TargetAmount);
                Assert.Equal(f.Lines.Sum(l => l.Difference), f.Difference);
                Assert.Equal(
                    f.Lines.ToDictionary(l => (l.ProjectId, l.ElementCode, l.PeriodDetail), l => l.Difference),
                    r.Lines.ToDictionary(l => (l.ProjectId, l.ElementCode, l.PeriodDetail), l => -l.Difference));
            }
        });
    }

    // ---- 損益分析 ----

    [Fact]
    public void 案件別損益の合計から期間費用を引くと課全体の損益に一致する()
    {
        Gen.Select(承認済み課予算, 実績一覧).Sample((budget, actuals) =>
        {
            var report = 損益分析.Analyze(差異分析.Analyze(budget, actuals));

            Assert.Equal(report.PlannedProfit,
                report.ProjectLines.Sum(l => l.PlannedProfit) - report.PlannedPeriodCost);
            Assert.Equal(report.ActualProfit,
                report.ProjectLines.Sum(l => l.ActualProfit) - report.ActualPeriodCost);
            Assert.Equal(report.ActualProfit - report.PlannedProfit, report.ProfitVariance);
            Assert.Equal(budget.PlannedProfit.Value, report.PlannedProfit);

            foreach (var line in report.ProjectLines)
            {
                Assert.Equal(line.PlannedRevenue - line.PlannedProcessing - line.PlannedOutsourcing, line.PlannedProfit);
                Assert.Equal(line.ActualRevenue - line.ActualProcessing - line.ActualOutsourcing, line.ActualProfit);
                Assert.Equal(line.ActualProfit - line.PlannedProfit, line.ProfitVariance);
            }
            Assert.Equal(report.ProjectLines.Count, report.ProjectLines.Select(l => l.ProjectId).Distinct().Count());
        });
    }

    [Fact]
    public void 粗利率は売上高が0のときだけ算出されず符号は損益の符号と一致する()
    {
        Gen.Select(承認済み課予算, 実績一覧).Sample((budget, actuals) =>
        {
            var report = 損益分析.Analyze(差異分析.Analyze(budget, actuals));

            Assert.Equal(report.PlannedRevenue == 0m, report.PlannedMarginRate is null);
            Assert.Equal(report.ActualRevenue == 0m, report.ActualMarginRate is null);
            if (report.PlannedMarginRate is { } planned)
                Assert.Equal(Math.Sign(report.PlannedProfit), Math.Sign(planned));
            if (report.ActualMarginRate is { } actual)
                Assert.Equal(Math.Sign(report.ActualProfit), Math.Sign(actual));
        });
    }

    // ---- 部集計 ----

    private static readonly Gen<DepartmentCategoryTotals> 課の区分別合計 =
        Gen.Select(Gen.Guid, 区分別金額, 区分別金額)
            .Select((id, planned, actual) => DepartmentCategoryTotals.FromCategoryAmounts(
                new DepartmentId(id),
                planned.Select(p => new DepartmentCategoryAmount(new DepartmentId(id), p.Key, new Money(p.Value))),
                actual.Select(a => new DepartmentCategoryAmount(new DepartmentId(id), a.Key, new Money(a.Value)))));

    /// <summary>区分 → 金額(金額のない区分は含めない。リポジトリの合計取得と同じ形)。</summary>
    private static Gen<Dictionary<BudgetCategory, decimal>> 区分別金額 =>
        Gen.Select(区分, 金額).List[0, 4]
            .Select(pairs => pairs.GroupBy(p => p.Item1).ToDictionary(g => g.Key, g => g.First().Item2));

    [Fact]
    public void 課の区分別合計は4区分を埋めコストは売上高以外の合計になる()
    {
        課の区分別合計.Sample(totals =>
        {
            Assert.Equal(Enum.GetValues<BudgetCategory>(), totals.Categories.Select(c => c.Category));
            var revenue = totals.Categories.Single(c => c.Category == BudgetCategory.Revenue);
            var costs = totals.Categories.Where(c => c.Category != BudgetCategory.Revenue).ToList();
            Assert.Equal(revenue.PlannedAmount, totals.PlannedRevenue);
            Assert.Equal(revenue.ActualAmount, totals.ActualRevenue);
            Assert.Equal(costs.Sum(c => c.PlannedAmount), totals.PlannedCost);
            Assert.Equal(costs.Sum(c => c.ActualAmount), totals.ActualCost);
            Assert.All(totals.Categories, c => Assert.Equal(c.ActualAmount - c.PlannedAmount, c.Variance));
        });
    }

    [Fact]
    public void 部合計は課別内訳の合計に一致する()
    {
        課の区分別合計.List[0, 8].Sample(inputs =>
        {
            var report = 部集計.Summarize(inputs);

            Assert.Equal(inputs.Count, report.DepartmentLines.Count);
            Assert.Equal(report.DepartmentLines.Sum(l => l.PlannedRevenue), report.PlannedRevenue);
            Assert.Equal(report.DepartmentLines.Sum(l => l.ActualRevenue), report.ActualRevenue);
            Assert.Equal(report.DepartmentLines.Sum(l => l.PlannedCost), report.PlannedCost);
            Assert.Equal(report.DepartmentLines.Sum(l => l.ActualCost), report.ActualCost);
            Assert.Equal(report.DepartmentLines.Sum(l => l.PlannedProfit), report.PlannedProfit);
            Assert.Equal(report.DepartmentLines.Sum(l => l.ActualProfit), report.ActualProfit);
            Assert.Equal(report.DepartmentLines.Sum(l => l.ProfitVariance), report.ProfitVariance);
            Assert.Equal(report.PlannedRevenue - report.PlannedCost, report.PlannedProfit);
            Assert.Equal(report.ActualRevenue - report.ActualCost, report.ActualProfit);

            foreach (var c in report.Categories)
            {
                var parts = inputs.SelectMany(i => i.Categories).Where(x => x.Category == c.Category).ToList();
                Assert.Equal(parts.Sum(x => x.PlannedAmount), c.PlannedAmount);
                Assert.Equal(parts.Sum(x => x.ActualAmount), c.ActualAmount);
                Assert.Equal(c.ActualAmount - c.PlannedAmount, c.Variance);
            }
            // 区分別の部合計と、売上高・コストの部合計は整合する
            Assert.Equal(report.Categories.Single(c => c.Category == BudgetCategory.Revenue).PlannedAmount,
                report.PlannedRevenue);
            Assert.Equal(report.Categories.Where(c => c.Category != BudgetCategory.Revenue).Sum(c => c.ActualAmount),
                report.ActualCost);
        });
    }

    [Fact]
    public void 部合計は課の並び順に依存せず課別内訳は入力順を保つ()
    {
        課の区分別合計.List[0, 8]
            .SelectMany(inputs => Gen.Shuffle(inputs.ToList()).Select(shuffled => (inputs, shuffled)))
            .Sample((inputs, shuffled) =>
            {
                var original = 部集計.Summarize(inputs);
                var reordered = 部集計.Summarize(shuffled);

                Assert.Equal(original.Categories, reordered.Categories);
                Assert.Equal((original.PlannedProfit, original.ActualProfit, original.PlannedCost, original.ActualRevenue),
                    (reordered.PlannedProfit, reordered.ActualProfit, reordered.PlannedCost, reordered.ActualRevenue));
                Assert.Equal(shuffled.Select(i => i.DepartmentId.Value), reordered.DepartmentLines.Select(l => l.DepartmentId));
            });
    }
}

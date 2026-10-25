using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;
using CsCheck;
using static CostManagement.Domain.Tests.PropertyBased.ドメイン生成器;

namespace CostManagement.Domain.Tests.PropertyBased;

/// <summary>
/// 課予算(DepartmentBudget)の性質。
/// 任意の編集操作列を集約と単純なモデル(明細キー → 金額・月別金額)の両方に適用し、
/// 成否と結果の状態が一致すること、各操作の後で不変条件が保たれることを確かめる(モデルベーステスト)。
/// </summary>
public class 課予算の性質
{
    // ---- 編集操作 ----

    private abstract record 操作;
    private sealed record 案件明細を半期一括で登録(BudgetCategory 区分, ProjectId 案件, decimal 金額) : 操作;
    private sealed record 案件明細を月次で登録(BudgetCategory 区分, ProjectId 案件, Dictionary<int, decimal> 月別) : 操作
    {
        public override string ToString() => $"案件明細を月次で登録 {{ 区分 = {区分}, 案件 = {案件}, 月別 = {月別表記(月別)} }}";
    }
    private sealed record 期間費用を半期一括で登録(CostElementCode 費目, decimal 金額, string? 明細名) : 操作;
    private sealed record 期間費用を月次で登録(CostElementCode 費目, Dictionary<int, decimal> 月別, string? 明細名) : 操作
    {
        public override string ToString() => $"期間費用を月次で登録 {{ 費目 = {費目}, 月別 = {月別表記(月別)}, 明細名 = {明細名} }}";
    }

    /// <summary>失敗時の反例表示用(Dictionary の既定の ToString は中身を出さないため)。</summary>
    private static string 月別表記(Dictionary<int, decimal> monthly) =>
        "{" + string.Join(", ", monthly.Select(kv => $"{kv.Key}={kv.Value}")) + "}";
    private sealed record 案件明細を削除(BudgetCategory 区分, ProjectId 案件) : 操作;
    private sealed record 期間費用を削除(CostElementCode 費目, string? 明細名) : 操作;
    private sealed record 明細名を変更(CostElementCode 費目, string? 旧明細名, string? 新明細名) : 操作;

    // 区分は4区分すべてから選ぶ(案件別の操作に期間費用を渡すと拒否されることも検証する)。
    private static readonly Gen<操作> 任意の操作 = Gen.Frequency<操作>(
        (4, Gen.Select(区分, 案件, 金額_負を含む).Select(操作 (c, p, a) => new 案件明細を半期一括で登録(c, p, a))),
        (3, Gen.Select(区分, 案件, 月別金額_不正を含む).Select(操作 (c, p, m) => new 案件明細を月次で登録(c, p, m))),
        (4, Gen.Select(費目, 金額_負を含む, 明細名).Select(操作 (e, a, d) => new 期間費用を半期一括で登録(e, a, d))),
        (3, Gen.Select(費目, 月別金額_不正を含む, 明細名).Select(操作 (e, m, d) => new 期間費用を月次で登録(e, m, d))),
        (2, Gen.Select(区分, 案件).Select(操作 (c, p) => new 案件明細を削除(c, p))),
        (2, Gen.Select(費目, 明細名).Select(操作 (e, d) => new 期間費用を削除(e, d))),
        (2, Gen.Select(費目, 明細名, 明細名).Select(操作 (e, o, n) => new 明細名を変更(e, o, n))));

    // ---- モデル ----

    private readonly record struct 明細キー(BudgetCategory 区分, Guid? 案件, string? 費目, string? 明細名);

    private sealed record 明細モデル(Guid? Id, decimal 金額, IReadOnlyDictionary<int, decimal> 月別);

    private static string? 正規化(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

    private static 明細キー キー(BudgetLine l) =>
        new(l.Category, l.ProjectId?.Value, l.ElementCode?.Value, l.PeriodDetail);

    private static bool 月別が妥当(Dictionary<int, decimal> monthly) =>
        monthly.All(kv => kv.Key is >= 1 and <= HalfMonths.Count && kv.Value >= 0m);

    private static 明細モデル 月次の明細(Guid? id, Dictionary<int, decimal> monthly)
    {
        var nonZero = monthly.Where(kv => kv.Value != 0m).ToDictionary(kv => kv.Key, kv => kv.Value);
        return new 明細モデル(id, nonZero.Values.Sum(), nonZero);
    }

    /// <summary>
    /// 操作をモデルに適用する。仕様上拒否されるべき操作なら false を返しモデルを変えない。
    /// </summary>
    private static bool モデルに適用(Dictionary<明細キー, 明細モデル> model, 操作 op)
    {
        bool 費目に明細名つきがある(CostElementCode e) =>
            model.Keys.Any(k => k.区分 == BudgetCategory.PeriodCost && k.費目 == e.Value && k.明細名 is not null);
        bool 費目に費目一括がある(CostElementCode e) =>
            model.Keys.Any(k => k.区分 == BudgetCategory.PeriodCost && k.費目 == e.Value && k.明細名 is null);
        bool 混在になる(CostElementCode e, string? detail) =>
            detail is null ? 費目に明細名つきがある(e) : 費目に費目一括がある(e);
        Guid? 既存Id(明細キー key) => model.TryGetValue(key, out var line) ? line.Id : null;

        switch (op)
        {
            case 案件明細を半期一括で登録(var c, var p, var a):
            {
                if (!c.IsProjectBased() || a < 0m) return false;
                var key = new 明細キー(c, p.Value, null, null);
                model[key] = new 明細モデル(既存Id(key), a, new Dictionary<int, decimal>());
                return true;
            }
            case 案件明細を月次で登録(var c, var p, var m):
            {
                if (!c.IsProjectBased() || !月別が妥当(m)) return false;
                var key = new 明細キー(c, p.Value, null, null);
                model[key] = 月次の明細(既存Id(key), m);
                return true;
            }
            case 期間費用を半期一括で登録(var e, var a, var d):
            {
                var detail = 正規化(d);
                if (a < 0m || 混在になる(e, detail)) return false;
                var key = new 明細キー(BudgetCategory.PeriodCost, null, e.Value, detail);
                model[key] = new 明細モデル(既存Id(key), a, new Dictionary<int, decimal>());
                return true;
            }
            case 期間費用を月次で登録(var e, var m, var d):
            {
                var detail = 正規化(d);
                if (!月別が妥当(m) || 混在になる(e, detail)) return false;
                var key = new 明細キー(BudgetCategory.PeriodCost, null, e.Value, detail);
                model[key] = 月次の明細(既存Id(key), m);
                return true;
            }
            case 案件明細を削除(var c, var p):
                return c.IsProjectBased() && model.Remove(new 明細キー(c, p.Value, null, null));
            case 期間費用を削除(var e, var d):
                return model.Remove(new 明細キー(BudgetCategory.PeriodCost, null, e.Value, 正規化(d)));
            case 明細名を変更(var e, var oldName, var newName):
            {
                var 旧キー = new 明細キー(BudgetCategory.PeriodCost, null, e.Value, 正規化(oldName));
                var 新キー = 旧キー with { 明細名 = 正規化(newName) };
                if (新キー.明細名 is null || !model.TryGetValue(旧キー, out var line)) return false;
                if (新キー == 旧キー) return true;
                // 費目一括(明細名なし)の明細は改名できない。改名先が既存なら重複で拒否。
                if (旧キー.明細名 is null || model.ContainsKey(新キー)) return false;
                model.Remove(旧キー);
                model[新キー] = line;
                return true;
            }
            default:
                throw new InvalidOperationException($"未知の操作: {op}");
        }
    }

    private static void 集約に適用(DepartmentBudget budget, 操作 op)
    {
        switch (op)
        {
            case 案件明細を半期一括で登録(var c, var p, var a):
                budget.UpsertProjectLine(c, p, new Money(a)); break;
            case 案件明細を月次で登録(var c, var p, var m):
                budget.UpsertProjectLineMonthly(c, p, ToMoney(m)); break;
            case 期間費用を半期一括で登録(var e, var a, var d):
                budget.UpsertPeriodCostLine(e, new Money(a), d); break;
            case 期間費用を月次で登録(var e, var m, var d):
                budget.UpsertPeriodCostLineMonthly(e, ToMoney(m), d); break;
            case 案件明細を削除(var c, var p):
                budget.RemoveProjectLine(c, p); break;
            case 期間費用を削除(var e, var d):
                budget.RemovePeriodCostLine(e, d); break;
            case 明細名を変更(var e, var oldName, var newName):
                budget.RenamePeriodCostDetail(e, oldName!, newName!); break;
            default:
                throw new InvalidOperationException($"未知の操作: {op}");
        }
    }

    /// <summary>集約の明細の状態を比較可能な形に写し取る(ID・金額・月別金額を含む)。</summary>
    private static string 状態(DepartmentBudget budget) =>
        string.Join(" | ", budget.Lines
            .OrderBy(l => l.Id)
            .Select(l => $"{l.Id}:{キー(l)}:{l.Amount.Value}:" +
                string.Join(",", l.MonthlyAmounts.OrderBy(m => m.Key).Select(m => $"{m.Key}={m.Value.Value}"))));

    /// <summary>操作の成否に関わらず常に成り立つべき不変条件。</summary>
    private static void 不変条件を確認(DepartmentBudget budget)
    {
        // 明細キーは集約内で一意
        Assert.Equal(budget.Lines.Count, budget.Lines.Select(キー).Distinct().Count());
        Assert.Equal(budget.Lines.Count, budget.Lines.Select(l => l.Id).Distinct().Count());

        // 区分合計 = 明細合計、計画損益 = 売上高 − 総コスト
        foreach (var category in Enum.GetValues<BudgetCategory>())
        {
            var expected = budget.Lines.Where(l => l.Category == category).Sum(l => l.Amount.Value);
            Assert.Equal(expected, budget.CategoryTotal(category).Value);
        }
        Assert.Equal(budget.CategoryTotal(BudgetCategory.Revenue) - budget.TotalCost, budget.PlannedProfit);

        foreach (var line in budget.Lines)
        {
            // 区分と案件/費目の排他、明細名は期間費用のみ
            Assert.Equal(line.Category.IsProjectBased(), line.ProjectId is not null);
            Assert.Equal(line.Category == BudgetCategory.PeriodCost, line.ElementCode is not null);
            if (line.Category != BudgetCategory.PeriodCost)
                Assert.Null(line.PeriodDetail);

            // 金額は0以上。月次なら半期合計 = 月別の合計、月は1..6、0円の月は保持しない
            Assert.False(line.Amount.IsNegative);
            Assert.Equal(line.MonthlyAmounts.Count > 0, line.IsMonthly);
            if (line.IsMonthly)
            {
                Assert.Equal(line.MonthlyAmounts.Values.Sum(m => m.Value), line.Amount.Value);
                Assert.All(line.MonthlyAmounts, m =>
                {
                    Assert.InRange(m.Key, 1, HalfMonths.Count);
                    Assert.True(m.Value.Value > 0m);
                });
            }
        }

        // 1費目内で費目一括と明細は混在しない
        foreach (var element in budget.Lines.Where(l => l.ElementCode is not null).GroupBy(l => l.ElementCode))
            Assert.Single(element.Select(l => l.PeriodDetail is null).Distinct());
    }

    [Fact]
    public void 任意の編集操作列でも集約はモデルと一致し不変条件を保つ()
    {
        任意の操作.List[1, 40].Sample(ops =>
        {
            var budget = DepartmentBudget.CreateInitial(対象課, 対象半期, "当初予算", 現在時刻);
            var model = new Dictionary<明細キー, 明細モデル>();

            foreach (var op in ops)
            {
                var before = 状態(budget);
                var expectedSuccess = モデルに適用(model, op);
                var error = Record.Exception(() => 集約に適用(budget, op));

                if (expectedSuccess)
                    Assert.Null(error);
                else
                {
                    Assert.IsType<DomainException>(error);
                    // 拒否された操作は集約の状態を一切変えない
                    Assert.Equal(before, 状態(budget));
                }

                不変条件を確認(budget);

                // モデルとの一致(明細キー・金額・月別金額、上書きでは明細IDが変わらない)
                var actual = budget.Lines.ToDictionary(キー);
                Assert.Equal(model.Keys.ToHashSet(), actual.Keys.ToHashSet());
                foreach (var (key, expected) in model.ToList())
                {
                    var line = actual[key];
                    Assert.Equal(expected.金額, line.Amount.Value);
                    Assert.Equal(expected.月別.OrderBy(m => m.Key),
                        line.MonthlyAmounts.OrderBy(m => m.Key).Select(m => KeyValuePair.Create(m.Key, m.Value.Value)));
                    if (expected.Id is { } id)
                        Assert.Equal(id, line.Id);
                    else
                        model[key] = expected with { Id = line.Id };
                }
            }
        });
    }

    [Fact]
    public void 承認済みの予算はどの編集操作も拒否し状態を変えない()
    {
        Gen.Select(承認済み課予算, 任意の操作.List[1, 10]).Sample((budget, ops) =>
        {
            var before = 状態(budget);
            foreach (var op in ops)
                Assert.Throws<DomainException>(() => 集約に適用(budget, op));
            Assert.Equal(before, 状態(budget));
            Assert.Equal(BudgetStatus.Approved, budget.Status);
        });
    }

    [Fact]
    public void 改定版は基の予算の明細を金額と月別金額と明細名ごと新しいIDで引き継ぐ()
    {
        Gen.Select(承認済み課予算, Gen.Int[1, 10]).Sample((baseBudget, step) =>
        {
            var revised = DepartmentBudget.ReviseFrom(baseBudget, baseBudget.Version + step, "見直し", 現在時刻);

            Assert.Equal(BudgetStatus.Draft, revised.Status);
            Assert.Equal(baseBudget.Version + step, revised.Version);
            Assert.Equal(baseBudget.DepartmentId, revised.DepartmentId);
            Assert.Equal(baseBudget.FiscalHalf, revised.FiscalHalf);
            Assert.Empty(revised.Lines.Select(l => l.Id).Intersect(baseBudget.Lines.Select(l => l.Id)));
            Assert.Equal(状態の値(baseBudget), 状態の値(revised));
            foreach (var category in Enum.GetValues<BudgetCategory>())
                Assert.Equal(baseBudget.CategoryTotal(category), revised.CategoryTotal(category));
        });

        static string 状態の値(DepartmentBudget b) => string.Join(" | ", b.Lines
            .Select(l => $"{キー(l)}:{l.Amount.Value}:{l.IsMonthly}:" +
                string.Join(",", l.MonthlyAmounts.OrderBy(m => m.Key).Select(m => $"{m.Key}={m.Value.Value}")))
            .Order());
    }

    [Fact]
    public void 基の予算以下のバージョン番号では改定版を作れない()
    {
        Gen.Select(承認済み課予算, Gen.Int[-5, 0]).Sample((baseBudget, offset) =>
            Assert.Throws<DomainException>(() =>
                DepartmentBudget.ReviseFrom(baseBudget, baseBudget.Version + offset, "見直し", 現在時刻)));
    }

    [Fact]
    public void 永続化形式から復元した予算は元の予算と同じ明細を持つ()
    {
        Gen.Select(課予算, Gen.Enum<BudgetStatus>()).Sample((budget, status) =>
        {
            var restored = DepartmentBudget.Restore(budget.Id.Value, budget.DepartmentId.Value,
                budget.FiscalHalf.ToString(), budget.Version, budget.Label, status,
                budget.CreatedAt, budget.ApprovedAt,
                budget.Lines.Select(l => (l.Id, l.Category.ToString(), l.ProjectId?.Value,
                    l.ElementCode?.Value, l.PeriodDetail, l.Amount.Value,
                    (IReadOnlyDictionary<int, decimal>)l.MonthlyAmounts.ToDictionary(m => m.Key, m => m.Value.Value))));

            Assert.Equal(状態(budget), 状態(restored));
            Assert.Equal(status, restored.Status);
            Assert.Equal(budget.FiscalHalf, restored.FiscalHalf);
        });
    }

    [Fact]
    public void 承認できるのは明細のあるドラフトだけで承認後は失効にのみ遷移できる()
    {
        課予算.Sample(budget =>
        {
            if (budget.Lines.Count == 0)
            {
                Assert.Throws<DomainException>(() => budget.Approve(現在時刻));
                Assert.Equal(BudgetStatus.Draft, budget.Status);
                return;
            }
            Assert.Throws<DomainException>(() => budget.Supersede()); // ドラフトは失効にできない
            budget.Approve(現在時刻);
            Assert.Equal(現在時刻, budget.ApprovedAt);
            Assert.Throws<DomainException>(() => budget.Approve(現在時刻)); // 二重承認不可
            budget.Supersede();
            Assert.Equal(BudgetStatus.Superseded, budget.Status);
            Assert.Throws<DomainException>(() => budget.Supersede());
            Assert.Throws<DomainException>(() => budget.Approve(現在時刻));
        });
    }
}

using CostManagement.Domain.Actuals;
using CostManagement.Domain.Budgeting;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Projects;
using CostManagement.Domain.Shared;
using CsCheck;

namespace CostManagement.Domain.Tests.PropertyBased;

/// <summary>
/// プロパティベーステスト(CsCheck)用のドメイン値の生成器。
/// 案件・費目・明細名は小さなプールから選び、同一キーへの上書き・衝突・混在が十分な頻度で起きるようにする。
/// </summary>
internal static class ドメイン生成器
{
    public static readonly DateTime 現在時刻 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DepartmentId 対象課 = new(Guid.Parse("00000000-0000-0000-0000-0000000000d1"));
    public static readonly FiscalHalf 対象半期 = new(2026, HalfTerm.H1);

    public static readonly ProjectId[] 案件プール =
    [
        new(Guid.Parse("00000000-0000-0000-0000-00000000000a")),
        new(Guid.Parse("00000000-0000-0000-0000-00000000000b")),
        new(Guid.Parse("00000000-0000-0000-0000-00000000000c")),
    ];

    public static readonly CostElementCode[] 費目プール =
        [new("PERSONNEL"), new("LICENSE"), new("TRAVEL")];

    public static readonly BudgetCategory[] 案件別区分 =
        [BudgetCategory.Revenue, BudgetCategory.Processing, BudgetCategory.Outsourcing];

    /// <summary>金額(円。0 〜 100億、1円単位)。decimal の加減算を厳密に比較できる範囲に収める。</summary>
    public static readonly Gen<decimal> 金額 = Gen.Long[0, 10_000_000_000L].Select(v => (decimal)v);

    /// <summary>負の金額も稀に混ぜる(拒否されることの検証用)。</summary>
    public static readonly Gen<decimal> 金額_負を含む = Gen.Frequency(
        (9, 金額),
        (1, Gen.Long[-1_000_000L, -1L].Select(v => (decimal)v)));

    public static readonly Gen<BudgetCategory> 区分 = Gen.Enum<BudgetCategory>();
    public static readonly Gen<BudgetCategory> 案件区分 = Gen.OneOfConst(案件別区分);
    public static readonly Gen<ProjectId> 案件 = Gen.OneOfConst(案件プール);
    public static readonly Gen<CostElementCode> 費目 = Gen.OneOfConst(費目プール);

    /// <summary>
    /// 明細名。null/空白(= 費目一括)と、前後に空白を含む表記ゆれ(正規化で同一視される)を混ぜる。
    /// </summary>
    public static readonly Gen<string?> 明細名 = Gen.OneOfConst<string?>(
        null, "", "  ", "AWS", " AWS ", "GitHub", "Slack");

    /// <summary>月別金額(月インデックス1..6 → 金額)。0円の月も含む。</summary>
    public static readonly Gen<Dictionary<int, decimal>> 月別金額 =
        Gen.Select(Gen.Int[1, HalfMonths.Count], 金額).List[0, HalfMonths.Count]
            .Select(pairs => pairs
                .GroupBy(p => p.Item1)
                .ToDictionary(g => g.Key, g => g.Last().Item2));

    /// <summary>月別金額。範囲外の月(0, 7)や負の金額を稀に混ぜる(拒否されることの検証用)。</summary>
    public static readonly Gen<Dictionary<int, decimal>> 月別金額_不正を含む = Gen.Frequency(
        (8, 月別金額),
        (1, Gen.Select(月別金額, Gen.OneOfConst(0, 7, -1), 金額)
            .Select((m, badMonth, amount) => new Dictionary<int, decimal>(m) { [badMonth] = amount })),
        (1, Gen.Select(月別金額, Gen.Int[1, HalfMonths.Count])
            .Select((m, month) => new Dictionary<int, decimal>(m) { [month] = -1m })));

    public static IReadOnlyDictionary<int, Money> ToMoney(IReadOnlyDictionary<int, decimal> monthly) =>
        monthly.ToDictionary(kv => kv.Key, kv => new Money(kv.Value));

    /// <summary>
    /// 課予算(ドラフト)。4区分の明細を半期一括・月次・明細名つきで任意に組み合わせる。
    /// 生成手順は必ず成功する操作だけを使う(費目一括と明細の混在は費目ごとにモードを決めて避ける)。
    /// </summary>
    public static readonly Gen<DepartmentBudget> 課予算 =
        Gen.Select(
                Gen.Select(案件区分, 案件, 金額, Gen.Bool, 月別金額).List[0, 8],
                Gen.Select(費目, 明細名, 金額, Gen.Bool, 月別金額).List[0, 6])
            .Select((projectLines, periodLines) =>
            {
                var budget = DepartmentBudget.CreateInitial(対象課, 対象半期, "当初予算", 現在時刻);
                foreach (var (category, project, amount, monthly, months) in projectLines)
                {
                    if (monthly)
                        budget.UpsertProjectLineMonthly(category, project, ToMoney(months));
                    else
                        budget.UpsertProjectLine(category, project, new Money(amount));
                }
                // 費目ごとに最初に現れた明細名の有無でモードを固定し、混在を避ける。
                var modeByElement = new Dictionary<CostElementCode, bool>();
                foreach (var (element, detail, amount, monthly, months) in periodLines)
                {
                    var hasDetail = !string.IsNullOrWhiteSpace(detail);
                    if (modeByElement.TryGetValue(element, out var mode) && mode != hasDetail)
                        continue;
                    modeByElement[element] = hasDetail;
                    if (monthly)
                        budget.UpsertPeriodCostLineMonthly(element, ToMoney(months), detail);
                    else
                        budget.UpsertPeriodCostLine(element, new Money(amount), detail);
                }
                return budget;
            });

    /// <summary>明細が1件以上ある承認済みの課予算。</summary>
    public static readonly Gen<DepartmentBudget> 承認済み課予算 =
        課予算.Where(b => b.Lines.Count > 0).Select(b =>
        {
            b.Approve(現在時刻);
            return b;
        });

    /// <summary>
    /// 実績(常に計上に成功する組み合わせ)。案件別区分は案件、期間費用は費目と任意の明細名を持つ。
    /// </summary>
    public static readonly Gen<ActualEntry> 実績 = Gen.Frequency(
        (3, Gen.Select(案件区分, 案件, 金額, Gen.Int[1, HalfMonths.Count].Nullable(0.5))
            .Select((category, project, amount, month) => ActualEntry.Record(対象課, 対象半期, category,
                project, null, month, new Money(amount), null, 現在時刻))),
        (1, Gen.Select(費目, 明細名, 金額, Gen.Int[1, HalfMonths.Count].Nullable(0.5))
            .Select((element, detail, amount, month) => ActualEntry.Record(対象課, 対象半期,
                BudgetCategory.PeriodCost, null, element, month, new Money(amount), null, 現在時刻,
                detail))));

    public static readonly Gen<List<ActualEntry>> 実績一覧 = 実績.List[0, 15];
}

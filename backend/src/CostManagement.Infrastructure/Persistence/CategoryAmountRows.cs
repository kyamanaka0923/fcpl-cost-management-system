using CostManagement.Domain.Budgeting;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;

namespace CostManagement.Infrastructure.Persistence;

/// <summary>
/// (課, 区分, 金額) の生の行を <see cref="DepartmentCategoryAmount"/> へ畳み込むヘルパ。
/// 金額カラムは TEXT 保存で SQL の SUM() が使えないため、合計は decimal のままここで行う
/// (REAL へキャストすると丸め誤差が出るので採用しない)。
/// </summary>
internal static class CategoryAmountRows
{
    /// <summary>(課, 区分, 金額) の1行に対応する DTO。</summary>
    internal sealed record Row(Guid DepartmentId, string Category, decimal Amount);

    /// <summary>区分名の文字列を列挙型へ引くための表(行ごとの Enum.Parse を避ける)。</summary>
    private static readonly Dictionary<string, BudgetCategory> CategoryByName =
        Enum.GetValues<BudgetCategory>().ToDictionary(c => c.ToString());

    /// <summary>行を (課, 区分) ごとに合計する。金額が1件もない組み合わせは結果に含まれない。</summary>
    public static IReadOnlyList<DepartmentCategoryAmount> Aggregate(IEnumerable<Row> rows)
    {
        var totals = new Dictionary<(Guid Department, BudgetCategory Category), decimal>();
        foreach (var row in rows)
        {
            var key = (row.DepartmentId, CategoryByName[row.Category]);
            totals[key] = totals.GetValueOrDefault(key) + row.Amount;
        }
        return totals
            .Select(kv => new DepartmentCategoryAmount(
                new DepartmentId(kv.Key.Department), kv.Key.Category, new Money(kv.Value)))
            .ToList();
    }
}

using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Budgeting;

/// <summary>
/// (課, 区分) ごとの金額合計。部の集計のように**明細を必要としない**読み取り用のビュー。
/// 予算側・実績側の双方がこの形で合計を返し、部サマリはこれだけで組み立てられる
/// (明細まで復元すると課数 × 明細数に比例したコストがかかるため)。
/// </summary>
public readonly record struct DepartmentCategoryAmount(
    DepartmentId DepartmentId,
    BudgetCategory Category,
    Money Amount);

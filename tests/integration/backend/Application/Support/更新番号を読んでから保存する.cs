namespace CostManagement.Application.Tests.Support;

/// <summary>
/// 楽観ロック(BR-BUD-008・SPEC-004)を確かめないテスト向けに、予算への保存の直前に予算を読み、
/// その更新番号を付けて保存する。ほかの保存と重ならない 1 つの画面の操作にあたるので、衝突しない。
/// 衝突を確かめるテストは、更新番号を明示する <see cref="DepartmentBudgetService"/> のメソッドを直接呼ぶ。
/// </summary>
public static class 更新番号を読んでから保存する
{
    /// <summary>予算を読んだ直後の更新番号で明細を保存する。</summary>
    public static async Task<BudgetDetailDto> UpsertLineAsync(this DepartmentBudgetService svc,
        Guid budgetId, UpsertBudgetLineRequest request) =>
        await svc.UpsertLineAsync(budgetId, request, await 現在の更新番号(svc, budgetId));

    /// <summary>予算を読んだ直後の更新番号で明細を削除する。</summary>
    public static async Task<BudgetDetailDto> RemoveLineAsync(this DepartmentBudgetService svc,
        Guid budgetId, string category, Guid? projectId, string? elementCode,
        string? periodDetail = null) =>
        await svc.RemoveLineAsync(budgetId, category, projectId, elementCode, periodDetail,
            await 現在の更新番号(svc, budgetId));

    /// <summary>予算を読んだ直後の更新番号で明細名を変更する。</summary>
    public static async Task<BudgetDetailDto> RenamePeriodDetailAsync(this DepartmentBudgetService svc,
        Guid budgetId, RenamePeriodDetailRequest request) =>
        await svc.RenamePeriodDetailAsync(budgetId, request, await 現在の更新番号(svc, budgetId));

    /// <summary>予算を読んだ直後の更新番号で承認する。</summary>
    public static async Task<BudgetDetailDto> ApproveAsync(this DepartmentBudgetService svc,
        Guid budgetId) =>
        await svc.ApproveAsync(budgetId, await 現在の更新番号(svc, budgetId));

    /// <summary>予算を読み、いまの更新番号を返す。</summary>
    private static async Task<int> 現在の更新番号(DepartmentBudgetService svc, Guid budgetId) =>
        (await svc.GetAsync(budgetId)).UpdateNumber;
}

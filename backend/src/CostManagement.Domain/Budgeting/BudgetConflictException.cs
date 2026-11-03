namespace CostManagement.Domain.Budgeting;

/// <summary>
/// 予算への保存の衝突(BR-BUD-008)を表す例外。画面が読んだ時点のあとに、その予算へほかの保存が受け付けられていた。
/// 不変条件の違反(<see cref="Shared.DomainException"/>)と区別するための型で、WebApi が 409 に変換する。
/// </summary>
public sealed class BudgetConflictException : Exception
{
    /// <summary>利用者に見せる衝突のメッセージ(BR-BUD-008)。</summary>
    private const string ConflictMessage =
        "ほかの画面でこの予算が更新されました。最新の内容を読み込み直しました。もう一度入力してください。";

    /// <summary>衝突のメッセージで生成する。</summary>
    public BudgetConflictException() : base(ConflictMessage)
    {
    }
}

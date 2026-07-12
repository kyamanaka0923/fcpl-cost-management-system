namespace CostManagement.Domain.Shared;

/// <summary>ドメイン不変条件の違反を表す例外。</summary>
public sealed class DomainException : Exception
{
    /// <summary>ユーザーに提示できる日本語メッセージで生成する(WebApi が 400 に変換する)。</summary>
    public DomainException(string message) : base(message)
    {
    }
}

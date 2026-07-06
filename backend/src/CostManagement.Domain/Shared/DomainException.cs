namespace CostManagement.Domain.Shared;

/// <summary>ドメイン不変条件の違反を表す例外。</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

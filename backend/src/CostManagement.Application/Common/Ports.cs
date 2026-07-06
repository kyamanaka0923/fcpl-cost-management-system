namespace CostManagement.Application.Common;

/// <summary>現在時刻を提供するポート(テスト容易性のための抽象)。</summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

/// <summary>要求されたリソースが存在しないことを表す例外。</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

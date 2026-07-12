namespace CostManagement.Application.Common;

/// <summary>現在時刻を提供するポート(テスト容易性のための抽象)。</summary>
public interface ISystemClock
{
    /// <summary>現在の UTC 時刻。</summary>
    DateTime UtcNow { get; }
}

/// <summary>要求されたリソースが存在しないことを表す例外。</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>ユーザーに提示できる日本語メッセージで生成する(WebApi が 404 に変換する)。</summary>
    public NotFoundException(string message) : base(message)
    {
    }
}

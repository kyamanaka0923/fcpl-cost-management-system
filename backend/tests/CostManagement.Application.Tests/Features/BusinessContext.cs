using System.Globalization;
using CostManagement.Application.Common;
using CostManagement.Domain.Shared;
using Reqnroll;

namespace CostManagement.Application.Tests.Features;

/// <summary>
/// Gherkin シナリオ1本ぶんの状態。Reqnroll のコンテキスト注入で各ステップ定義クラスに共有され、
/// シナリオ終了時に Dispose されて一時 SQLite が削除される。
/// フィーチャーでは部・課・案件をコード(= 名称)で書き、ここで ID へ解決する。
/// </summary>
public sealed class 業務コンテキスト : IDisposable
{
    public UseCaseFixture Fx { get; } = new();

    public Dictionary<string, Guid> 部 { get; } = new();
    public Dictionary<string, Guid> 課 { get; } = new();
    public Dictionary<(string 課, string 案件), Guid> 案件 { get; } = new();

    /// <summary>直近に操作した (課, 半期) と予算。「予算を承認する」等の主語を省いたステップが対象にする。</summary>
    public string? 現在の課 { get; set; }
    public string? 現在の半期 { get; set; }
    public BudgetDetailDto? 現在の予算 { get; set; }

    public VarianceReportDto? 差異 { get; set; }
    public ProfitSummaryDto? 損益 { get; set; }
    public DivisionBudgetSummaryDto? 部サマリ { get; set; }

    /// <summary>「…しようとする」ステップで捕捉した業務エラー。「… を含むエラーになる」で検証する。</summary>
    public Exception? 直前のエラー { get; set; }

    public Guid 課Id(string code) =>
        課.TryGetValue(code, out var id) ? id : throw new InvalidOperationException($"課 \"{code}\" が未登録です。");

    public Guid 案件Id(string dept, string code) =>
        案件.TryGetValue((dept, code), out var id)
            ? id
            : throw new InvalidOperationException($"課 \"{dept}\" の案件 \"{code}\" が未登録です。");

    public BudgetDetailDto 予算() =>
        現在の予算 ?? throw new InvalidOperationException("対象の予算がありません。先に予算を起票してください。");

    /// <summary>業務エラー(400/404 相当)になりうる操作を実行し、例外を記録する。</summary>
    public async Task 試行(Func<Task> action)
    {
        直前のエラー = null;
        try
        {
            await action();
        }
        catch (Exception e) when (e is DomainException or NotFoundException)
        {
            直前のエラー = e;
        }
    }

    // ---- 日本語表記 ↔ 内部表記 ----

    public static string 区分(string 日本語) => 日本語 switch
    {
        "売上高" => "Revenue",
        "加工費" => "Processing",
        "外注費" => "Outsourcing",
        "期間費用" => "PeriodCost",
        _ => throw new ArgumentException($"未知の区分です: {日本語}"),
    };

    public static string 状態(string 内部) => 内部 switch
    {
        "Draft" => "策定中",
        "Approved" => "承認済",
        "Superseded" => "失効",
        _ => 内部,
    };

    /// <summary>表のセルの金額(実行環境のロケールに依存せずに読む)。</summary>
    public static decimal 金額(string cell) => decimal.Parse(cell, CultureInfo.InvariantCulture);

    /// <summary>表のセルの整数(月・版など)。</summary>
    public static int 整数(string cell) => int.Parse(cell, CultureInfo.InvariantCulture);

    /// <summary>表のセルの空文字・空白を null として扱う。</summary>
    public static string? 空ならnull(string? cell) => string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();

    public void Dispose() => Fx.Dispose();
}

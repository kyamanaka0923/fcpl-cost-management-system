using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CostManagement.E2E.Tests.Support;

/// <summary>
/// 予算への保存(明細の保存・削除・明細名の変更・承認)に付けるクエリ updateNumber(BR-BUD-008・SPEC-004)。
/// 楽観ロックを確かめないテストは、保存の直前に予算を読み、その更新番号を付けて送る(衝突しない 1 つの画面の操作)。
/// </summary>
public static class 更新番号
{
    /// <summary>
    /// 予算を読み、いまの更新番号をクエリ updateNumber として path に付けて返す。
    /// 予算が読めないとき(存在しない予算)は 0 を付ける(予算がない拒否 404 はそのまま返る)。
    /// </summary>
    public static async Task<string> 読んで付ける(HttpClient client, string budgetId, string path)
    {
        var res = await client.GetAsync($"/api/budgets/{budgetId}");
        var number = res.StatusCode == HttpStatusCode.OK
            ? (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updateNumber").GetInt32()
            : 0;
        return 付ける(path, number);
    }

    /// <summary>クエリ updateNumber を path に付ける。</summary>
    public static string 付ける(string path, int number) =>
        $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}updateNumber={number.ToString(CultureInfo.InvariantCulture)}";
}

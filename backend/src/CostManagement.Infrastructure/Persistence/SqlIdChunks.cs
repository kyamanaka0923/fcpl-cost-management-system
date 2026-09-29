namespace CostManagement.Infrastructure.Persistence;

/// <summary>
/// IN 句へ展開する ID 群を、SQLite のパラメータ上限に収まる大きさへ分割するヘルパ。
/// Dapper はリストパラメータを 1 要素 1 パラメータへ展開するため、
/// 課や予算をまとめて引く一括取得系のクエリで使う。
/// </summary>
internal static class SqlIdChunks
{
    /// <summary>1 つの IN 句に展開する ID の最大数(SQLite の既定上限に対し十分小さく取る)。</summary>
    private const int ChunkSize = 500;

    /// <summary>
    /// ID 群を IN 句 1 回分ずつのチャンクに分割する。
    /// <para>
    /// Guid は文字列化せず Guid のまま渡すこと。ID カラムは TEXT だが、書き込み側も Guid を
    /// そのまま渡しており、TEXT への変換(大文字の "D" 形式)はドライバが行っている。
    /// 呼び出し側で <c>ToString("D")</c>(小文字)にすると保存値と一致せず 1 件も引けない。
    /// </para>
    /// </summary>
    public static IEnumerable<Guid[]> Chunked(IEnumerable<Guid> ids) => ids.Chunk(ChunkSize);
}

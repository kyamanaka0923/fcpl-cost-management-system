using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CostManagement.Infrastructure.Persistence;

/// <summary>SQLite 接続を生成するファクトリ。</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    static SqliteConnectionFactory()
    {
        // Microsoft.Data.Sqlite は Guid/decimal/DateTime を TEXT で保持するため、
        // Dapper の読み書きを型ハンドラで明示的に橋渡しする。
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.AddTypeHandler(new DecimalTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
    }

    /// <summary>接続文字列を受け取る(例: "Data Source=costmanagement.db")。</summary>
    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>接続を開き、外部キー制約を有効化して返す。呼び出し側が Dispose する。</summary>
    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    /// <summary>Guid を TEXT("D" 形式)で往復させる Dapper 型ハンドラ。</summary>
    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        /// <summary>Guid を "D" 形式の文字列としてパラメータへ設定する。</summary>
        public override void SetValue(IDbDataParameter parameter, Guid value) =>
            parameter.Value = value.ToString("D");

        /// <summary>TEXT を Guid へ変換する。</summary>
        public override Guid Parse(object value) => Guid.Parse((string)value);
    }

    /// <summary>decimal を TEXT(不変カルチャ)で往復させる Dapper 型ハンドラ。</summary>
    private sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        /// <summary>decimal を不変カルチャの文字列としてパラメータへ設定する。</summary>
        public override void SetValue(IDbDataParameter parameter, decimal value) =>
            parameter.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>TEXT/数値を decimal へ変換する。</summary>
        public override decimal Parse(object value) => value switch
        {
            decimal d => d,
            double d => (decimal)d,
            long l => l,
            string s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    /// <summary>DateTime を TEXT(ISO 8601 "O" 形式)で往復させる Dapper 型ハンドラ。</summary>
    private sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
    {
        /// <summary>DateTime を ラウンドトリップ("O")形式の文字列としてパラメータへ設定する。</summary>
        public override void SetValue(IDbDataParameter parameter, DateTime value) =>
            parameter.Value = value.ToString("O");

        /// <summary>TEXT を DateTime へ変換する(ラウンドトリップ解釈)。</summary>
        public override DateTime Parse(object value) => DateTime.Parse((string)value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
    }
}

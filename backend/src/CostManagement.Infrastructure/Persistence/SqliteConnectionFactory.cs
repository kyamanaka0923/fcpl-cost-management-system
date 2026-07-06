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

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value) =>
            parameter.Value = value.ToString("D");

        public override Guid Parse(object value) => Guid.Parse((string)value);
    }

    private sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter parameter, decimal value) =>
            parameter.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public override decimal Parse(object value) => value switch
        {
            decimal d => d,
            double d => (decimal)d,
            long l => l,
            string s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value) =>
            parameter.Value = value.ToString("O");

        public override DateTime Parse(object value) => DateTime.Parse((string)value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
    }
}

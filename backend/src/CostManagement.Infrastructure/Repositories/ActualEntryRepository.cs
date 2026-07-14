using CostManagement.Domain.Actuals;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;
using CostManagement.Infrastructure.Persistence;
using Dapper;

namespace CostManagement.Infrastructure.Repositories;

/// <summary>実績の永続化ポート <see cref="IActualEntryRepository"/> の Dapper/SQLite 実装。</summary>
public sealed class ActualEntryRepository : IActualEntryRepository
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>接続ファクトリを受け取る。</summary>
    public ActualEntryRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// actual_entries テーブルの1行に対応する DTO。
    /// 未使用の project_id / element_code は空文字で保存されるため <see cref="ToEntity"/> で null に読み替える。
    /// </summary>
    // SQLite の INTEGER は Int64 で返るため month は long? で受け、復元時に int? へ変換する。
    private sealed record Row(Guid Id, Guid DepartmentId, string FiscalHalf, string Category,
        string ProjectId, string ElementCode, long? Month, decimal Amount, string? Note,
        DateTime RecordedAt);

    private const string SelectSql = """
        SELECT id AS Id, department_id AS DepartmentId, fiscal_half AS FiscalHalf,
               category AS Category, project_id AS ProjectId, element_code AS ElementCode,
               month AS Month, amount AS Amount, note AS Note, recorded_at AS RecordedAt
        FROM actual_entries
        """;

    /// <inheritdoc />
    public async Task<ActualEntry?> FindByIdAsync(ActualEntryId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE id = @Id", new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActualEntry>> ListAsync(DepartmentId departmentId,
        FiscalHalf fiscalHalf, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            $"{SelectSql} WHERE department_id = @Did AND fiscal_half = @Half ORDER BY recorded_at",
            new { Did = departmentId.Value, Half = fiscalHalf.ToString() });
        return rows.Select(ToEntity).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(ActualEntry entry, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("""
            INSERT INTO actual_entries
                (id, department_id, fiscal_half, category, project_id, element_code, month, amount, note, recorded_at)
            VALUES (@Id, @DepartmentId, @FiscalHalf, @Category, @ProjectId, @ElementCode, @Month, @Amount, @Note, @RecordedAt)
            """, new
        {
            Id = entry.Id.Value,
            DepartmentId = entry.DepartmentId.Value,
            FiscalHalf = entry.FiscalHalf.ToString(),
            Category = entry.Category.ToString(),
            ProjectId = entry.ProjectId?.Value.ToString("D") ?? "",
            ElementCode = entry.ElementCode?.Value ?? "",
            entry.Month,
            Amount = entry.Amount.Value,
            entry.Note,
            entry.RecordedAt,
        });
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ActualEntryId id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM actual_entries WHERE id = @Id", new { Id = id.Value });
    }

    /// <summary>取得行を実績エンティティへ復元する(空文字の案件/費目は null に読み替える)。</summary>
    private static ActualEntry ToEntity(Row row) =>
        ActualEntry.Restore(row.Id, row.DepartmentId, row.FiscalHalf, row.Category,
            row.ProjectId == "" ? null : Guid.Parse(row.ProjectId),
            row.ElementCode == "" ? null : row.ElementCode,
            (int?)row.Month, row.Amount, row.Note, row.RecordedAt);
}

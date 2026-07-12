using CostManagement.Domain.Shared;

namespace CostManagement.Domain.Divisions;

/// <summary>部を識別する型付きID(値オブジェクト)。</summary>
public readonly record struct DivisionId(Guid Value)
{
    /// <summary>新しい一意なIDを採番する。</summary>
    public static DivisionId New() => new(Guid.NewGuid());

    /// <summary>GUID 文字列を返す。</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>課の上位組織となる部。配下の課の予実を合計して把握する単位。集約ルート。</summary>
public sealed class Division
{
    /// <summary>部ID。</summary>
    public DivisionId Id { get; }

    /// <summary>部コード(一意)。</summary>
    public string Code { get; }

    /// <summary>部名(表示用)。</summary>
    public string Name { get; private set; }

    /// <summary>作成日時(UTC)。</summary>
    public DateTime CreatedAt { get; }

    private Division(DivisionId id, string code, string name, DateTime createdAt)
    {
        Id = id;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>コード・名称を検証して新しい部を生成する。コード・名称必須。</summary>
    public static Division Create(string code, string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("部コードは必須です。");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("部名は必須です。");
        return new Division(DivisionId.New(), code.Trim(), name.Trim(), now);
    }

    /// <summary>部名を変更する。空名称は <see cref="DomainException"/>。</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("部名は必須です。");
        Name = name.Trim();
    }

    /// <summary>永続化層からの復元用ファクトリ。</summary>
    public static Division Restore(Guid id, string code, string name, DateTime createdAt) =>
        new(new DivisionId(id), code, name, createdAt);
}

/// <summary>部の永続化ポート(実装はインフラ層)。</summary>
public interface IDivisionRepository
{
    /// <summary>IDで部を1件取得する。無ければ null。</summary>
    Task<Division?> FindByIdAsync(DivisionId id, CancellationToken ct = default);

    /// <summary>コードで部を1件取得する。無ければ null。</summary>
    Task<Division?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>全ての部を取得する。</summary>
    Task<IReadOnlyList<Division>> ListAsync(CancellationToken ct = default);

    /// <summary>部を1件追加する。</summary>
    Task AddAsync(Division division, CancellationToken ct = default);

    /// <summary>部(名称など)を更新する。</summary>
    Task UpdateAsync(Division division, CancellationToken ct = default);
}

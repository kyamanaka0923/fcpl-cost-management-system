using CostManagement.Application.Common;
using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>部マスタの登録・参照ユースケース。</summary>
public sealed class DivisionService
{
    private readonly IDivisionRepository _divisions;
    private readonly ISystemClock _clock;

    /// <summary>依存する部リポジトリと時計を受け取る。</summary>
    public DivisionService(IDivisionRepository divisions, ISystemClock clock)
    {
        _divisions = divisions;
        _clock = clock;
    }

    /// <summary>部を新規登録する。部コードの重複は <see cref="DomainException"/>。</summary>
    public async Task<DivisionDto> CreateAsync(CreateDivisionRequest request,
        CancellationToken ct = default)
    {
        if (await _divisions.FindByCodeAsync(request.Code?.Trim() ?? "", ct) is not null)
            throw new DomainException($"部コードが重複しています: {request.Code}");

        var division = Division.Create(request.Code!, request.Name, _clock.UtcNow);
        await _divisions.AddAsync(division, ct);
        return ToDto(division);
    }

    /// <summary>全ての部をコード順で取得する。</summary>
    public async Task<IReadOnlyList<DivisionDto>> ListAsync(CancellationToken ct = default)
    {
        var divisions = await _divisions.ListAsync(ct);
        return divisions.OrderBy(d => d.Code).Select(ToDto).ToList();
    }

    /// <summary>IDで部を取得する。存在しなければ <see cref="NotFoundException"/>。</summary>
    public async Task<DivisionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var division = await _divisions.FindByIdAsync(new DivisionId(id), ct)
            ?? throw new NotFoundException($"部が見つかりません: {id}");
        return ToDto(division);
    }

    /// <summary>ドメインの部を応答 DTO へ変換する。</summary>
    internal static DivisionDto ToDto(Division d) =>
        new(d.Id.Value, d.Code, d.Name, d.CreatedAt);
}

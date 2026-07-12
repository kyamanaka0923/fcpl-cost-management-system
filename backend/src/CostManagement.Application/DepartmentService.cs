using CostManagement.Application.Common;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Divisions;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>課マスタ(部に属する)の登録・参照ユースケース。</summary>
public sealed class DepartmentService
{
    private readonly IDepartmentRepository _departments;
    private readonly IDivisionRepository _divisions;
    private readonly ISystemClock _clock;

    /// <summary>依存する課・部リポジトリと時計を受け取る。</summary>
    public DepartmentService(IDepartmentRepository departments, IDivisionRepository divisions,
        ISystemClock clock)
    {
        _departments = departments;
        _divisions = divisions;
        _clock = clock;
    }

    /// <summary>
    /// 指定した部に課を新規登録する。部が無ければ <see cref="NotFoundException"/>、
    /// 課コード重複は <see cref="DomainException"/>。
    /// </summary>
    public async Task<DepartmentDto> CreateAsync(Guid divisionId, CreateDepartmentRequest request,
        CancellationToken ct = default)
    {
        var divId = new DivisionId(divisionId);
        _ = await _divisions.FindByIdAsync(divId, ct)
            ?? throw new NotFoundException($"部が見つかりません: {divisionId}");
        if (await _departments.FindByCodeAsync(request.Code?.Trim() ?? "", ct) is not null)
            throw new DomainException($"課コードが重複しています: {request.Code}");

        var department = Department.Create(divId, request.Code!, request.Name, _clock.UtcNow);
        await _departments.AddAsync(department, ct);
        return ToDto(department);
    }

    /// <summary>指定した部に属する課をコード順で取得する。</summary>
    public async Task<IReadOnlyList<DepartmentDto>> ListByDivisionAsync(Guid divisionId,
        CancellationToken ct = default)
    {
        var departments = await _departments.ListByDivisionAsync(new DivisionId(divisionId), ct);
        return departments.OrderBy(d => d.Code).Select(ToDto).ToList();
    }

    /// <summary>IDで課を取得する。存在しなければ <see cref="NotFoundException"/>。</summary>
    public async Task<DepartmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var department = await _departments.FindByIdAsync(new DepartmentId(id), ct)
            ?? throw new NotFoundException($"課が見つかりません: {id}");
        return ToDto(department);
    }

    /// <summary>ドメインの課を応答 DTO へ変換する。</summary>
    internal static DepartmentDto ToDto(Department d) =>
        new(d.Id.Value, d.DivisionId.Value, d.Code, d.Name, d.CreatedAt);
}

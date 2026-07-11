using CostManagement.Application.Common;
using CostManagement.Domain.Departments;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>課マスタの登録・参照ユースケース。</summary>
public sealed class DepartmentService
{
    private readonly IDepartmentRepository _departments;
    private readonly ISystemClock _clock;

    public DepartmentService(IDepartmentRepository departments, ISystemClock clock)
    {
        _departments = departments;
        _clock = clock;
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request,
        CancellationToken ct = default)
    {
        if (await _departments.FindByCodeAsync(request.Code?.Trim() ?? "", ct) is not null)
            throw new DomainException($"課コードが重複しています: {request.Code}");

        var department = Department.Create(request.Code!, request.Name, _clock.UtcNow);
        await _departments.AddAsync(department, ct);
        return ToDto(department);
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(CancellationToken ct = default)
    {
        var departments = await _departments.ListAsync(ct);
        return departments.OrderBy(d => d.Code).Select(ToDto).ToList();
    }

    public async Task<DepartmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var department = await _departments.FindByIdAsync(new DepartmentId(id), ct)
            ?? throw new NotFoundException($"課が見つかりません: {id}");
        return ToDto(department);
    }

    internal static DepartmentDto ToDto(Department d) =>
        new(d.Id.Value, d.Code, d.Name, d.CreatedAt);
}

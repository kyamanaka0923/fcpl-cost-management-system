using CostManagement.Domain.CostElements;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>費目マスタに関するユースケース。</summary>
public sealed class CostElementService
{
    private readonly ICostElementRepository _elements;

    public CostElementService(ICostElementRepository elements)
    {
        _elements = elements;
    }

    public async Task<IReadOnlyList<CostElementDto>> ListAsync(CancellationToken ct = default) =>
        (await _elements.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<CostElementDto> CreateAsync(CreateCostElementRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<CostElementType>(request.Type, ignoreCase: true, out var type))
            throw new DomainException(
                $"費目分類が不正です: {request.Type}(Material/Labor/Overhead/Expense)");

        var code = new CostElementCode(request.Code);
        if (await _elements.FindByCodeAsync(code, ct) is not null)
            throw new DomainException($"費目コード '{code}' は既に使用されています。");

        var element = CostElement.Create(request.Code, request.Name, type, request.IsQuantityManaged);
        await _elements.AddAsync(element, ct);
        return ToDto(element);
    }

    internal static CostElementDto ToDto(CostElement e) =>
        new(e.Code.Value, e.Name, e.Type.ToString(), e.IsQuantityManaged);
}

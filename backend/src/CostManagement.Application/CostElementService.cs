using CostManagement.Application.Common;
using CostManagement.Domain.CostElements;
using CostManagement.Domain.Shared;

namespace CostManagement.Application;

/// <summary>期間費用の費目マスタの参照・登録ユースケース。</summary>
public sealed class CostElementService
{
    private readonly ICostElementRepository _elements;

    public CostElementService(ICostElementRepository elements)
    {
        _elements = elements;
    }

    public async Task<IReadOnlyList<CostElementDto>> ListAsync(CancellationToken ct = default)
    {
        var elements = await _elements.ListAsync(ct);
        return elements.OrderBy(e => e.Code.Value).Select(ToDto).ToList();
    }

    public async Task<CostElementDto> CreateAsync(CreateCostElementRequest request,
        CancellationToken ct = default)
    {
        var element = CostElement.Create(request.Code, request.Name);
        if (await _elements.FindByCodeAsync(element.Code, ct) is not null)
            throw new DomainException($"費目コードが重複しています: {element.Code.Value}");

        await _elements.AddAsync(element, ct);
        return ToDto(element);
    }

    internal static CostElementDto ToDto(CostElement e) => new(e.Code.Value, e.Name);
}

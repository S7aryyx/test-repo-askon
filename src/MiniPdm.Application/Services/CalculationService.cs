using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Models;

namespace MiniPdm.Application.Services;

public sealed class CalculationService : ICalculationService
{
    private readonly IPdmRepository _repository;

    public CalculationService(IPdmRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<BomNode>> GetTreeAsync(Guid assemblyObjectId, CancellationToken ct)
        => _repository.GetBomTreeAsync(assemblyObjectId, ct);

    public Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct)
        => _repository.GetSpecificationAsync(assemblyObjectId, ct);

    public async Task<MassCalculationResult> CalculateMassAsync(Guid assemblyObjectId, CancellationToken ct)
    {
        var assembly = await _repository.GetByIdAsync(assemblyObjectId, ct);
        if (assembly is null) return new MassCalculationResult { Error = "Сборка не найдена." };
        if (assembly.ObjectType != Domain.Enums.ObjectType.Assembly)
            return new MassCalculationResult { Error = "Выбранный объект не является сборкой." };

        var rows = await _repository.GetSpecificationAsync(assemblyObjectId, ct);
        var missing = rows.Where(x => !x.MassKg.HasValue).Select(x => x.Name).ToList();
        if (missing.Count > 0)
        {
            var result = new MassCalculationResult { Success = false, Error = "В составе есть компоненты без массы." };
            result.MissingMassItems.AddRange(missing);
            return result;
        }

        return new MassCalculationResult
        {
            Success = true,
            MassKg = rows.Sum(x => x.MassKg!.Value * x.Quantity)
        };
    }
}

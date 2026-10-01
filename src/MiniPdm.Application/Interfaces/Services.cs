using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Application.Interfaces;

public interface IImportService
{
    Task<ImportResult> ImportAsync(string folderPath, CancellationToken cancellationToken);
}

public interface ICalculationService
{
    Task<MassCalculationResult> CalculateMassAsync(Guid assemblyObjectId, CancellationToken ct);
    Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct);
    Task<IReadOnlyList<BomNode>> GetTreeAsync(Guid assemblyObjectId, CancellationToken ct);
}

public interface IVersionService
{
    Task ChangeStateAsync(Guid versionId, ObjectState newState, CancellationToken ct);
}

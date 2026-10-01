using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Application.Interfaces;

public interface IPdmRepository
{
    Task<PdmObject?> GetByIdentityAsync(ObjectType type, string? designation, string name, CancellationToken ct);
    Task<PdmObject?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ObjectVersion?> GetCurrentVersionAsync(Guid objectId, CancellationToken ct);
    Task<ObjectVersion?> GetVersionAsync(Guid versionId, CancellationToken ct);
    Task<ObjectVersion?> GetLatestNonCancelledVersionAsync(Guid objectId, CancellationToken ct);
    Task<IReadOnlyList<PdmObject>> SearchAsync(string text, CancellationToken ct);
    Task<IReadOnlyList<PdmObject>> GetAssembliesAsync(CancellationToken ct);
    Task<IReadOnlyList<PdmObject>> GetChildrenAsync(Guid parentVersionId, CancellationToken ct);
    Task<IReadOnlyList<BomLink>> GetBomLinksAsync(Guid parentVersionId, CancellationToken ct);
    Task<Guid> InsertObjectAsync(PdmObject item, CancellationToken ct);
    Task<Guid> InsertVersionAsync(ObjectVersion version, CancellationToken ct);
    Task UpdateObjectAsync(PdmObject item, CancellationToken ct);
    Task UpdateVersionAsync(ObjectVersion version, CancellationToken ct);
    Task UpdateStateAsync(Guid versionId, ObjectState state, CancellationToken ct);
    Task ReplaceBomAsync(Guid parentVersionId, IReadOnlyCollection<BomLink> links, CancellationToken ct);
    Task SetCurrentVersionAsync(Guid objectId, Guid? versionId, CancellationToken ct);
    Task<int> GetNextVersionNumberAsync(Guid objectId, CancellationToken ct);
    Task<IReadOnlyList<BomNode>> GetBomTreeAsync(Guid assemblyObjectId, CancellationToken ct);
    Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}

public interface IImportLogRepository
{
    Task AddAsync(string fileName, ImportSeverity severity, string reason, CancellationToken ct);
}

using Xunit;
using MiniPdm.Application.Interfaces;
using MiniPdm.Application.Services;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Tests;

public class VersionTests
{
    [Fact]
    public async Task ApprovedVersion_CanBeCancelled_AndPreviousVersionBecomesCurrent()
    {
        var objectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var repository = new FakeRepository
        {
            Object = new PdmObject { Id = objectId, CurrentVersionId = versionId },
            Versions = new List<ObjectVersion>
            {
                new() { Id = previousId, ObjectId = objectId, VersionNo = 1, State = ObjectState.Approved },
                new() { Id = versionId, ObjectId = objectId, VersionNo = 2, State = ObjectState.Approved }
            }
        };
        var service = new VersionService(repository, new FakeUnitOfWork());

        await service.ChangeStateAsync(versionId, ObjectState.Cancelled, CancellationToken.None);

        Assert.Equal(ObjectState.Cancelled, repository.Versions.Single(x => x.Id == versionId).State);
        Assert.Equal(previousId, repository.Object.CurrentVersionId);
    }

    [Fact]
    public async Task CancelledVersion_CannotBeApprovedAgain()
    {
        var versionId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var repository = new FakeRepository
        {
            Object = new PdmObject { Id = objectId, CurrentVersionId = versionId },
            Versions = new List<ObjectVersion>
            {
                new() { Id = versionId, ObjectId = objectId, VersionNo = 1, State = ObjectState.Cancelled }
            }
        };
        var service = new VersionService(repository, new FakeUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangeStateAsync(versionId, ObjectState.Approved, CancellationToken.None));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRepository : IPdmRepository
    {
        public PdmObject Object { get; set; } = new();
        public List<ObjectVersion> Versions { get; set; } = new();

        public Task<PdmObject?> GetByIdentityAsync(ObjectType type, string? designation, string name, CancellationToken ct) => Task.FromResult<PdmObject?>(Object);
        public Task<PdmObject?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<PdmObject?>(Object);
        public Task<ObjectVersion?> GetCurrentVersionAsync(Guid objectId, CancellationToken ct) => Task.FromResult(Versions.FirstOrDefault(x => x.Id == Object.CurrentVersionId));
        public Task<ObjectVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) => Task.FromResult(Versions.FirstOrDefault(x => x.Id == versionId));
        public Task<ObjectVersion?> GetLatestNonCancelledVersionAsync(Guid objectId, CancellationToken ct) => Task.FromResult(Versions.Where(x => x.ObjectId == objectId && x.State != ObjectState.Cancelled).OrderByDescending(x => x.VersionNo).FirstOrDefault());
        public Task<IReadOnlyList<PdmObject>> SearchAsync(string text, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<PdmObject>> GetAssembliesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<PdmObject>> GetChildrenAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<BomLink>> GetBomLinksAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BomLink>>(Array.Empty<BomLink>());
        public Task<Guid> InsertObjectAsync(PdmObject item, CancellationToken ct) => Task.FromResult(item.Id);
        public Task<Guid> InsertVersionAsync(ObjectVersion version, CancellationToken ct) => Task.FromResult(version.Id);
        public Task UpdateObjectAsync(PdmObject item, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateVersionAsync(ObjectVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateStateAsync(Guid versionId, ObjectState state, CancellationToken ct) { Versions.Single(x => x.Id == versionId).State = state; return Task.CompletedTask; }
        public Task ReplaceBomAsync(Guid parentVersionId, IReadOnlyCollection<BomLink> links, CancellationToken ct) => Task.CompletedTask;
        public Task SetCurrentVersionAsync(Guid objectId, Guid? versionId, CancellationToken ct) { Object.CurrentVersionId = versionId; return Task.CompletedTask; }
        public Task<int> GetNextVersionNumberAsync(Guid objectId, CancellationToken ct) => Task.FromResult(1);
        public Task<IReadOnlyList<BomNode>> GetBomTreeAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BomNode>>(Array.Empty<BomNode>());
        public Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SpecificationRow>>(Array.Empty<SpecificationRow>());
    }
}

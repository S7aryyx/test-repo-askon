using Xunit;
using MiniPdm.Application.Interfaces;
using MiniPdm.Application.Services;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Tests;

public class CalculationTests
{
    [Fact]
    public async Task Mass_IsCalculatedUsingTotalQuantities()
    {
        var repository = new FakeRepository
        {
            Tree = new List<BomNode> { new() { ObjectId = Guid.NewGuid(), Name = "Сборка", Level = 0 } },
            Specification = new List<SpecificationRow>
            {
                new() { Name = "B", Quantity = 2, MassKg = 2m },
                new() { Name = "C", Quantity = 3, MassKg = 3m }
            }
        };

        var service = new CalculationService(repository);
        var result = await service.CalculateMassAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(13m, result.MassKg);
    }

    [Fact]
    public async Task MissingMass_IsReportedInsteadOfReturningZero()
    {
        var repository = new FakeRepository
        {
            Tree = new List<BomNode> { new() { ObjectId = Guid.NewGuid(), Name = "Сборка", Level = 0 } },
            Specification = new List<SpecificationRow>
            {
                new() { Name = "Без массы", Quantity = 1, MassKg = null }
            }
        };

        var service = new CalculationService(repository);
        var result = await service.CalculateMassAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Без массы", result.MissingMassItems);
    }

    private sealed class FakeRepository : IPdmRepository
    {
        private readonly PdmObject _assembly = new() { Id = Guid.Empty, ObjectType = ObjectType.Assembly, Name = "Сборка" };
        public IReadOnlyList<BomNode> Tree { get; init; } = Array.Empty<BomNode>();
        public IReadOnlyList<SpecificationRow> Specification { get; init; } = Array.Empty<SpecificationRow>();
        public Task<PdmObject?> GetByIdentityAsync(ObjectType type, string? designation, string name, CancellationToken ct) => Task.FromResult<PdmObject?>(_assembly);
        public Task<PdmObject?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<PdmObject?>(_assembly);
        public Task<ObjectVersion?> GetCurrentVersionAsync(Guid objectId, CancellationToken ct) => Task.FromResult<ObjectVersion?>(null);
        public Task<ObjectVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) => Task.FromResult<ObjectVersion?>(null);
        public Task<ObjectVersion?> GetLatestNonCancelledVersionAsync(Guid objectId, CancellationToken ct) => Task.FromResult<ObjectVersion?>(null);
        public Task<IReadOnlyList<PdmObject>> SearchAsync(string text, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<PdmObject>> GetAssembliesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<PdmObject>> GetChildrenAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Array.Empty<PdmObject>());
        public Task<IReadOnlyList<BomLink>> GetBomLinksAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BomLink>>(Array.Empty<BomLink>());
        public Task<Guid> InsertObjectAsync(PdmObject item, CancellationToken ct) => Task.FromResult(item.Id);
        public Task<Guid> InsertVersionAsync(ObjectVersion version, CancellationToken ct) => Task.FromResult(version.Id);
        public Task UpdateObjectAsync(PdmObject item, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateVersionAsync(ObjectVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateStateAsync(Guid versionId, ObjectState state, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceBomAsync(Guid parentVersionId, IReadOnlyCollection<BomLink> links, CancellationToken ct) => Task.CompletedTask;
        public Task SetCurrentVersionAsync(Guid objectId, Guid? versionId, CancellationToken ct) => Task.CompletedTask;
        public Task<int> GetNextVersionNumberAsync(Guid objectId, CancellationToken ct) => Task.FromResult(1);
        public Task<IReadOnlyList<BomNode>> GetBomTreeAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult(Tree);
        public Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult(Specification);
    }
}

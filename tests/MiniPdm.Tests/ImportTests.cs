using Xunit;
using MiniPdm.Application.Interfaces;
using MiniPdm.Application.Services;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Tests;

public class ImportTests
{
    [Fact]
    public async Task Import_UsesCadReaderWithoutRealFiles()
    {
        var part = new CadDocument
        {
            FormatVersion = 1,
            FileName = "Деталь.m3d",
            Type = ObjectType.Part,
            Designation = "АБВГ.123456.001",
            Name = "Деталь",
            Properties = new CadProperties { Material = "Сталь", Mass = 1.5m }
        };

        var locator = new FakeLocator("virtual/Деталь.m3d");
        var reader = new FakeReader(new Dictionary<string, CadDocument>
        {
            ["virtual/Деталь.m3d"] = part
        });
        var repository = new FakeRepository();
        var service = new ImportService(locator, reader, repository, new FakeLogRepository(), new FakeUnitOfWork());

        var result = await service.ImportAsync("ignored", CancellationToken.None);

        Assert.Equal(1, result.AcceptedCount);
        Assert.Empty(result.Items.Where(x => x.Severity == ImportSeverity.Error));
        Assert.Single(repository.Objects);
    }

    [Fact]
    public async Task Import_RejectsAssemblyWithMissingComponent()
    {
        var assembly = new CadDocument
        {
            FormatVersion = 1,
            FileName = "Сборка.a3d",
            Type = ObjectType.Assembly,
            Designation = "АБВГ.123456.002",
            Name = "Сборка",
            Components = new List<CadComponent> { new() { File = "Нет.m3d", Count = 1 } }
        };

        var locator = new FakeLocator("Сборка.a3d");
        var reader = new FakeReader(new Dictionary<string, CadDocument> { ["Сборка.a3d"] = assembly });
        var service = new ImportService(locator, reader, new FakeRepository(), new FakeLogRepository(), new FakeUnitOfWork());

        var result = await service.ImportAsync("ignored", CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(ImportSeverity.Error, result.Items[0].Severity);
        Assert.Contains("отсутствующий файл", result.Items[0].Reason);
    }

    [Fact]
    public async Task Import_RejectsCycle()
    {
        var a = new CadDocument
        {
            FormatVersion = 1, FileName = "A.a3d", Type = ObjectType.Assembly,
            Designation = "АБВГ.123456.003", Name = "A",
            Components = new List<CadComponent> { new() { File = "B.a3d", Count = 1 } }
        };
        var b = new CadDocument
        {
            FormatVersion = 1, FileName = "B.a3d", Type = ObjectType.Assembly,
            Designation = "АБВГ.123456.004", Name = "B",
            Components = new List<CadComponent> { new() { File = "A.a3d", Count = 1 } }
        };

        var locator = new FakeLocator("A.a3d", "B.a3d");
        var reader = new FakeReader(new Dictionary<string, CadDocument> { ["A.a3d"] = a, ["B.a3d"] = b });
        var service = new ImportService(locator, reader, new FakeRepository(), new FakeLogRepository(), new FakeUnitOfWork());

        var result = await service.ImportAsync("ignored", CancellationToken.None);

        Assert.Equal(2, result.RejectedCount);
        Assert.All(result.Items, x => Assert.Contains("Циклическая", x.Reason));
    }

    private sealed class FakeLocator : ICadFileLocator
    {
        private readonly IReadOnlyList<string> _paths;
        public FakeLocator(params string[] paths) => _paths = paths;
        public IReadOnlyList<string> GetCadFiles(string folderPath) => _paths;
        public string GetFileName(string path) => Path.GetFileName(path);
    }

    private sealed class FakeReader : ICadDocumentReader
    {
        private readonly IReadOnlyDictionary<string, CadDocument> _documents;
        public FakeReader(IReadOnlyDictionary<string, CadDocument> documents) => _documents = documents;
        public Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(_documents[path]);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken ct) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeLogRepository : IImportLogRepository
    {
        public Task AddAsync(string fileName, ImportSeverity severity, string reason, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRepository : IPdmRepository
    {
        public List<PdmObject> Objects { get; } = new();
        public List<ObjectVersion> Versions { get; } = new();
        public List<BomLink> Links { get; } = new();

        public Task<PdmObject?> GetByIdentityAsync(ObjectType type, string? designation, string name, CancellationToken ct)
            => Task.FromResult(Objects.FirstOrDefault(x => x.ObjectType == type && (designation is not null ? x.Designation == designation : x.Name == name)));
        public Task<PdmObject?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Objects.FirstOrDefault(x => x.Id == id));
        public Task<ObjectVersion?> GetCurrentVersionAsync(Guid objectId, CancellationToken ct)
            => Task.FromResult(Versions.FirstOrDefault(x => x.Id == Objects.FirstOrDefault(o => o.Id == objectId)?.CurrentVersionId));
        public Task<ObjectVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) => Task.FromResult(Versions.FirstOrDefault(x => x.Id == versionId));
        public Task<IReadOnlyList<PdmObject>> SearchAsync(string text, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Objects);
        public Task<IReadOnlyList<PdmObject>> GetAssembliesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Objects.Where(x => x.ObjectType == ObjectType.Assembly).ToList());
        public Task<IReadOnlyList<PdmObject>> GetChildrenAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PdmObject>>(Objects);
        public Task<IReadOnlyList<BomLink>> GetBomLinksAsync(Guid parentVersionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BomLink>>(Links.Where(x => x.ParentVersionId == parentVersionId).ToList());
        public Task<Guid> InsertObjectAsync(PdmObject item, CancellationToken ct) { Objects.Add(item); return Task.FromResult(item.Id); }
        public Task<Guid> InsertVersionAsync(ObjectVersion version, CancellationToken ct) { Versions.Add(version); return Task.FromResult(version.Id); }
        public Task UpdateObjectAsync(PdmObject item, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateVersionAsync(ObjectVersion version, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateStateAsync(Guid versionId, ObjectState state, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceBomAsync(Guid parentVersionId, IReadOnlyCollection<BomLink> links, CancellationToken ct) { Links.RemoveAll(x => x.ParentVersionId == parentVersionId); Links.AddRange(links); return Task.CompletedTask; }
        public Task SetCurrentVersionAsync(Guid objectId, Guid? versionId, CancellationToken ct) { Objects.First(x => x.Id == objectId).CurrentVersionId = versionId; return Task.CompletedTask; }
        public Task<int> GetNextVersionNumberAsync(Guid objectId, CancellationToken ct) => Task.FromResult(Versions.Where(x => x.ObjectId == objectId).Select(x => x.VersionNo).DefaultIfEmpty().Max() + 1);
        public Task<ObjectVersion?> GetLatestNonCancelledVersionAsync(Guid objectId, CancellationToken ct) => Task.FromResult(Versions.Where(x => x.ObjectId == objectId && x.State != ObjectState.Cancelled).OrderByDescending(x => x.VersionNo).FirstOrDefault());
        public Task<IReadOnlyList<BomNode>> GetBomTreeAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BomNode>>(Array.Empty<BomNode>());
        public Task<IReadOnlyList<SpecificationRow>> GetSpecificationAsync(Guid assemblyObjectId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SpecificationRow>>(Array.Empty<SpecificationRow>());
    }
}

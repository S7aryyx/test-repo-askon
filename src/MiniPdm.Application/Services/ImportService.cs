using System.Text.Json;
using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;

namespace MiniPdm.Application.Services;

public sealed class ImportService : IImportService
{
    private readonly ICadFileLocator _fileLocator;
    private readonly ICadDocumentReader _reader;
    private readonly IPdmRepository _repository;
    private readonly IImportLogRepository _logRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportService(ICadFileLocator fileLocator, ICadDocumentReader reader, IPdmRepository repository, IImportLogRepository logRepository, IUnitOfWork unitOfWork)
    {
        _fileLocator = fileLocator;
        _reader = reader;
        _repository = repository;
        _logRepository = logRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> ImportAsync(string folderPath, CancellationToken ct)
    {
        var result = new ImportResult();
        var paths = _fileLocator.GetCadFiles(folderPath);
        var documents = new Dictionary<string, CadDocument>(StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var fileName = _fileLocator.GetFileName(path);
            try
            {
                var document = await _reader.ReadAsync(path, ct);
                document.FileName = fileName;
                documents[fileName] = document;
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
            {
                errors[fileName] = $"Ошибка чтения CAD-документа: {ex.Message}";
            }
        }

        ValidateDocuments(documents, errors);
        var availableFileNames = paths.Select(_fileLocator.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        PropagateAssemblyErrors(documents, errors, availableFileNames);

        await _unitOfWork.BeginAsync(ct);
        try
        {
            foreach (var document in OrderByDependencies(documents.Values.Where(x => !errors.ContainsKey(x.FileName)).ToList(), documents, errors))
                await ImportDocumentAsync(document, documents, ct);

            foreach (var document in documents.Values.Where(x => x.Type == ObjectType.Assembly && !errors.ContainsKey(x.FileName)))
                await ReplaceComponentsAsync(document, documents, ct);

            foreach (var path in paths)
            {
                var fileName = _fileLocator.GetFileName(path);
                if (errors.TryGetValue(fileName, out var error))
                {
                    await AddResultAsync(result, fileName, ImportSeverity.Error, error, ct);
                    continue;
                }

                var document = documents[fileName];
                var warning = document.Type == ObjectType.Part && !document.Properties.Mass.HasValue;
                var severity = warning ? ImportSeverity.Warning : ImportSeverity.Accepted;
                var reason = warning ? "Не указана масса." : "Импортирован.";
                await AddResultAsync(result, fileName, severity, reason, ct);
            }

            await _unitOfWork.CommitAsync(ct);
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void ValidateDocuments(Dictionary<string, CadDocument> documents, Dictionary<string, string> errors)
    {
        foreach (var document in documents.Values)
        {
            if (document.FormatVersion != 1)
                errors[document.FileName] = $"Неподдерживаемая версия формата: {document.FormatVersion}.";
            else if (string.IsNullOrWhiteSpace(document.Name))
                errors[document.FileName] = "Не указано наименование.";
            else if ((document.Type is ObjectType.Assembly or ObjectType.Part) && !DomainRules.IsValidDesignation(document.Designation))
                errors[document.FileName] = "Некорректное обозначение. Ожидается формат АБВГ.123456.789.";
            else if (document.Type == ObjectType.StandardPart && (!string.IsNullOrWhiteSpace(document.Designation) || !document.Properties.Mass.HasValue || document.Properties.Mass <= 0))
                errors[document.FileName] = "Для стандартного изделия обозначение отсутствует, а масса обязательна и должна быть больше нуля.";
            else if (document.Type == ObjectType.Part && string.IsNullOrWhiteSpace(document.Properties.Material))
                errors[document.FileName] = "Для детали обязательно указать материал.";
            else if (document.Type == ObjectType.Part && document.Properties.Mass is <= 0)
                errors[document.FileName] = "Масса детали должна быть больше нуля.";

            foreach (var component in document.Components)
            {
                if (component.Count <= 0)
                {
                    errors[document.FileName] = $"Количество компонента «{component.File}» должно быть больше нуля.";
                    break;
                }
            }
        }

        foreach (var group in documents.Values.Where(x => x.Type is ObjectType.Assembly or ObjectType.Part).Where(x => !string.IsNullOrWhiteSpace(x.Designation)).GroupBy(x => x.Designation!, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            foreach (var document in group) errors[document.FileName] = $"Дублируется обозначение «{document.Designation}».";

        foreach (var group in documents.Values.Where(x => x.Type == ObjectType.StandardPart).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            foreach (var document in group) errors[document.FileName] = $"Дублируется наименование стандартного изделия «{document.Name}».";

        DetectCycles(documents, errors);
    }

    private static void DetectCycles(Dictionary<string, CadDocument> documents, Dictionary<string, string> errors)
    {
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();

        foreach (var document in documents.Values.Where(x => x.Type == ObjectType.Assembly))
            Visit(document.FileName);

        void Visit(string file)
        {
            if (errors.ContainsKey(file)) return;
            if (state.TryGetValue(file, out var value))
            {
                if (value == 1)
                {
                    var start = stack.FindIndex(x => string.Equals(x, file, StringComparison.OrdinalIgnoreCase));
                    for (var i = Math.Max(0, start); i < stack.Count; i++)
                        errors[stack[i]] = "Обнаружена циклическая ссылка в составе.";
                }
                return;
            }

            state[file] = 1;
            stack.Add(file);
            foreach (var component in documents[file].Components)
            {
                if (documents.TryGetValue(component.File, out var child) && child.Type == ObjectType.Assembly)
                    Visit(child.FileName);
            }
            stack.RemoveAt(stack.Count - 1);
            state[file] = 2;
        }
    }

    private static void PropagateAssemblyErrors(Dictionary<string, CadDocument> documents, Dictionary<string, string> errors, IReadOnlySet<string> availableFileNames)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var assembly in documents.Values.Where(x => x.Type == ObjectType.Assembly && !errors.ContainsKey(x.FileName)))
            {
                foreach (var component in assembly.Components)
                {
                    if (!documents.ContainsKey(component.File))
                    {
                        errors[assembly.FileName] = availableFileNames.Contains(component.File)
                            ? $"Компонент «{component.File}» отклонён: файл не удалось прочитать."
                            : $"Ссылка на отсутствующий файл «{component.File}».";
                        changed = true;
                        break;
                    }
                    if (errors.TryGetValue(component.File, out var childError))
                    {
                        errors[assembly.FileName] = $"Компонент «{component.File}» отклонён: {childError}";
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    private async Task ImportDocumentAsync(CadDocument document, IReadOnlyDictionary<string, CadDocument> documents, CancellationToken ct)
    {
        var existing = await _repository.GetByIdentityAsync(document.Type, document.Designation, document.Name, ct);
        if (existing is null)
        {
            var version = CreateVersion(Guid.NewGuid(), 1, document);
            await _repository.InsertObjectAsync(new PdmObject { Id = version.ObjectId, ObjectType = document.Type, Designation = document.Designation, Name = document.Name, CurrentVersionId = version.Id }, ct);
            await _repository.InsertVersionAsync(version, ct);
            return;
        }

        var current = await _repository.GetCurrentVersionAsync(existing.Id, ct);
        if (current is not null && await IsSameAsync(existing, current, document, documents, ct))
            return;

        existing.Name = document.Name;
        existing.Designation = document.Designation;
        await _repository.UpdateObjectAsync(existing, ct);

        if (current is null || current.State == ObjectState.Approved || current.State == ObjectState.Cancelled)
        {
            var version = CreateVersion(existing.Id, await _repository.GetNextVersionNumberAsync(existing.Id, ct), document);
            await _repository.InsertVersionAsync(version, ct);
            await _repository.SetCurrentVersionAsync(existing.Id, version.Id, ct);
            return;
        }

        current.Material = document.Properties.Material;
        current.MassKg = document.Properties.Mass;
        await _repository.UpdateVersionAsync(current, ct);
    }

    private async Task<bool> IsSameAsync(PdmObject existing, ObjectVersion current, CadDocument document, IReadOnlyDictionary<string, CadDocument> documents, CancellationToken ct)
    {
        if (existing.ObjectType != document.Type || existing.Name != document.Name || existing.Designation != document.Designation || current.Material != document.Properties.Material || current.MassKg != document.Properties.Mass)
            return false;
        if (document.Type != ObjectType.Assembly)
            return true;

        var oldLinks = await _repository.GetBomLinksAsync(current.Id, ct);
        if (oldLinks.Count != document.Components.Count) return false;

        foreach (var component in document.Components)
        {
            var childDocument = documents[component.File];
            var child = await _repository.GetByIdentityAsync(childDocument.Type, childDocument.Designation, childDocument.Name, ct);
            if (child is null) return false;
            var link = oldLinks.FirstOrDefault(x => x.ChildObjectId == child.Id);
            if (link is null || link.Quantity != component.Count) return false;
        }
        return true;
    }

    private async Task ReplaceComponentsAsync(CadDocument document, IReadOnlyDictionary<string, CadDocument> documents, CancellationToken ct)
    {
        var parent = await _repository.GetByIdentityAsync(document.Type, document.Designation, document.Name, ct)
            ?? throw new InvalidOperationException($"Не найдена сборка «{document.Name}».");
        var current = await _repository.GetCurrentVersionAsync(parent.Id, ct)
            ?? throw new InvalidOperationException($"Не найдена текущая версия сборки «{document.Name}».");

        var links = new List<BomLink>();
        foreach (var component in document.Components)
        {
            var childDocument = documents[component.File];
            var child = await _repository.GetByIdentityAsync(childDocument.Type, childDocument.Designation, childDocument.Name, ct)
                ?? throw new InvalidOperationException($"Не найден дочерний объект «{component.File}».");
            links.Add(new BomLink { Id = Guid.NewGuid(), ParentVersionId = current.Id, ChildObjectId = child.Id, Quantity = component.Count });
        }
        await _repository.ReplaceBomAsync(current.Id, links, ct);
    }

    private static List<CadDocument> OrderByDependencies(List<CadDocument> source, IReadOnlyDictionary<string, CadDocument> documents, IReadOnlyDictionary<string, string> errors)
    {
        var result = new List<CadDocument>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in source) Add(document);
        return result;

        void Add(CadDocument document)
        {
            if (!added.Add(document.FileName)) return;
            foreach (var component in document.Components)
                if (documents.TryGetValue(component.File, out var child) && !errors.ContainsKey(child.FileName)) Add(child);
            result.Add(document);
        }
    }

    private async Task AddResultAsync(ImportResult result, string fileName, ImportSeverity severity, string reason, CancellationToken ct)
    {
        result.Items.Add(new ImportItem { FileName = fileName, Severity = severity, Reason = reason });
        await _logRepository.AddAsync(fileName, severity, reason, ct);
    }

    private static ObjectVersion CreateVersion(Guid objectId, int versionNo, CadDocument document) => new()
    {
        Id = Guid.NewGuid(), ObjectId = objectId, VersionNo = versionNo, State = ObjectState.InWork,
        Material = document.Properties.Material, MassKg = document.Properties.Mass, CreatedAt = DateTime.UtcNow
    };
}

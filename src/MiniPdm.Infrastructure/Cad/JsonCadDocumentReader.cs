using System.Text.Json;
using System.Text.Json.Serialization;
using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Models;

namespace MiniPdm.Infrastructure.Cad;

public sealed class JsonCadDocumentReader : ICadDocumentReader
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonCadDocumentReader()
    {
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<CadDocument>(stream, _options, cancellationToken);
        if (document is null)
            throw new InvalidDataException("Файл не содержит CAD-документ.");
        return document;
    }
}

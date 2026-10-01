using MiniPdm.Domain.Models;

namespace MiniPdm.Application.Interfaces;

public interface ICadDocumentReader
{
    Task<CadDocument> ReadAsync(string path, CancellationToken cancellationToken);
}

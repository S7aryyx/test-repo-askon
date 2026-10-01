using Dapper;
using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Models;

namespace MiniPdm.Infrastructure.Repositories;

public sealed class ImportLogRepository : IImportLogRepository
{
    private readonly Database.DbSession _session;

    public ImportLogRepository(Database.DbSession session)
    {
        _session = session;
    }

    public async Task AddAsync(string fileName, ImportSeverity severity, string reason, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO import_log(id, started_at, file_name, severity, reason)
            VALUES(@Id, @StartedAt, @FileName, @Severity, @Reason);
            """;
        await _session.Connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(), StartedAt = DateTime.UtcNow, FileName = fileName, Severity = severity.ToString(), Reason = reason
        }, _session.Transaction, cancellationToken: ct));
    }
}

using MiniPdm.Application.Interfaces;

namespace MiniPdm.Infrastructure.Database;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _session;

    public UnitOfWork(DbSession session)
    {
        _session = session;
    }

    public Task BeginAsync(CancellationToken ct) => _session.BeginTransactionAsync(ct);
    public Task CommitAsync(CancellationToken ct) => _session.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct) => _session.RollbackAsync(ct);
}

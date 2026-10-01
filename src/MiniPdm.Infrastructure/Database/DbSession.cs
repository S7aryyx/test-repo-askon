using Npgsql;

namespace MiniPdm.Infrastructure.Database;

public sealed class DbSession : IAsyncDisposable
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public DbSession(string connectionString) => _connectionString = connectionString;

    public NpgsqlConnection Connection => _connection ??= new NpgsqlConnection(_connectionString);
    public NpgsqlTransaction? Transaction => _transaction;

    public async Task OpenAsync(CancellationToken ct)
    {
        if (Connection.State != System.Data.ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct)
    {
        await OpenAsync(ct);
        _transaction = await Connection.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}

#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

namespace System.Data.Async;

public abstract class AsyncDbCommand : IAsyncDbCommand
{
    public abstract string CommandText { get; set; }
    public abstract int CommandTimeout { get; set; }
    public abstract CommandType CommandType { get; set; }
    public abstract IAsyncDbConnection? Connection { get; set; }
    public abstract IAsyncDbTransaction? Transaction { get; set; }
    public abstract IDataParameterCollection Parameters { get; }
    public abstract UpdateRowSource UpdatedRowSource { get; set; }
    public abstract IDbDataParameter CreateParameter();
    public abstract void Cancel();

    protected abstract ValueTask<IAsyncDataReader> ExecuteDbReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken);
    protected abstract ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask PrepareCoreAsync(CancellationToken cancellationToken);

    // Sync -> async bridge (throws on WASM)
    public IAsyncDataReader ExecuteReader() { SyncBridge.ThrowIfBrowser(nameof(ExecuteReaderAsync)); return ExecuteDbReaderAsync(CommandBehavior.Default, CancellationToken.None).GetAwaiter().GetResult(); }
    public IAsyncDataReader ExecuteReader(CommandBehavior behavior) { SyncBridge.ThrowIfBrowser(nameof(ExecuteReaderAsync)); return ExecuteDbReaderAsync(behavior, CancellationToken.None).GetAwaiter().GetResult(); }
    public int ExecuteNonQuery() { SyncBridge.ThrowIfBrowser(nameof(ExecuteNonQueryAsync)); return ExecuteNonQueryCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public object? ExecuteScalar() { SyncBridge.ThrowIfBrowser(nameof(ExecuteScalarAsync)); return ExecuteScalarCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public void Prepare() { SyncBridge.ThrowIfBrowser(nameof(PrepareAsync)); PrepareCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        => ExecuteDbReaderAsync(CommandBehavior.Default, cancellationToken);

    public ValueTask<IAsyncDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default)
        => ExecuteDbReaderAsync(behavior, cancellationToken);

    public ValueTask<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        => ExecuteNonQueryCoreAsync(cancellationToken);

    public ValueTask<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        => ExecuteScalarCoreAsync(cancellationToken);

    public ValueTask PrepareAsync(CancellationToken cancellationToken = default)
        => PrepareCoreAsync(cancellationToken);

    private bool _disposed;

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            Dispose(true);
        }

        GC.SuppressFinalize(this);
        return default;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Dispose(true);
        }

        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}

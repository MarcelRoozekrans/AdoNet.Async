#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

namespace System.Data.Async;

/// <summary>
/// Abstract base class implementing <see cref="IAsyncDbBatch"/>. Mirrors
/// <see cref="AsyncDbCommand"/>'s shape: derived classes implement async core methods
/// (<see cref="ExecuteDbReaderAsync"/>, <see cref="ExecuteNonQueryCoreAsync"/>,
/// <see cref="ExecuteScalarCoreAsync"/>, <see cref="PrepareCoreAsync"/>); the public
/// sync overloads bridge through <see cref="SyncBridge.ThrowIfBrowser"/> to keep the
/// WASM behaviour consistent.
/// </summary>
public abstract class AsyncDbBatch : IAsyncDbBatch
{
    public abstract IList<IAsyncDbBatchCommand> BatchCommands { get; }
    public abstract IAsyncDbConnection? Connection { get; set; }
    public abstract IAsyncDbTransaction? Transaction { get; set; }
    public abstract int Timeout { get; set; }
    public abstract IAsyncDbBatchCommand CreateBatchCommand();
    public abstract void Cancel();

    protected abstract ValueTask<IAsyncDataReader> ExecuteDbReaderAsync(CancellationToken cancellationToken);
    protected abstract ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask PrepareCoreAsync(CancellationToken cancellationToken);

    // Sync -> async bridge (throws on WASM)
    public IAsyncDataReader ExecuteReader() { SyncBridge.ThrowIfBrowser(nameof(ExecuteReaderAsync)); return ExecuteDbReaderAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public int ExecuteNonQuery() { SyncBridge.ThrowIfBrowser(nameof(ExecuteNonQueryAsync)); return ExecuteNonQueryCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public object? ExecuteScalar() { SyncBridge.ThrowIfBrowser(nameof(ExecuteScalarAsync)); return ExecuteScalarCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public void Prepare() { SyncBridge.ThrowIfBrowser(nameof(PrepareAsync)); PrepareCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        => ExecuteDbReaderAsync(cancellationToken);

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

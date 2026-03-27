#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

namespace System.Data.Async;

public abstract class AsyncDbTransaction : IAsyncDbTransaction
{
    public abstract IAsyncDbConnection Connection { get; }
    public abstract IsolationLevel IsolationLevel { get; }

    protected abstract ValueTask CommitCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask RollbackCoreAsync(CancellationToken cancellationToken);

    // Sync -> async bridge (throws on WASM)
    public void Commit() { SyncBridge.ThrowIfBrowser(nameof(CommitAsync)); CommitCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public void Rollback() { SyncBridge.ThrowIfBrowser(nameof(RollbackAsync)); RollbackCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask CommitAsync(CancellationToken cancellationToken = default) => CommitCoreAsync(cancellationToken);
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default) => RollbackCoreAsync(cancellationToken);

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await DisposeAsyncCore().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => default;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            SyncBridge.ThrowIfBrowser(nameof(DisposeAsync));
            DisposeAsyncCore().GetAwaiter().GetResult();
        }

        GC.SuppressFinalize(this);
    }
}

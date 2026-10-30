#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

namespace System.Data.Async;

public abstract class AsyncDbTransaction : IAsyncDbTransaction
{
    public abstract IAsyncDbConnection Connection { get; }
    public abstract IsolationLevel IsolationLevel { get; }

    protected abstract ValueTask CommitCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask RollbackCoreAsync(CancellationToken cancellationToken);

    // Sync members delegate to a virtual core. The defaults bridge to the async core and throw on
    // WASM; a transaction over a provider with native sync members overrides them to call those.
    public void Commit() => CommitCore();
    public void Rollback() => RollbackCore();

    protected virtual void CommitCore() { SyncBridge.ThrowIfBrowser(nameof(CommitAsync)); CommitCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    protected virtual void RollbackCore() { SyncBridge.ThrowIfBrowser(nameof(RollbackAsync)); RollbackCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask CommitAsync(CancellationToken cancellationToken = default) => CommitCoreAsync(cancellationToken);
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default) => RollbackCoreAsync(cancellationToken);

    private bool _disposed;

    // The core's task goes straight to the caller: awaiting it here would allocate a state
    // machine whenever the core completes asynchronously.
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_disposed)
        {
            return default;
        }

        _disposed = true;
        return DisposeAsyncCore();
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

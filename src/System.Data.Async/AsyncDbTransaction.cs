#pragma warning disable CA2012 // Use ValueTasks correctly -- sync-to-async bridge by design

namespace System.Data.Async;

public abstract class AsyncDbTransaction : IAsyncDbTransaction
{
    public abstract IAsyncDbConnection Connection { get; }
    public abstract IsolationLevel IsolationLevel { get; }

    protected abstract ValueTask CommitCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask RollbackCoreAsync(CancellationToken cancellationToken);

    public void Commit() => CommitCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    public void Rollback() => RollbackCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
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
            DisposeAsyncCore().GetAwaiter().GetResult();
        }

        GC.SuppressFinalize(this);
    }
}

#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

namespace System.Data.Async;

public abstract class AsyncDbConnection : IAsyncDbConnection
{
    public abstract string ConnectionString { get; set; }
    public abstract string Database { get; }
    public abstract ConnectionState State { get; }
    public abstract int ConnectionTimeout { get; }

    protected abstract ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken);
    protected abstract ValueTask OpenCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask CloseCoreAsync();
    protected abstract ValueTask ChangeDatabaseCoreAsync(string databaseName, CancellationToken cancellationToken);
    protected abstract IAsyncDbCommand CreateDbCommand();

    /// <summary>
    /// Default to <c>false</c> — providers that support batches override this to forward
    /// <see cref="System.Data.Common.DbConnection.CanCreateBatch"/>.
    /// </summary>
    public virtual bool CanCreateBatch => false;

    /// <summary>
    /// Default implementation throws <see cref="NotSupportedException"/>. Providers that
    /// support batches override to construct a concrete <see cref="IAsyncDbBatch"/>.
    /// </summary>
    public virtual IAsyncDbBatch CreateBatch()
        => throw new NotSupportedException(
            "This provider does not support batches. Check IAsyncDbConnection.CanCreateBatch before calling CreateBatch.");

    // Sync -> async bridge (throws on WASM)
    public IAsyncDbTransaction BeginTransaction() => BeginDbTransaction(IsolationLevel.Unspecified);
    public IAsyncDbTransaction BeginTransaction(IsolationLevel il) => BeginDbTransaction(il);
    public void ChangeDatabase(string databaseName) => ChangeDatabaseCore(databaseName);
    public IAsyncDbCommand CreateCommand() => CreateDbCommand();
    public void Open() => OpenCore();
    public void Close() => CloseCore();

    // Sync members delegate to a virtual core. The defaults bridge to the async core and throw on
    // WASM; a connection over a provider with native sync members overrides them to call those.
    protected virtual IAsyncDbTransaction BeginDbTransaction(IsolationLevel isolationLevel) { SyncBridge.ThrowIfBrowser(nameof(BeginTransactionAsync)); return BeginDbTransactionAsync(isolationLevel, CancellationToken.None).GetAwaiter().GetResult(); }
    protected virtual void ChangeDatabaseCore(string databaseName) { SyncBridge.ThrowIfBrowser(nameof(ChangeDatabaseAsync)); ChangeDatabaseCoreAsync(databaseName, CancellationToken.None).GetAwaiter().GetResult(); }
    protected virtual void OpenCore() { SyncBridge.ThrowIfBrowser(nameof(OpenAsync)); OpenCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    protected virtual void CloseCore() { SyncBridge.ThrowIfBrowser(nameof(CloseAsync)); CloseCoreAsync().GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => BeginDbTransactionAsync(IsolationLevel.Unspecified, cancellationToken);

    public ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default)
        => BeginDbTransactionAsync(il, cancellationToken);

    public ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
        => ChangeDatabaseCoreAsync(databaseName, cancellationToken);

    public ValueTask OpenAsync(CancellationToken cancellationToken = default) => OpenCoreAsync(cancellationToken);
    public ValueTask CloseAsync() => CloseCoreAsync();

    // A derived class that owns a resource overrides DisposeAsyncCore and Dispose(bool), so the
    // resource is released whichever static type the caller disposes through. The core's task
    // goes straight to the caller: awaiting it here would allocate a state machine whenever the
    // core completes asynchronously. There is no finalizer, so the async path has no
    // Dispose(false) step to run afterwards.
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return DisposeAsyncCore();
    }

    /// <summary>
    /// Releases the connection asynchronously. The default closes it if it is not closed. A
    /// derived class that wraps another connection overrides this to dispose that connection.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
        => State == ConnectionState.Closed ? default : CloseCoreAsync();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the connection. When <paramref name="disposing"/> is <see langword="true"/> the
    /// default closes it if it is not closed. A derived class that wraps another connection
    /// overrides this to dispose that connection.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && State != ConnectionState.Closed)
        {
            Close();
        }
    }
}

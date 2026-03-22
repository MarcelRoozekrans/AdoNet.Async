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

    // Sync -> async bridge (throws on WASM)
    public IAsyncDbTransaction BeginTransaction() { SyncBridge.ThrowIfBrowser(nameof(BeginTransactionAsync)); return BeginDbTransactionAsync(IsolationLevel.Unspecified, CancellationToken.None).GetAwaiter().GetResult(); }
    public IAsyncDbTransaction BeginTransaction(IsolationLevel il) { SyncBridge.ThrowIfBrowser(nameof(BeginTransactionAsync)); return BeginDbTransactionAsync(il, CancellationToken.None).GetAwaiter().GetResult(); }
    public void ChangeDatabase(string databaseName) { SyncBridge.ThrowIfBrowser(nameof(ChangeDatabaseAsync)); ChangeDatabaseCoreAsync(databaseName, CancellationToken.None).GetAwaiter().GetResult(); }
    public IAsyncDbCommand CreateCommand() => CreateDbCommand();
    public void Open() { SyncBridge.ThrowIfBrowser(nameof(OpenAsync)); OpenCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public void Close() { SyncBridge.ThrowIfBrowser(nameof(CloseAsync)); CloseCoreAsync().GetAwaiter().GetResult(); }

    // Async delegates to core
    public ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => BeginDbTransactionAsync(IsolationLevel.Unspecified, cancellationToken);

    public ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default)
        => BeginDbTransactionAsync(il, cancellationToken);

    public ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
        => ChangeDatabaseCoreAsync(databaseName, cancellationToken);

    public ValueTask OpenAsync(CancellationToken cancellationToken = default) => OpenCoreAsync(cancellationToken);
    public ValueTask CloseAsync() => CloseCoreAsync();

    public async ValueTask DisposeAsync()
    {
        if (State != ConnectionState.Closed)
        {
            await CloseCoreAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (State != ConnectionState.Closed)
        {
            Close();
        }

        GC.SuppressFinalize(this);
    }
}

using System.Data.Common;
using System.Runtime.CompilerServices;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbConnection : AsyncDbConnection
{
    private readonly DbConnection _inner;

    public AdapterDbConnection(DbConnection inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    internal DbConnection InnerConnection => _inner;
    public static explicit operator DbConnection(AdapterDbConnection connection) => connection._inner;

    public override string ConnectionString
    {
        get => _inner.ConnectionString!;
        set => _inner.ConnectionString = value;
    }

    public override string Database => _inner.Database;
    public override ConnectionState State => _inner.State;
    public override int ConnectionTimeout => _inner.ConnectionTimeout;

    // Pure forwarders hand the provider's task straight to the caller; see AdapterDbDataReader.
    protected override ValueTask OpenCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask(_inner.OpenAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    protected override ValueTask CloseCoreAsync()
    {
        try
        {
            return new ValueTask(_inner.CloseAsync());
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    protected override ValueTask ChangeDatabaseCoreAsync(string databaseName, CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask(_inner.ChangeDatabaseAsync(databaseName, cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    // Wrapping the provider's result needs an await. The pooling builder reuses the state machine
    // box, so a yielding call allocates only the adapter object. Its ValueTask must be awaited once
    // and never blocked on before it completes; the sync members call the provider directly.
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    protected override async ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => new AdapterDbTransaction(await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false), this);

    protected override IAsyncDbCommand CreateDbCommand()
        => new AdapterDbCommand(_inner.CreateCommand(), this);

    public override bool CanCreateBatch => _inner.CanCreateBatch;

    public override IAsyncDbBatch CreateBatch()
        => new AdapterDbBatch(_inner.CreateBatch(), this);

    // Sync members call the provider's own instead of the base class's sync-over-async bridge.
    protected override void OpenCore() => _inner.Open();
    protected override void CloseCore() => _inner.Close();
    protected override void ChangeDatabaseCore(string databaseName) => _inner.ChangeDatabase(databaseName);

    protected override IAsyncDbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => new AdapterDbTransaction(_inner.BeginTransaction(isolationLevel), this);

    // Dispose the provider's connection, not just close it. When that completes synchronously the
    // base call runs and finds the connection closed, since State reads the provider's
    // connection. When it yields, the provider's task goes straight to the caller and the base
    // call is skipped: all it could do is close the provider's connection, which disposing
    // already closes. Awaiting the dispose and then the base call would box a state machine on
    // every yield.
    protected override ValueTask DisposeAsyncCore()
        => ProviderTask.TryDisposeSynchronously(_inner, out var pending)
            ? base.DisposeAsyncCore()
            : pending;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

using System.Data.Common;

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

    protected override async ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => new AdapterDbTransaction(await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false), this);

    protected override IAsyncDbCommand CreateDbCommand()
        => new AdapterDbCommand(_inner.CreateCommand(), this);

    public override bool CanCreateBatch => _inner.CanCreateBatch;

    public override IAsyncDbBatch CreateBatch()
        => new AdapterDbBatch(_inner.CreateBatch(), this);

    // Sync methods using native _inner (hide base class sync-over-async bridge)
    public new void Open() => _inner.Open();
    public new void Close() => _inner.Close();

    // Dispose the provider's connection, not just close it. The base call that follows finds the
    // connection closed and does nothing more.
    protected override async ValueTask DisposeAsyncCore()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

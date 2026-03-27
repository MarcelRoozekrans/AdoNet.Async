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

    protected override async ValueTask OpenCoreAsync(CancellationToken cancellationToken)
        => await _inner.OpenAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask CloseCoreAsync()
        => await _inner.CloseAsync().ConfigureAwait(false);

    protected override async ValueTask ChangeDatabaseCoreAsync(string databaseName, CancellationToken cancellationToken)
        => await _inner.ChangeDatabaseAsync(databaseName, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => new AdapterDbTransaction(await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false), this);

    protected override IAsyncDbCommand CreateDbCommand()
        => new AdapterDbCommand(_inner.CreateCommand(), this);

    // Sync methods using native _inner (hide base class sync-over-async bridge)
    public new void Open() => _inner.Open();
    public new void Close() => _inner.Close();

    // Override to properly dispose the inner connection
#pragma warning disable CA1816 // Hiding base Dispose to delegate to inner connection
    public new async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public new void Dispose()
    {
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }
#pragma warning restore CA1816
}

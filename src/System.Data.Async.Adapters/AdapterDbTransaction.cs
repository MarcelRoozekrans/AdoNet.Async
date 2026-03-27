using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbTransaction : AsyncDbTransaction
{
    private readonly DbTransaction _inner;
    private readonly AdapterDbConnection _connection;

    public AdapterDbTransaction(DbTransaction inner, AdapterDbConnection connection)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    internal DbTransaction InnerTransaction => _inner;
    public static explicit operator DbTransaction(AdapterDbTransaction transaction) => transaction._inner;

    public override IAsyncDbConnection Connection => _connection;
    public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

    protected override async ValueTask CommitCoreAsync(CancellationToken cancellationToken)
        => await _inner.CommitAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask RollbackCoreAsync(CancellationToken cancellationToken)
        => await _inner.RollbackAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask DisposeAsyncCore()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    // Sync methods using native _inner (hide base class sync-over-async bridge)
    public new void Commit() => _inner.Commit();
    public new void Rollback() => _inner.Rollback();
}

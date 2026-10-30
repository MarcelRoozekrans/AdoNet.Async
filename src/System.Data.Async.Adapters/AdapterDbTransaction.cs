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

    // Pure forwarders hand the provider's task straight to the caller; see AdapterDbDataReader.
    protected override ValueTask CommitCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask(_inner.CommitAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    protected override ValueTask RollbackCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask(_inner.RollbackAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    // Dispose the provider's transaction. When that yields, the provider's task goes straight to
    // the caller and the base call is skipped: the base core does nothing. Awaiting the dispose
    // and then the base call would box a state machine on every yield.
    protected override ValueTask DisposeAsyncCore()
        => ProviderTask.TryDisposeSynchronously(_inner, out var pending)
            ? base.DisposeAsyncCore()
            : pending;

    // Sync members call the provider's own instead of the base class's sync-over-async bridge.
    protected override void CommitCore() => _inner.Commit();
    protected override void RollbackCore() => _inner.Rollback();
}

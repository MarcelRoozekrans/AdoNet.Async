using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbCommand : AsyncDbCommand
{
    private readonly DbCommand _inner;
    private AdapterDbConnection? _connection;
    private AdapterDbTransaction? _transaction;

    public AdapterDbCommand(DbCommand inner, AdapterDbConnection? connection)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _connection = connection;

        if (connection is not null)
        {
            _inner.Connection = connection.InnerConnection;
        }
    }

    public DbCommand InnerCommand => _inner;

    public override string CommandText
    {
        get => _inner.CommandText;
        set => _inner.CommandText = value;
    }

    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    public override IAsyncDbConnection? Connection
    {
        get => _connection;
        set
        {
            _connection = value as AdapterDbConnection;
            _inner.Connection = _connection?.InnerConnection;
        }
    }

    public override IAsyncDbTransaction? Transaction
    {
        get => _transaction;
        set
        {
            _transaction = value as AdapterDbTransaction;
            _inner.Transaction = _transaction?.InnerTransaction;
        }
    }

    public override IDataParameterCollection Parameters => _inner.Parameters;
    public override UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    public override IDbDataParameter CreateParameter() => _inner.CreateParameter();
    public override void Cancel() => _inner.Cancel();

    protected override async ValueTask<IAsyncDataReader> ExecuteDbReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        => new AdapterDbDataReader(await _inner.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false));

    protected override async ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken)
        => await _inner.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken)
        => await _inner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask PrepareCoreAsync(CancellationToken cancellationToken)
        => await _inner.PrepareAsync(cancellationToken).ConfigureAwait(false);

    // Sync methods using native _inner (hide base class sync-over-async bridge)
    public new IAsyncDataReader ExecuteReader() => new AdapterDbDataReader(_inner.ExecuteReader());
    public new IAsyncDataReader ExecuteReader(CommandBehavior behavior) => new AdapterDbDataReader(_inner.ExecuteReader(behavior));
    public new int ExecuteNonQuery() => _inner.ExecuteNonQuery();
    public new object? ExecuteScalar() => _inner.ExecuteScalar();
    public new void Prepare() => _inner.Prepare();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

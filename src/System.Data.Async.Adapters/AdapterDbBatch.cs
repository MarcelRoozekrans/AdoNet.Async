using System.Collections;
using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbBatch : AsyncDbBatch
{
    private readonly DbBatch _inner;
    private AdapterDbConnection? _connection;
    private AdapterDbTransaction? _transaction;
    private BatchCommandList? _commands;

    public AdapterDbBatch(DbBatch inner, AdapterDbConnection? connection)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _connection = connection;
    }

    internal DbBatch InnerBatch => _inner;
    public static explicit operator DbBatch(AdapterDbBatch batch) => batch._inner;

    public override IList<IAsyncDbBatchCommand> BatchCommands
        => _commands ??= new BatchCommandList(_inner);

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

    public override int Timeout
    {
        get => _inner.Timeout;
        set => _inner.Timeout = value;
    }

    public override IAsyncDbBatchCommand CreateBatchCommand()
        => new AdapterDbBatchCommand(_inner.CreateBatchCommand());

    public override void Cancel() => _inner.Cancel();

    protected override async ValueTask<IAsyncDataReader> ExecuteDbReaderAsync(CancellationToken cancellationToken)
        => new AdapterDbDataReader(await _inner.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false));

    protected override async ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken)
        => await _inner.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken)
        => await _inner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask PrepareCoreAsync(CancellationToken cancellationToken)
        => await _inner.PrepareAsync(cancellationToken).ConfigureAwait(false);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Pass-through wrapper around <see cref="DbBatch.BatchCommands"/>. The BCL surface
    /// is <c>DbBatchCommandCollection</c> (which is itself <c>IList&lt;DbBatchCommand&gt;</c>);
    /// we only re-shape it as <c>IList&lt;IAsyncDbBatchCommand&gt;</c> by wrapping each entry
    /// in an <see cref="AdapterDbBatchCommand"/> on read. Add/Insert accept any
    /// <see cref="IAsyncDbBatchCommand"/> and unwrap to the underlying <c>DbBatchCommand</c>.
    /// </summary>
    private sealed class BatchCommandList : IList<IAsyncDbBatchCommand>
    {
        private readonly DbBatch _batch;

        public BatchCommandList(DbBatch batch)
        {
            _batch = batch;
        }

        public IAsyncDbBatchCommand this[int index]
        {
            get => new AdapterDbBatchCommand(_batch.BatchCommands[index]);
            set => _batch.BatchCommands[index] = Unwrap(value);
        }

        public int Count => _batch.BatchCommands.Count;
        public bool IsReadOnly => _batch.BatchCommands.IsReadOnly;

        public void Add(IAsyncDbBatchCommand item) => _batch.BatchCommands.Add(Unwrap(item));
        public void Clear() => _batch.BatchCommands.Clear();
        public bool Contains(IAsyncDbBatchCommand item) => _batch.BatchCommands.Contains(Unwrap(item));

        public void CopyTo(IAsyncDbBatchCommand[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            for (var i = 0; i < _batch.BatchCommands.Count; i++)
            {
                array[arrayIndex + i] = new AdapterDbBatchCommand(_batch.BatchCommands[i]);
            }
        }

        public int IndexOf(IAsyncDbBatchCommand item) => _batch.BatchCommands.IndexOf(Unwrap(item));
        public void Insert(int index, IAsyncDbBatchCommand item) => _batch.BatchCommands.Insert(index, Unwrap(item));
        public bool Remove(IAsyncDbBatchCommand item) => _batch.BatchCommands.Remove(Unwrap(item));
        public void RemoveAt(int index) => _batch.BatchCommands.RemoveAt(index);

        // HLQ006 nudges toward a struct enumerator for hot-path iteration; the batch-
        // commands collection is rare-path (typically iterated once at submit time), so
        // a compiler-generated reference enumerator is fine here.
#pragma warning disable HLQ006
        public IEnumerator<IAsyncDbBatchCommand> GetEnumerator()
        {
            foreach (var raw in _batch.BatchCommands)
            {
                yield return new AdapterDbBatchCommand(raw);
            }
        }
#pragma warning restore HLQ006

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static DbBatchCommand Unwrap(IAsyncDbBatchCommand item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return item is AdapterDbBatchCommand wrapped
                ? wrapped.InnerCommand
                : throw new ArgumentException(
                    "Only AdapterDbBatchCommand instances are accepted by AdapterDbBatch.BatchCommands. " +
                    "Use IAsyncDbBatch.CreateBatchCommand() to construct commands compatible with this batch.",
                    nameof(item));
        }
    }
}

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

    // Pure forwarders hand the provider's task straight to the caller; see AdapterDbDataReader.
    protected override ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<int>(_inner.ExecuteNonQueryAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<int>(ex);
        }
    }

    protected override ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<object?>(_inner.ExecuteScalarAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<object?>(ex);
        }
    }

    protected override ValueTask PrepareCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask(_inner.PrepareAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown(ex);
        }
    }

    // Sync members call the provider's own instead of the base class's sync-over-async bridge.
    protected override IAsyncDataReader ExecuteDbReader() => new AdapterDbDataReader(_inner.ExecuteReader());
    protected override int ExecuteNonQueryCore() => _inner.ExecuteNonQuery();
    protected override object? ExecuteScalarCore() => _inner.ExecuteScalar();
    protected override void PrepareCore() => _inner.Prepare();

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
    /// we only re-shape it as <c>IList&lt;IAsyncDbBatchCommand&gt;</c>. Each entry is wrapped in an
    /// <see cref="AdapterDbBatchCommand"/> once and the wrapper is kept alongside its position, so
    /// reading an entry hands back the same instance every time, including the instance that was
    /// added, without allocating. Add/Insert accept any <see cref="IAsyncDbBatchCommand"/> and
    /// unwrap to the underlying <c>DbBatchCommand</c>.
    /// </summary>
    private sealed class BatchCommandList : IList<IAsyncDbBatchCommand>
    {
        private readonly DbBatch _batch;

        // Wrappers by position, mirroring _batch.BatchCommands. Every mutation made through this
        // list keeps it aligned. A change made straight on the provider's collection can shift
        // entries, so a read checks that the cached wrapper still wraps the command at that
        // position and re-wraps it when it does not.
        private readonly List<AdapterDbBatchCommand?> _wrappers = [];

        public BatchCommandList(DbBatch batch)
        {
            _batch = batch;
        }

        public IAsyncDbBatchCommand this[int index]
        {
            get => Wrap(index);
            set
            {
                var wrapped = Cast(value);
                _batch.BatchCommands[index] = wrapped.InnerCommand;
                Store(index, wrapped);
            }
        }

        public int Count => _batch.BatchCommands.Count;
        public bool IsReadOnly => _batch.BatchCommands.IsReadOnly;

        public void Add(IAsyncDbBatchCommand item)
        {
            var wrapped = Cast(item);
            _batch.BatchCommands.Add(wrapped.InnerCommand);
            Store(_batch.BatchCommands.Count - 1, wrapped);
        }

        public void Clear()
        {
            _batch.BatchCommands.Clear();
            _wrappers.Clear();
        }

        public bool Contains(IAsyncDbBatchCommand item) => _batch.BatchCommands.Contains(Cast(item).InnerCommand);

        public void CopyTo(IAsyncDbBatchCommand[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            for (var i = 0; i < _batch.BatchCommands.Count; i++)
            {
                array[arrayIndex + i] = Wrap(i);
            }
        }

        public int IndexOf(IAsyncDbBatchCommand item) => _batch.BatchCommands.IndexOf(Cast(item).InnerCommand);

        public void Insert(int index, IAsyncDbBatchCommand item)
        {
            var wrapped = Cast(item);
            _batch.BatchCommands.Insert(index, wrapped.InnerCommand);
            while (_wrappers.Count < index)
            {
                _wrappers.Add(null);
            }

            _wrappers.Insert(index, wrapped);
        }

        public bool Remove(IAsyncDbBatchCommand item)
        {
            var index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            _batch.BatchCommands.RemoveAt(index);
            if (index < _wrappers.Count)
            {
                _wrappers.RemoveAt(index);
            }
        }

        // HLQ006 nudges toward a struct enumerator for hot-path iteration; the batch-
        // commands collection is rare-path (typically iterated once at submit time), so
        // a compiler-generated reference enumerator is fine here.
#pragma warning disable HLQ006
        public IEnumerator<IAsyncDbBatchCommand> GetEnumerator()
        {
            for (var i = 0; i < _batch.BatchCommands.Count; i++)
            {
                yield return Wrap(i);
            }
        }
#pragma warning restore HLQ006

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private AdapterDbBatchCommand Wrap(int index)
        {
            var raw = _batch.BatchCommands[index];
            if (index < _wrappers.Count && _wrappers[index] is { } cached && ReferenceEquals(cached.InnerCommand, raw))
            {
                return cached;
            }

            Resync();
            return _wrappers[index]!;
        }

        // Rebuilds the mirror after the provider's collection changed behind this list, reusing
        // the wrapper already handed out for each command so identities stay stable.
        private void Resync()
        {
            var previous = _wrappers.ToArray();
            var commands = _batch.BatchCommands;
            _wrappers.Clear();
            for (var i = 0; i < commands.Count; i++)
            {
                var raw = commands[i];
                AdapterDbBatchCommand? existing = null;
                foreach (var candidate in previous)
                {
                    if (candidate is not null && ReferenceEquals(candidate.InnerCommand, raw))
                    {
                        existing = candidate;
                        break;
                    }
                }

                _wrappers.Add(existing ?? new AdapterDbBatchCommand(raw));
            }
        }

        private void Store(int index, AdapterDbBatchCommand wrapped)
        {
            while (_wrappers.Count <= index)
            {
                _wrappers.Add(null);
            }

            _wrappers[index] = wrapped;
        }

        private static AdapterDbBatchCommand Cast(IAsyncDbBatchCommand item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return item as AdapterDbBatchCommand
                ?? throw new ArgumentException(
                    "Only AdapterDbBatchCommand instances are accepted by AdapterDbBatch.BatchCommands. " +
                    "Use IAsyncDbBatch.CreateBatchCommand() to construct commands compatible with this batch.",
                    nameof(item));
        }
    }
}

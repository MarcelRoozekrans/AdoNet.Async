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
    /// we only re-shape it as <c>IList&lt;IAsyncDbBatchCommand&gt;</c>. Add/Insert accept any
    /// <see cref="IAsyncDbBatchCommand"/> and unwrap to the underlying <c>DbBatchCommand</c>,
    /// storing nothing of their own, so building a batch costs only what the provider allocates.
    /// The first read wraps each entry in an <see cref="AdapterDbBatchCommand"/> and keeps the
    /// wrapper alongside its position, so every later read hands back that same instance without
    /// allocating. IndexOf, Contains and Remove compare the underlying provider commands, so they
    /// accept the instance that was added as well as the one a read returned.
    /// </summary>
    private sealed class BatchCommandList : IList<IAsyncDbBatchCommand>
    {
        // The provider's batch until the first read, then a WrapperCache over it. One field keeps
        // this list as small as it is without a cache, so a batch that is built, run and disposed
        // without being read back allocates nothing here beyond the list itself.
        private object _state;

        public BatchCommandList(DbBatch batch)
        {
            _state = batch;
        }

        private DbBatchCommandCollection Commands
            => (_state is WrapperCache cache ? cache.Batch : (DbBatch)_state).BatchCommands;

        public IAsyncDbBatchCommand this[int index]
        {
            get => Wrap(index);
            set
            {
                var wrapped = Cast(value);
                Commands[index] = wrapped.InnerCommand;
                if (_state is WrapperCache cache)
                {
                    cache.Store(index, wrapped);
                }
            }
        }

        public int Count => Commands.Count;
        public bool IsReadOnly => Commands.IsReadOnly;

        public void Add(IAsyncDbBatchCommand item)
        {
            var wrapped = Cast(item);
            var commands = Commands;
            commands.Add(wrapped.InnerCommand);
            if (_state is WrapperCache cache)
            {
                cache.Store(commands.Count - 1, wrapped);
            }
        }

        public void Clear()
        {
            Commands.Clear();
            if (_state is WrapperCache cache)
            {
                cache.Wrappers.Clear();
            }
        }

        public bool Contains(IAsyncDbBatchCommand item) => Commands.Contains(Cast(item).InnerCommand);

        public void CopyTo(IAsyncDbBatchCommand[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            for (var i = 0; i < Commands.Count; i++)
            {
                array[arrayIndex + i] = Wrap(i);
            }
        }

        public int IndexOf(IAsyncDbBatchCommand item) => Commands.IndexOf(Cast(item).InnerCommand);

        public void Insert(int index, IAsyncDbBatchCommand item)
        {
            var wrapped = Cast(item);
            Commands.Insert(index, wrapped.InnerCommand);
            if (_state is WrapperCache cache)
            {
                while (cache.Wrappers.Count < index)
                {
                    cache.Wrappers.Add(null);
                }

                cache.Wrappers.Insert(index, wrapped);
            }
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
            Commands.RemoveAt(index);
            if (_state is WrapperCache cache && index < cache.Wrappers.Count)
            {
                cache.Wrappers.RemoveAt(index);
            }
        }

        // HLQ006 nudges toward a struct enumerator for hot-path iteration; the batch-
        // commands collection is rare-path (typically iterated once at submit time), so
        // a compiler-generated reference enumerator is fine here.
#pragma warning disable HLQ006
        public IEnumerator<IAsyncDbBatchCommand> GetEnumerator()
        {
            for (var i = 0; i < Commands.Count; i++)
            {
                yield return Wrap(i);
            }
        }
#pragma warning restore HLQ006

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private AdapterDbBatchCommand Wrap(int index)
        {
            if (_state is not WrapperCache cache)
            {
                cache = new WrapperCache((DbBatch)_state);
                _state = cache;
            }

            var raw = cache.Batch.BatchCommands[index];
            var wrappers = cache.Wrappers;
            if (index < wrappers.Count && wrappers[index] is { } cached && ReferenceEquals(cached.InnerCommand, raw))
            {
                return cached;
            }

            cache.Resync();
            return wrappers[index]!;
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

    // The wrappers handed out by reads, by position, mirroring the provider's BatchCommands. Every
    // mutation made through BatchCommandList keeps it aligned. A change made straight on the
    // provider's collection can shift entries, so a read checks that the cached wrapper still
    // wraps the command at that position and resyncs when it does not.
    private sealed class WrapperCache
    {
        public WrapperCache(DbBatch batch)
        {
            Batch = batch;
        }

        public DbBatch Batch { get; }

        public List<AdapterDbBatchCommand?> Wrappers { get; } = [];

        public void Store(int index, AdapterDbBatchCommand wrapped)
        {
            while (Wrappers.Count <= index)
            {
                Wrappers.Add(null);
            }

            Wrappers[index] = wrapped;
        }

        // Rebuilds the mirror after the provider's collection changed, reusing the wrapper
        // already handed out for each command so identities stay stable.
        public void Resync()
        {
            var previous = Wrappers.ToArray();
            var commands = Batch.BatchCommands;
            Wrappers.Clear();
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

                Wrappers.Add(existing ?? new AdapterDbBatchCommand(raw));
            }
        }
    }
}

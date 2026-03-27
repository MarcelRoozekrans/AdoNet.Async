#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge
#pragma warning disable HLQ006 // GetAsyncEnumerator returns reference type -- async iterators require compiler-generated state machines

namespace System.Data.Async;

public abstract class AsyncDbDataReader : IAsyncDataReader
{
    // Abstract properties
    public abstract int FieldCount { get; }
    public abstract int Depth { get; }
    public abstract bool IsClosed { get; }
    public abstract int RecordsAffected { get; }
    public abstract bool HasRows { get; }

    // Abstract indexers
    public abstract object this[int i] { get; }
    public abstract object this[string name] { get; }

    // Abstract Get* methods (all from IAsyncDataRecord)
    public abstract bool GetBoolean(int i);
    public abstract byte GetByte(int i);
    public abstract long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length);
    public abstract char GetChar(int i);
    public abstract long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length);
    public abstract Guid GetGuid(int i);
    public abstract short GetInt16(int i);
    public abstract int GetInt32(int i);
    public abstract long GetInt64(int i);
    public abstract float GetFloat(int i);
    public abstract double GetDouble(int i);
    public abstract string GetString(int i);
    public abstract decimal GetDecimal(int i);
    public abstract DateTime GetDateTime(int i);
    public abstract IDataReader GetData(int i);
    public abstract string GetDataTypeName(int i);
    public abstract Type GetFieldType(int i);
    public abstract string GetName(int i);
    public abstract int GetOrdinal(string name);
    public abstract object GetValue(int i);
    public abstract int GetValues(object[] values);
    public abstract bool IsDBNull(int i);
    public abstract DataTable GetSchemaTable();

    // Abstract async core methods
    protected abstract ValueTask<bool> ReadCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask<bool> NextResultCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask CloseCoreAsync();

    // Virtual async methods (can be overridden for optimization)
    public virtual ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default)
        => new(IsDBNull(i));

    public virtual ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default)
        => new((T)GetValue(i));

    public virtual ValueTask<DataTable> GetSchemaTableAsync(CancellationToken cancellationToken = default)
        => new(GetSchemaTable());

    // Sync -> async bridge (throws on WASM)
    public bool Read() { SyncBridge.ThrowIfBrowser(nameof(ReadAsync)); return ReadCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public bool NextResult() { SyncBridge.ThrowIfBrowser(nameof(NextResultAsync)); return NextResultCoreAsync(CancellationToken.None).GetAwaiter().GetResult(); }
    public void Close() { SyncBridge.ThrowIfBrowser(nameof(CloseAsync)); CloseCoreAsync().GetAwaiter().GetResult(); }

    // Template methods -- async delegates to core
    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default) => ReadCoreAsync(cancellationToken);
    public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default) => NextResultCoreAsync(cancellationToken);
    public ValueTask CloseAsync() => CloseCoreAsync();

    // IAsyncEnumerable<IAsyncDataRecord>
    public async IAsyncEnumerator<IAsyncDataRecord> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        while (await ReadCoreAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return this;
        }
    }

    // IAsyncDisposable
    public async ValueTask DisposeAsync()
    {
        if (!IsClosed)
        {
            await CloseCoreAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    // IDisposable
    public void Dispose()
    {
        if (!IsClosed)
        {
            Close();
        }

        GC.SuppressFinalize(this);
    }
}

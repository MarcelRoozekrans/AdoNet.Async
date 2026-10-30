using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbDataReader : AsyncDbDataReader
{
    private readonly DbDataReader _inner;

    public AdapterDbDataReader(DbDataReader inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    internal DbDataReader InnerReader => _inner;
    public static explicit operator DbDataReader(AdapterDbDataReader reader) => reader._inner;

    // Properties
    public override int FieldCount => _inner.FieldCount;
    public override int Depth => _inner.Depth;
    public override bool IsClosed => _inner.IsClosed;
    public override int RecordsAffected => _inner.RecordsAffected;
    public override bool HasRows => _inner.HasRows;

    // Indexers
    public override object this[int i] => _inner[i];
    public override object this[string name] => _inner[name];

    // Get* methods
    public override bool GetBoolean(int i) => _inner.GetBoolean(i);
    public override byte GetByte(int i) => _inner.GetByte(i);
    public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length)
        => _inner.GetBytes(i, fieldOffset, buffer, bufferOffset, length);
    public override char GetChar(int i) => _inner.GetChar(i);
    public override long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length)
        => _inner.GetChars(i, fieldOffset, buffer, bufferOffset, length);
    public override Guid GetGuid(int i) => _inner.GetGuid(i);
    public override short GetInt16(int i) => _inner.GetInt16(i);
    public override int GetInt32(int i) => _inner.GetInt32(i);
    public override long GetInt64(int i) => _inner.GetInt64(i);
    public override float GetFloat(int i) => _inner.GetFloat(i);
    public override double GetDouble(int i) => _inner.GetDouble(i);
    public override string GetString(int i) => _inner.GetString(i);
    public override decimal GetDecimal(int i) => _inner.GetDecimal(i);
    public override DateTime GetDateTime(int i) => _inner.GetDateTime(i);
    public override IDataReader GetData(int i) => _inner.GetData(i);
    public override string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
    public override Type GetFieldType(int i) => _inner.GetFieldType(i);
    public override string GetName(int i) => _inner.GetName(i);
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public override object GetValue(int i) => _inner.GetValue(i);
    public override int GetValues(object[] values) => _inner.GetValues(values);
    public override bool IsDBNull(int i) => _inner.IsDBNull(i);
    public override DataTable GetSchemaTable() => _inner.GetSchemaTable()!;

    // Async core methods. Each hands the provider's task straight to the caller: awaiting it
    // here would allocate a state machine whenever the provider completes asynchronously.
    // ProviderTask keeps a synchronous provider throw surfacing on await.
    protected override ValueTask<bool> ReadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<bool>(_inner.ReadAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<bool>(ex);
        }
    }

    protected override ValueTask<bool> NextResultCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<bool>(_inner.NextResultAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<bool>(ex);
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

    // Optimized async overrides
    public override ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default)
    {
        try
        {
            return new ValueTask<bool>(_inner.IsDBNullAsync(i, cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<bool>(ex);
        }
    }

    public override ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default)
    {
        try
        {
            return new ValueTask<T>(_inner.GetFieldValueAsync<T>(i, cancellationToken));
        }
        catch (Exception ex)
        {
            return ProviderTask.FromThrown<T>(ex);
        }
    }

    // Straight to the provider rather than the base class's cast over GetValue:
    // providers convert here (Microsoft.Data.Sqlite hands back a string for a
    // TEXT-declared DateTimeOffset column, which a cast cannot survive).
    public override T GetFieldValue<T>(int i) => _inner.GetFieldValue<T>(i);

    // Sync members call the provider's own instead of the base class's sync-over-async bridge.
    protected override bool ReadCore() => _inner.Read();
    protected override bool NextResultCore() => _inner.NextResult();
    protected override void CloseCore() => _inner.Close();

    // Dispose the provider's reader, not just close it. When that completes synchronously the
    // base call runs and finds the reader closed, since IsClosed reads the provider's reader.
    // When it yields, the provider's task goes straight to the caller and the base call is
    // skipped: all it could do is close the provider's reader, which disposing already closes.
    // Awaiting the dispose and then the base call would box a state machine on every yield.
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

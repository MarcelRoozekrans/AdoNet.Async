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

    // Async core methods
    protected override async ValueTask<bool> ReadCoreAsync(CancellationToken cancellationToken)
        => await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<bool> NextResultCoreAsync(CancellationToken cancellationToken)
        => await _inner.NextResultAsync(cancellationToken).ConfigureAwait(false);

    protected override async ValueTask CloseCoreAsync()
        => await _inner.CloseAsync().ConfigureAwait(false);

    // Optimized async overrides
    public override async ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default)
        => await _inner.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false);

    public override async ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default)
        => await _inner.GetFieldValueAsync<T>(i, cancellationToken).ConfigureAwait(false);

    // Sync methods using native _inner (hide base class sync-over-async bridge)
    public new bool Read() => _inner.Read();
    public new bool NextResult() => _inner.NextResult();
    public new void Close() => _inner.Close();

    // Override to properly dispose the inner reader
#pragma warning disable CA1816 // Hiding base Dispose to delegate to inner reader
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

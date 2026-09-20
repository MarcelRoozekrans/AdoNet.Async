namespace System.Data.Async;

public interface IAsyncDataRecord
{
    int FieldCount { get; }
    object this[int i] { get; }
    object this[string name] { get; }

    bool GetBoolean(int i);
    byte GetByte(int i);
    long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length);
    char GetChar(int i);
    long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length);
    Guid GetGuid(int i);
    short GetInt16(int i);
    int GetInt32(int i);
    long GetInt64(int i);
    float GetFloat(int i);
    double GetDouble(int i);
    string GetString(int i);
    decimal GetDecimal(int i);
    DateTime GetDateTime(int i);
    IDataReader GetData(int i);
    string GetDataTypeName(int i);
    Type GetFieldType(int i);
    string GetName(int i);
    int GetOrdinal(string name);
    object GetValue(int i);
    int GetValues(object[] values);
    bool IsDBNull(int i);

    ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default);
    ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default);

    /// <summary>
    /// Strongly-typed synchronous read, mirroring
    /// <see cref="System.Data.Common.DbDataReader.GetFieldValue{T}(int)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The typed getters above cover the types <see cref="IDataRecord"/> names
    /// explicitly. Everything else a provider can return -- <c>byte[]</c> for a
    /// BLOB, <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/> -- had only
    /// <see cref="GetFieldValueAsync{T}"/> or an untyped
    /// <see cref="GetValue"/> plus a cast. Callers materialising a row
    /// synchronously had no typed option, which is the gap this closes.
    /// </para>
    /// <para>
    /// The default implementation casts <see cref="GetValue"/>, matching what
    /// <see cref="GetFieldValueAsync{T}"/> does by default. It is a default
    /// interface method so adding it breaks no existing implementer. Adapters
    /// over a real <see cref="System.Data.Common.DbDataReader"/> override it to
    /// delegate to the provider, which also converts -- a provider that returns
    /// a string for a TEXT-backed <see cref="DateTimeOffset"/> column is handled
    /// there and would throw under the cast.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type to return the column as.</typeparam>
    /// <param name="i">Zero-based column ordinal.</param>
    T GetFieldValue<T>(int i) => (T)GetValue(i);
}

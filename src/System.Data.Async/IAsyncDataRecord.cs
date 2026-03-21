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
}

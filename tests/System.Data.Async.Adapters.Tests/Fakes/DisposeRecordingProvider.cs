#pragma warning disable CA1065 // Unsupported members of the fakes throw NotSupportedException by design
#pragma warning disable HLQ006 // GetEnumerator override keeps the signature DbDataReader declares
#pragma warning disable MA0025 // NotSupportedException is the intended signal for members the tests never call

using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace System.Data.Async.Adapters.Tests.Fakes;

/// <summary>
/// A provider reader that counts how it was disposed, so a test can tell whether disposing the
/// adapter reached the provider object or only closed it.
/// </summary>
internal sealed class DisposeRecordingDbDataReader : DbDataReader
{
    private bool _closed;

    public int CloseCalls { get; private set; }
    /// <summary>Calls of Dispose, including the one the base DisposeAsync makes.</summary>
    public int DisposeCalls { get; private set; }
    public int DisposeAsyncCalls { get; private set; }

    public override int FieldCount => 0;
    public override int Depth => 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => 0;
    public override bool HasRows => false;
    public override object this[int ordinal] => throw new NotSupportedException();
    public override object this[string name] => throw new NotSupportedException();

    public override void Close()
    {
        CloseCalls++;
        _closed = true;
    }

    public override Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
            _closed = true;
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        DisposeAsyncCalls++;
        _closed = true;
        return base.DisposeAsync();
    }

    public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
    public override byte GetByte(int ordinal) => throw new NotSupportedException();
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char GetChar(int ordinal) => throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
    public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
    public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
    public override double GetDouble(int ordinal) => throw new NotSupportedException();
    public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
    public override float GetFloat(int ordinal) => throw new NotSupportedException();
    public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
    public override short GetInt16(int ordinal) => throw new NotSupportedException();
    public override int GetInt32(int ordinal) => throw new NotSupportedException();
    public override long GetInt64(int ordinal) => throw new NotSupportedException();
    public override string GetName(int ordinal) => throw new NotSupportedException();
    public override int GetOrdinal(string name) => throw new NotSupportedException();
    public override string GetString(int ordinal) => throw new NotSupportedException();
    public override object GetValue(int ordinal) => throw new NotSupportedException();
    public override int GetValues(object[] values) => throw new NotSupportedException();
    public override bool IsDBNull(int ordinal) => throw new NotSupportedException();
    public override bool NextResult() => throw new NotSupportedException();
    public override bool Read() => throw new NotSupportedException();
    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
}

/// <summary>
/// A provider connection that counts how it was disposed. It starts open, so the base class's
/// close-on-dispose path is the one a hidden dispose would fall back to.
/// </summary>
internal sealed class DisposeRecordingDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Open;

    public int CloseCalls { get; private set; }
    /// <summary>Calls of Dispose, including the one the base DisposeAsync makes.</summary>
    public int DisposeCalls { get; private set; }
    public int DisposeAsyncCalls { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = "";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;

    public override void Close()
    {
        CloseCalls++;
        _state = ConnectionState.Closed;
    }

    public override Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
            _state = ConnectionState.Closed;
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        DisposeAsyncCalls++;
        _state = ConnectionState.Closed;
        return base.DisposeAsync();
    }

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Open() => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
}

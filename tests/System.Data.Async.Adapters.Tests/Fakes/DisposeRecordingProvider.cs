#pragma warning disable CA1065 // Unsupported members of the fakes throw NotSupportedException by design
#pragma warning disable HLQ006 // GetEnumerator override keeps the signature DbDataReader declares
#pragma warning disable MA0025 // NotSupportedException is the intended signal for members the tests never call

using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Sources;

namespace System.Data.Async.Adapters.Tests.Fakes;

/// <summary>
/// What a test can set on any of the dispose-recording fakes to choose how its DisposeAsync ends.
/// </summary>
internal interface IDisposeRecordingProvider
{
    Task? PendingDispose { get; set; }

    Exception? ThrowOnDisposeAsync { get; set; }
}

/// <summary>
/// A provider reader that counts how it was disposed, so a test can tell whether disposing the
/// adapter reached the provider object or only closed it.
/// </summary>
internal sealed class DisposeRecordingDbDataReader : DbDataReader, IDisposeRecordingProvider
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

    /// <summary>
    /// When set, DisposeAsync hands back this task instead of completing synchronously, the way
    /// a network provider's dispose waits on the server.
    /// </summary>
    public Task? PendingDispose { get; set; }

    /// <summary>When set, DisposeAsync throws this synchronously instead of returning a task.</summary>
    public Exception? ThrowOnDisposeAsync { get; set; }

    public override ValueTask DisposeAsync()
    {
        DisposeAsyncCalls++;
        if (ThrowOnDisposeAsync is { } thrown)
        {
            throw thrown;
        }

        _closed = true;
        return PendingDispose is { } pending ? new ValueTask(pending) : base.DisposeAsync();
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
internal sealed class DisposeRecordingDbConnection : DbConnection, IDisposeRecordingProvider
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

    /// <summary>
    /// When set, DisposeAsync hands back this task instead of completing synchronously, the way
    /// a network provider's dispose waits on the server.
    /// </summary>
    public Task? PendingDispose { get; set; }

    /// <summary>When set, DisposeAsync throws this synchronously instead of returning a task.</summary>
    public Exception? ThrowOnDisposeAsync { get; set; }

    public override ValueTask DisposeAsync()
    {
        DisposeAsyncCalls++;
        if (ThrowOnDisposeAsync is { } thrown)
        {
            throw thrown;
        }

        _state = ConnectionState.Closed;
        return PendingDispose is { } pending ? new ValueTask(pending) : base.DisposeAsync();
    }

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Open() => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
}

/// <summary>
/// A provider transaction that counts how it was disposed.
/// </summary>
internal sealed class DisposeRecordingDbTransaction(DbConnection connection) : DbTransaction, IDisposeRecordingProvider
{
    /// <summary>Calls of Dispose, including the one the base DisposeAsync makes.</summary>
    public int DisposeCalls { get; private set; }
    public int DisposeAsyncCalls { get; private set; }

    /// <summary>
    /// When set, DisposeAsync hands back this task instead of completing synchronously, the way
    /// a network provider's dispose waits on the server.
    /// </summary>
    public Task? PendingDispose { get; set; }

    /// <summary>When set, DisposeAsync throws this synchronously instead of returning a task.</summary>
    public Exception? ThrowOnDisposeAsync { get; set; }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCalls++;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// When set, DisposeAsync hands back a ValueTask over this source rather than over a Task,
    /// as a provider with pooled dispose operations does.
    /// </summary>
    public IValueTaskSource? PendingDisposeSource { get; set; }

    public override ValueTask DisposeAsync()
    {
        DisposeAsyncCalls++;
        if (ThrowOnDisposeAsync is { } thrown)
        {
            throw thrown;
        }

        if (PendingDisposeSource is { } source)
        {
            return new ValueTask(source, 0);
        }

        return PendingDispose is { } pending ? new ValueTask(pending) : base.DisposeAsync();
    }

    public override void Commit() => throw new NotSupportedException();
    public override void Rollback() => throw new NotSupportedException();
}

/// <summary>
/// A single-use IValueTaskSource a test completes by hand, so a ValueTask over it is pending
/// until then and is not backed by a Task.
/// </summary>
internal sealed class ManualValueTaskSource : IValueTaskSource
{
    private ManualResetValueTaskSourceCore<bool> _core;

    public void SetResult() => _core.SetResult(true);

    public void GetResult(short token) => _core.GetResult(token);

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}

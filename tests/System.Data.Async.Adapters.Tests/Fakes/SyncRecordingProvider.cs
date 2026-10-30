#pragma warning disable CA1065 // Unsupported members of the fakes throw NotSupportedException by design
#pragma warning disable HLQ006 // GetEnumerator override keeps the signature DbDataReader declares
#pragma warning disable MA0025 // NotSupportedException is the intended signal for members the tests never call

using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace System.Data.Async.Adapters.Tests.Fakes;

/// <summary>
/// Provider objects whose synchronous members count their calls and whose asynchronous
/// counterparts throw. A test drives an adapter's synchronous member through some static type and
/// checks that the provider's own synchronous member ran, not a sync-over-async bridge.
/// </summary>
internal static class SyncOnly
{
    public static NotSupportedException AsyncCalled(string member)
        => new($"{member} was called; the synchronous call should not go through the async path.");
}

internal sealed class SyncRecordingDbDataReader : DbDataReader
{
    private bool _closed;

    public int ReadCalls { get; private set; }
    public int NextResultCalls { get; private set; }
    public int CloseCalls { get; private set; }

    public override int FieldCount => 0;
    public override int Depth => 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => 0;
    public override bool HasRows => true;
    public override object this[int ordinal] => throw new NotSupportedException();
    public override object this[string name] => throw new NotSupportedException();

    public override bool Read()
    {
        ReadCalls++;
        return true;
    }

    public override bool NextResult()
    {
        NextResultCalls++;
        return true;
    }

    public override void Close()
    {
        CloseCalls++;
        _closed = true;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => throw SyncOnly.AsyncCalled(nameof(ReadAsync));
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => throw SyncOnly.AsyncCalled(nameof(NextResultAsync));
    public override Task CloseAsync() => throw SyncOnly.AsyncCalled(nameof(CloseAsync));

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
    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
}

internal sealed class SyncRecordingDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public int OpenCalls { get; private set; }
    public int CloseCalls { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = "";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1";
    public override ConnectionState State => _state;

    public override void Open()
    {
        OpenCalls++;
        _state = ConnectionState.Open;
    }

    public override void Close()
    {
        CloseCalls++;
        _state = ConnectionState.Closed;
    }

    public override Task OpenAsync(CancellationToken cancellationToken) => throw SyncOnly.AsyncCalled(nameof(OpenAsync));
    public override Task CloseAsync() => throw SyncOnly.AsyncCalled(nameof(CloseAsync));

    public int BeginTransactionCalls { get; private set; }
    public IsolationLevel LastIsolationLevel { get; private set; }
    public string? LastDatabase { get; private set; }

    public override void ChangeDatabase(string databaseName) => LastDatabase = databaseName;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        BeginTransactionCalls++;
        LastIsolationLevel = isolationLevel;
        return new SyncRecordingDbTransaction(this);
    }

    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => throw SyncOnly.AsyncCalled(nameof(BeginTransactionAsync));

    public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
        => throw SyncOnly.AsyncCalled(nameof(ChangeDatabaseAsync));

    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
}

internal sealed class SyncRecordingDbTransaction(DbConnection connection) : DbTransaction
{
    public int CommitCalls { get; private set; }
    public int RollbackCalls { get; private set; }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;

    public override void Commit() => CommitCalls++;
    public override void Rollback() => RollbackCalls++;

    public override Task CommitAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(CommitAsync));
    public override Task RollbackAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(RollbackAsync));
}

internal sealed class SyncRecordingDbCommand : DbCommand
{
    public int ExecuteReaderCalls { get; private set; }
    public CommandBehavior LastBehavior { get; private set; }
    public int ExecuteNonQueryCalls { get; private set; }
    public int ExecuteScalarCalls { get; private set; }
    public int PrepareCalls { get; private set; }

    [AllowNull]
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }

    public override int ExecuteNonQuery()
    {
        ExecuteNonQueryCalls++;
        return 1;
    }

    public override object? ExecuteScalar()
    {
        ExecuteScalarCalls++;
        return "scalar";
    }

    public override void Prepare() => PrepareCalls++;

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        ExecuteReaderCalls++;
        LastBehavior = behavior;
        return new SyncRecordingDbDataReader();
    }

    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => throw SyncOnly.AsyncCalled(nameof(ExecuteNonQueryAsync));
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => throw SyncOnly.AsyncCalled(nameof(ExecuteScalarAsync));
    public override Task PrepareAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(PrepareAsync));
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        => throw SyncOnly.AsyncCalled(nameof(ExecuteReaderAsync));
}

internal sealed class SyncRecordingDbBatch : DbBatch
{
    private readonly ListDbBatchCommandCollection _commands = new();

    public int ExecuteReaderCalls { get; private set; }
    public int ExecuteNonQueryCalls { get; private set; }
    public int ExecuteScalarCalls { get; private set; }
    public int PrepareCalls { get; private set; }

    public override int Timeout { get; set; }
    protected override DbBatchCommandCollection DbBatchCommands => _commands;
    protected override DbConnection? DbConnection { get; set; }
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }

    public override int ExecuteNonQuery()
    {
        ExecuteNonQueryCalls++;
        return 7;
    }

    public override object? ExecuteScalar()
    {
        ExecuteScalarCalls++;
        return "scalar";
    }

    public override void Prepare() => PrepareCalls++;

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        ExecuteReaderCalls++;
        return new SyncRecordingDbDataReader();
    }

    protected override DbBatchCommand CreateDbBatchCommand() => new PendingDbBatchCommand();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(ExecuteNonQueryAsync));
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(ExecuteScalarAsync));
    public override Task PrepareAsync(CancellationToken cancellationToken = default) => throw SyncOnly.AsyncCalled(nameof(PrepareAsync));
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        => throw SyncOnly.AsyncCalled(nameof(ExecuteReaderAsync));
}

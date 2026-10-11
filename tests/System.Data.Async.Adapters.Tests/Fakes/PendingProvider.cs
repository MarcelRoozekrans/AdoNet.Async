#pragma warning disable CA1065 // Unsupported members of the fakes throw NotSupportedException by design
#pragma warning disable HLQ006 // GetEnumerator overrides keep the signatures DbDataReader and DbBatchCommandCollection declare
#pragma warning disable MA0025 // NotSupportedException is the intended signal for members the tests never call

using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace System.Data.Async.Adapters.Tests.Fakes;

/// <summary>
/// The task every async member of the fake provider hands back. A test sets <see cref="Next"/>
/// to a not-yet-completed task to model a network provider whose calls complete asynchronously,
/// or sets <see cref="ThrowOnCall"/> to model a provider that throws synchronously instead of
/// returning a faulted task.
/// </summary>
internal sealed class PendingSlot
{
    public Task Next { get; set; } = Task.CompletedTask;

    public Exception? ThrowOnCall { get; set; }

    public TTask Take<TTask>() where TTask : Task
    {
        if (ThrowOnCall is { } ex)
        {
            throw ex;
        }

        return (TTask)Next;
    }
}

internal sealed class PendingDbConnection(PendingSlot slot) : DbConnection
{
    [AllowNull]
    public override string ConnectionString { get; set; } = "";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "1";
    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Close() => throw new NotSupportedException();
    public override void Open() => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

    public override Task OpenAsync(CancellationToken cancellationToken) => slot.Take<Task>();
    public override Task CloseAsync() => slot.Take<Task>();
    public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) => slot.Take<Task>();
}

internal sealed class PendingDbTransaction(PendingSlot slot, DbConnection connection) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;

    public override void Commit() => throw new NotSupportedException();
    public override void Rollback() => throw new NotSupportedException();

    public override Task CommitAsync(CancellationToken cancellationToken = default) => slot.Take<Task>();
    public override Task RollbackAsync(CancellationToken cancellationToken = default) => slot.Take<Task>();
}

internal sealed class PendingDbCommand(PendingSlot slot) : DbCommand
{
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
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    public override void Prepare() => throw new NotSupportedException();
    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => slot.Take<Task<int>>();
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => slot.Take<Task<object?>>();
    public override Task PrepareAsync(CancellationToken cancellationToken = default) => slot.Take<Task>();
}

internal sealed class PendingDbDataReader(PendingSlot slot) : DbDataReader
{
    public override int FieldCount => 1;
    public override int Depth => 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => 0;
    public override bool HasRows => true;
    public override object this[int ordinal] => throw new NotSupportedException();
    public override object this[string name] => throw new NotSupportedException();

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

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => slot.Take<Task<bool>>();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => slot.Take<Task<bool>>();
    public override Task CloseAsync() => slot.Take<Task>();
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => slot.Take<Task<bool>>();
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => slot.Take<Task<T>>();
}

internal sealed class PendingDbBatchCommand : DbBatchCommand
{
    [AllowNull]
    public override string CommandText { get; set; } = "";
    public override CommandType CommandType { get; set; }
    public override int RecordsAffected => 0;
    protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
}

internal sealed class ListDbBatchCommandCollection : DbBatchCommandCollection
{
    private readonly List<DbBatchCommand> _items = [];

    public override int Count => _items.Count;
    public override bool IsReadOnly => false;

    public override void Add(DbBatchCommand item) => _items.Add(item);
    public override void Clear() => _items.Clear();
    public override bool Contains(DbBatchCommand item) => _items.Contains(item);
    public override void CopyTo(DbBatchCommand[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public override IEnumerator<DbBatchCommand> GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(DbBatchCommand item) => _items.IndexOf(item);
    public override void Insert(int index, DbBatchCommand item) => _items.Insert(index, item);
    public override bool Remove(DbBatchCommand item) => _items.Remove(item);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    protected override DbBatchCommand GetBatchCommand(int index) => _items[index];
    protected override void SetBatchCommand(int index, DbBatchCommand batchCommand) => _items[index] = batchCommand;
}

internal sealed class PendingDbBatch(PendingSlot slot) : DbBatch
{
    private readonly ListDbBatchCommandCollection _commands = new();

    public override int Timeout { get; set; }
    protected override DbBatchCommandCollection DbBatchCommands => _commands;
    protected override DbConnection? DbConnection { get; set; }
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object? ExecuteScalar() => throw new NotSupportedException();
    public override void Prepare() => throw new NotSupportedException();
    protected override DbBatchCommand CreateDbBatchCommand() => new PendingDbBatchCommand();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) => slot.Take<Task<DbDataReader>>();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default) => slot.Take<Task<int>>();
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default) => slot.Take<Task<object?>>();
    public override Task PrepareAsync(CancellationToken cancellationToken = default) => slot.Take<Task>();
}

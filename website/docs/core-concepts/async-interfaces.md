---
sidebar_position: 1
title: Async Interfaces
---

# Async Interfaces

AdoNet.Async defines a complete set of async interfaces that mirror the `System.Data` types. Every interface provides both synchronous and asynchronous members, so you can migrate incrementally.

All async methods return `ValueTask` or `ValueTask<T>` and accept an optional `CancellationToken`.

## IAsyncDbConnection

**Mirrors:** `IDbConnection` / `DbConnection`

```csharp
public interface IAsyncDbConnection : IAsyncDisposable, IDisposable
{
    // Properties
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }

    // Sync methods
    IAsyncDbTransaction BeginTransaction();
    IAsyncDbTransaction BeginTransaction(IsolationLevel il);
    void ChangeDatabase(string databaseName);
    IAsyncDbCommand CreateCommand();
    void Open();
    void Close();

    // Async methods
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default);
    ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    ValueTask OpenAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
}
```

Key differences from `IDbConnection`:

- `CreateCommand()` returns `IAsyncDbCommand` instead of `IDbCommand`.
- `BeginTransaction()` returns `IAsyncDbTransaction` instead of `IDbTransaction`.
- `OpenAsync` and `CloseAsync` are first-class members, not extension methods.
- Implements `IAsyncDisposable` -- use `await using` for deterministic cleanup.

```csharp
await using IAsyncDbConnection connection = factory.CreateConnection();
connection.ConnectionString = "...";
await connection.OpenAsync();
// use connection...
// automatically closed and disposed at end of scope
```

## IAsyncDbCommand

**Mirrors:** `IDbCommand` / `DbCommand`

```csharp
public interface IAsyncDbCommand : IAsyncDisposable, IDisposable
{
    // Properties
    string CommandText { get; set; }
    int CommandTimeout { get; set; }
    CommandType CommandType { get; set; }
    IAsyncDbConnection? Connection { get; set; }
    IAsyncDbTransaction? Transaction { get; set; }
    IDataParameterCollection Parameters { get; }
    UpdateRowSource UpdatedRowSource { get; set; }

    // Sync methods
    IAsyncDataReader ExecuteReader();
    IAsyncDataReader ExecuteReader(CommandBehavior behavior);
    int ExecuteNonQuery();
    object? ExecuteScalar();
    void Prepare();
    void Cancel();
    IDbDataParameter CreateParameter();

    // Async methods
    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default);
    ValueTask<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
    ValueTask<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);
    ValueTask PrepareAsync(CancellationToken cancellationToken = default);
}
```

Key differences from `IDbCommand`:

- `ExecuteReader()` and `ExecuteReaderAsync()` return `IAsyncDataReader` instead of `IDataReader`.
- `Connection` and `Transaction` properties use the async interface types.
- `Parameters` uses the standard `IDataParameterCollection` -- parameter handling is unchanged.

```csharp
IAsyncDbCommand cmd = connection.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM Orders WHERE Status = @status";

var param = cmd.CreateParameter();
param.ParameterName = "@status";
param.Value = "Active";
cmd.Parameters.Add(param);

object? count = await cmd.ExecuteScalarAsync();
```

## IAsyncDataReader

**Mirrors:** `IDataReader` / `DbDataReader`

```csharp
public interface IAsyncDataReader : IAsyncDataRecord, IAsyncEnumerable<IAsyncDataRecord>, IAsyncDisposable, IDisposable
{
    // Properties
    int Depth { get; }
    bool IsClosed { get; }
    int RecordsAffected { get; }
    bool HasRows { get; }

    // Sync methods
    bool Read();
    bool NextResult();
    void Close();
    DataTable GetSchemaTable();

    // Async methods
    ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
    ValueTask<DataTable> GetSchemaTableAsync(CancellationToken cancellationToken = default);
}
```

The most notable feature is that `IAsyncDataReader` implements `IAsyncEnumerable<IAsyncDataRecord>`. This means you can iterate rows with `await foreach` instead of a manual `while (await reader.ReadAsync())` loop. See [Await ForEach](./await-foreach.md) for details.

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    await foreach (IAsyncDataRecord record in reader)
    {
        Console.WriteLine(record.GetString(0));
    }
}
```

## IAsyncDataRecord

**Mirrors:** `IDataRecord`

```csharp
public interface IAsyncDataRecord
{
    // Properties
    int FieldCount { get; }
    object this[int i] { get; }
    object this[string name] { get; }

    // Typed accessors (sync)
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

    // Async methods
    ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default);
    ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default);
}
```

`IAsyncDataRecord` extends the standard `IDataRecord` accessors with two async methods:

- **`IsDBNullAsync`** -- Asynchronously checks if a field is `DBNull`.
- **`GetFieldValueAsync<T>`** -- Asynchronously retrieves a field value as a specific type. This is the generic equivalent of all the typed `Get*` methods.

The synchronous accessors (`GetInt32`, `GetString`, etc.) are included so that code using positional field access does not need to change. They are safe to call on the current row after `ReadAsync` advances the reader.

## IAsyncDbTransaction

**Mirrors:** `IDbTransaction` / `DbTransaction`

```csharp
public interface IAsyncDbTransaction : IAsyncDisposable, IDisposable
{
    IAsyncDbConnection Connection { get; }
    IsolationLevel IsolationLevel { get; }

    void Commit();
    void Rollback();

    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
```

Use `await using` for automatic rollback on exception:

```csharp
await using IAsyncDbTransaction tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

IAsyncDbCommand cmd = connection.CreateCommand();
cmd.Transaction = tx;
cmd.CommandText = "UPDATE Accounts SET Balance = Balance - 100 WHERE Id = 1";
await cmd.ExecuteNonQueryAsync();

cmd.CommandText = "UPDATE Accounts SET Balance = Balance + 100 WHERE Id = 2";
await cmd.ExecuteNonQueryAsync();

await tx.CommitAsync();
// If an exception is thrown before CommitAsync, DisposeAsync rolls back
```

## IAsyncDbProviderFactory

**Mirrors:** `DbProviderFactory`

```csharp
public interface IAsyncDbProviderFactory
{
    IAsyncDbConnection CreateConnection();
    IAsyncDbCommand CreateCommand();
    IDbDataParameter CreateParameter();
}
```

This is a lightweight factory interface for creating connections, commands, and parameters without depending on a concrete provider. Register it via DI:

```csharp
services.AddAsyncData(SqlClientFactory.Instance);
```

Then inject `IAsyncDbProviderFactory` wherever you need to create database objects:

```csharp
public class OrderRepository(IAsyncDbProviderFactory factory)
{
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        await using var conn = factory.CreateConnection();
        conn.ConnectionString = "...";
        await conn.OpenAsync(ct);

        var cmd = factory.CreateCommand();
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM Orders";
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
```

## How they relate to System.Data

| System.Data | AdoNet.Async Interface | AdoNet.Async Base Class | Adapter Wrapper |
|---|---|---|---|
| `IDbConnection` / `DbConnection` | `IAsyncDbConnection` | `AsyncDbConnection` | `AdapterDbConnection` |
| `IDbCommand` / `DbCommand` | `IAsyncDbCommand` | `AsyncDbCommand` | `AdapterDbCommand` |
| `IDataReader` / `DbDataReader` | `IAsyncDataReader` | `AsyncDbDataReader` | `AdapterDbDataReader` |
| `IDataRecord` | `IAsyncDataRecord` | (included in `AsyncDbDataReader`) | (included in `AdapterDbDataReader`) |
| `IDbTransaction` / `DbTransaction` | `IAsyncDbTransaction` | `AsyncDbTransaction` | `AdapterDbTransaction` |
| `DbProviderFactory` | `IAsyncDbProviderFactory` | -- | `AdapterDbProviderFactory` |

The **interfaces** define the contract. The **abstract base classes** provide the template method pattern (sync methods delegate to async core methods via a [sync bridge](./sync-bridge.md)). The **adapter wrappers** (in the `AdoNet.Async.Adapters` package) wrap existing `System.Data.Common` types to implement the interfaces.

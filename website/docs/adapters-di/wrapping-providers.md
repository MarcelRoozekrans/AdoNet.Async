---
sidebar_position: 1
title: Wrapping Providers
---

# Wrapping ADO.NET Providers

The `AdoNet.Async.Adapters` package wraps any existing ADO.NET provider (`DbConnection`, `DbCommand`, `DbDataReader`, `DbTransaction`) into the async-first `IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, and `IAsyncDbTransaction` interfaces. This enables a one-line migration path for existing code.

## Installation

```bash
dotnet add package AdoNet.Async.Adapters
```

## The `.AsAsync()` Extension Method

The simplest way to get started is the `.AsAsync()` extension method on any `DbConnection`:

```csharp
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

DbConnection sqlConnection = new SqlConnection(connectionString);
IAsyncDbConnection connection = sqlConnection.AsAsync();

await connection.OpenAsync();
```

This single line wraps the connection and all objects it creates (commands, transactions, readers) in the adapter layer. From this point on, you work exclusively with the `IAsync*` interfaces.

## Adapter Classes

The adapter layer consists of five wrapper classes:

### AdapterDbConnection

Wraps a `DbConnection` and implements `IAsyncDbConnection`.

```csharp
var inner = new SqlConnection(connectionString);
var connection = new AdapterDbConnection(inner);

await connection.OpenAsync();
await connection.ChangeDatabaseAsync("OtherDb");
await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
var cmd = connection.CreateCommand(); // returns AdapterDbCommand
await connection.CloseAsync();
```

Key behaviors:
- `CreateCommand()` returns an `AdapterDbCommand` linked to this connection
- `BeginTransactionAsync()` returns an `AdapterDbTransaction`
- Disposing the adapter disposes the inner connection

### AdapterDbCommand

Wraps a `DbCommand` and implements `IAsyncDbCommand`.

```csharp
var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name FROM Users WHERE Id = @id";

var param = cmd.CreateParameter();
param.ParameterName = "@id";
param.Value = 42;
cmd.Parameters.Add(param);

IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
int affected = await cmd.ExecuteNonQueryAsync();
object? scalar = await cmd.ExecuteScalarAsync();
await cmd.PrepareAsync();
```

### AdapterDbDataReader

Wraps a `DbDataReader` and implements `IAsyncDataReader` (which includes `IAsyncEnumerable<IAsyncDataRecord>`).

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    // Manual async loop
    while (await reader.ReadAsync())
    {
        int id = reader.GetInt32(0);
        string name = reader.GetString(1);
    }

    // Or use await foreach
    await foreach (IAsyncDataRecord record in reader)
    {
        bool isNull = await record.IsDBNullAsync(2);
        string value = await record.GetFieldValueAsync<string>(1);
    }
}
```

### AdapterDbTransaction

Wraps a `DbTransaction` and implements `IAsyncDbTransaction`.

```csharp
await using var tx = await connection.BeginTransactionAsync();

var cmd = connection.CreateCommand();
cmd.Transaction = tx;
cmd.CommandText = "INSERT INTO Users (Name) VALUES ('Alice')";
await cmd.ExecuteNonQueryAsync();

await tx.CommitAsync();
// or: await tx.RollbackAsync();
```

### AdapterDbDataAdapter

Provides `FillAsync` and `UpdateAsync` for populating `AsyncDataTable` and `AsyncDataSet` from a database.

```csharp
using System.Data.Async.DataSet;

var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT * FROM Products";

var adapter = new AdapterDbDataAdapter(cmd);

// Fill a single table
var table = new AsyncDataTable("Products");
int rows = await adapter.FillAsync(table);

// Fill an entire DataSet (one table per result set)
var dataSet = new AsyncDataSet();
int totalRows = await adapter.FillAsync(dataSet);
```

The adapter also supports `UpdateAsync` with separate insert/update/delete commands:

```csharp
var adapter = new AdapterDbDataAdapter(selectCmd);
adapter.InsertCommand = insertCmd;
adapter.UpdateCommand = updateCmd;
adapter.DeleteCommand = deleteCmd;

// Update sends changes back to the database
int affected = await adapter.UpdateAsync(table);
```

`UpdateAsync` iterates over rows with `Added`, `Modified`, or `Deleted` state, maps parameter values from row columns via `IDbDataParameter.SourceColumn`, and calls `ExecuteNonQueryAsync` for each.

:::info
`AdapterDbDataAdapter.AcceptChangesDuringFill` defaults to `true` (matching ADO.NET behavior). Set it to `false` if you need to track row states after a fill. `AcceptChangesDuringUpdate` also defaults to `true`.
:::

### AdapterDbProviderFactory

Wraps a `DbProviderFactory` and implements `IAsyncDbProviderFactory`:

```csharp
var factory = new AdapterDbProviderFactory(SqlClientFactory.Instance);

IAsyncDbConnection conn = factory.CreateConnection();
IAsyncDbCommand cmd = factory.CreateCommand();
IDbDataParameter param = factory.CreateParameter();
```

This is primarily used through [Dependency Injection](./dependency-injection.md) rather than directly.

## Explicit Cast Back to Inner Connection

When you need the underlying `DbConnection` for provider-specific operations (e.g., `SqlBulkCopy`, `NpgsqlConnection.TypeMapper`), use an explicit cast:

```csharp
IAsyncDbConnection asyncConn = new SqlConnection(connectionString).AsAsync();
await asyncConn.OpenAsync();

// Cast back to the inner DbConnection
DbConnection inner = (DbConnection)(AdapterDbConnection)asyncConn;

// Or directly to the provider type
SqlConnection sqlConn = (SqlConnection)(DbConnection)(AdapterDbConnection)asyncConn;

// Use for provider-specific operations
using var bulkCopy = new SqlBulkCopy(sqlConn);
```

Explicit casts are available on all adapter types:

| Adapter Type | Cast To |
|---|---|
| `AdapterDbConnection` | `DbConnection` |
| `AdapterDbCommand` | `DbCommand` |
| `AdapterDbDataReader` | `DbDataReader` |
| `AdapterDbTransaction` | `DbTransaction` |

## Sync Method Optimization

The adapter classes hide the base class sync-over-async bridge methods with `new` declarations that call the native synchronous methods on the inner object directly. This means:

- `connection.Open()` calls `_inner.Open()` (native sync)
- `connection.Close()` calls `_inner.Close()` (native sync)
- `command.ExecuteReader()` calls `_inner.ExecuteReader()` (native sync)
- `command.ExecuteNonQuery()` calls `_inner.ExecuteNonQuery()` (native sync)
- `command.ExecuteScalar()` calls `_inner.ExecuteScalar()` (native sync)
- `reader.Read()` calls `_inner.Read()` (native sync)
- `transaction.Commit()` calls `_inner.Commit()` (native sync)
- `transaction.Rollback()` calls `_inner.Rollback()` (native sync)

:::tip
The adapter layer is the recommended entry point for existing applications. There is no sync-over-async overhead when using synchronous methods, and async methods delegate to the provider's native async implementation.
:::

## Works with Any ADO.NET Provider

The adapter pattern works with any provider that derives from `DbConnection`:

```csharp
// SQL Server
using Microsoft.Data.SqlClient;
IAsyncDbConnection sql = new SqlConnection(cs).AsAsync();

// PostgreSQL
using Npgsql;
IAsyncDbConnection pg = new NpgsqlConnection(cs).AsAsync();

// MySQL
using MySqlConnector;
IAsyncDbConnection mysql = new MySqlConnection(cs).AsAsync();

// SQLite
using Microsoft.Data.Sqlite;
IAsyncDbConnection sqlite = new SqliteConnection(cs).AsAsync();
```

## Next Steps

- [Dependency Injection](./dependency-injection.md) -- register `IAsyncDbProviderFactory` in your DI container
- [Migrate Existing Code](../cookbook/migrate-existing-code.md) -- step-by-step migration guide

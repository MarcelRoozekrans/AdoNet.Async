# System.Data.Async — Design Document

**Date:** 2026-03-21
**Status:** Approved

## Overview

A full async-first replacement for `System.Data` / `System.Data.Common` targeting .NET 10. Provides modern async interfaces, abstract base classes, and drop-in compatible `AsyncDataSet`/`AsyncDataTable` types that are JSON-deserializable from the format produced by [Json.Net.DataSetConverters](https://github.com/AlesDo/DataSetConverters).

## Goals

- **Drop-in replacement** — Mirror the entire `System.Data` type hierarchy with async counterparts
- **Full feature parity** — All DataSet features: relations, constraints, merge, computed columns, row versioning
- **JSON compatibility** — `AsyncDataSet`/`AsyncDataTable` deserialize from JSON produced by `Json.Net.DataSetConverters` and serialize back to the same format
- **Dual audience** — Clean abstractions for library authors + ergonomic API for app developers
- **Immediate provider support** — Adapter wraps any existing ADO.NET provider; native provider contract for future performance optimization

## Non-Goals

- No POCO mapping / materialization layer
- No query builder or LINQ provider
- No new enum/value types where `System.Data` originals suffice

## Target

- .NET 10 only
- `<LangVersion>preview</LangVersion>`, nullable enabled, implicit usings
- Solution format: `.slnx`
- Analyzers: Meziantou, Roslynator (via `Directory.Build.props`)

## Package Structure

### System.Data.Async (core)

**Dependencies:** None

Contains:
- Async interfaces: `IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, `IAsyncDataRecord`, `IAsyncDbTransaction`
- Abstract base classes: `AsyncDbConnection`, `AsyncDbCommand`, `AsyncDbDataReader`
- Native provider contract: `IAsyncDbProviderFactory`

### System.Data.Async.DataSet

**Dependencies:** `System.Data.Async`, `Newtonsoft.Json`, `System.Data.Common` (BCL)

Contains:
- `AsyncDataSet`, `AsyncDataTable`, `AsyncDataAdapter`
- JSON converters: `AsyncDataSetConverter`, `AsyncDataTableConverter`
- Reuses `System.Data` in-memory types (`DataColumn`, `DataRow`, `DataRowCollection`, `DataColumnCollection`, `DataRelation`, `Constraint`, `DataView`, `PropertyCollection`)

### System.Data.Async.Adapters

**Dependencies:** `System.Data.Async`, `System.Data.Common` (BCL), `Microsoft.Extensions.DependencyInjection.Abstractions`

Contains:
- `AdapterDbConnection`, `AdapterDbCommand`, `AdapterDbDataReader`, `AdapterDbTransaction`
- `AdapterDbProviderFactory`, `AdapterDbDataAdapter`
- `DbConnectionExtensions.AsAsync()` extension method
- `ServiceCollectionExtensions.AddAsyncData()` DI registration

## Core Async Interfaces

### IAsyncDbConnection

```csharp
public interface IAsyncDbConnection : IAsyncDisposable, IDisposable
{
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }

    // Sync (drop-in compat)
    IAsyncDbTransaction BeginTransaction();
    IAsyncDbTransaction BeginTransaction(IsolationLevel il);
    void ChangeDatabase(string databaseName);
    IAsyncDbCommand CreateCommand();
    void Open();
    void Close();

    // Async
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken ct = default);
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken ct = default);
    ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken ct = default);
    ValueTask OpenAsync(CancellationToken ct = default);
    ValueTask CloseAsync();
}
```

### IAsyncDbCommand

```csharp
public interface IAsyncDbCommand : IAsyncDisposable, IDisposable
{
    string CommandText { get; set; }
    int CommandTimeout { get; set; }
    CommandType CommandType { get; set; }
    IAsyncDbConnection? Connection { get; set; }
    IAsyncDbTransaction? Transaction { get; set; }
    IDataParameterCollection Parameters { get; }
    UpdateRowSource UpdatedRowSource { get; set; }

    // Sync
    IAsyncDbDataReader ExecuteReader();
    IAsyncDbDataReader ExecuteReader(CommandBehavior behavior);
    int ExecuteNonQuery();
    object? ExecuteScalar();
    void Prepare();
    void Cancel();
    IDbDataParameter CreateParameter();

    // Async
    ValueTask<IAsyncDbDataReader> ExecuteReaderAsync(CancellationToken ct = default);
    ValueTask<IAsyncDbDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken ct = default);
    ValueTask<int> ExecuteNonQueryAsync(CancellationToken ct = default);
    ValueTask<object?> ExecuteScalarAsync(CancellationToken ct = default);
    ValueTask PrepareAsync(CancellationToken ct = default);
}
```

### IAsyncDataReader / IAsyncDataRecord

```csharp
public interface IAsyncDataRecord
{
    int FieldCount { get; }
    object this[int i] { get; }
    object this[string name] { get; }
    bool GetBoolean(int i);
    byte GetByte(int i);
    // ... all Get* methods from IDataRecord
    string GetName(int i);
    int GetOrdinal(string name);
    bool IsDBNull(int i);

    // Async
    ValueTask<bool> IsDBNullAsync(int i, CancellationToken ct = default);
    ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken ct = default);
}

public interface IAsyncDataReader : IAsyncDataRecord, IAsyncEnumerable<IAsyncDataRecord>, IAsyncDisposable, IDisposable
{
    int Depth { get; }
    bool IsClosed { get; }
    int RecordsAffected { get; }
    bool HasRows { get; }

    // Sync
    bool Read();
    bool NextResult();
    void Close();
    DataTable GetSchemaTable();

    // Async
    ValueTask<bool> ReadAsync(CancellationToken ct = default);
    ValueTask<bool> NextResultAsync(CancellationToken ct = default);
    ValueTask CloseAsync();
    ValueTask<DataTable> GetSchemaTableAsync(CancellationToken ct = default);
}
```

### IAsyncDbTransaction

```csharp
public interface IAsyncDbTransaction : IAsyncDisposable, IDisposable
{
    IAsyncDbConnection Connection { get; }
    IsolationLevel IsolationLevel { get; }

    void Commit();
    void Rollback();

    ValueTask CommitAsync(CancellationToken ct = default);
    ValueTask RollbackAsync(CancellationToken ct = default);
}
```

### IAsyncDbProviderFactory

```csharp
public interface IAsyncDbProviderFactory
{
    IAsyncDbConnection CreateConnection();
    IAsyncDbCommand CreateCommand();
    IDbDataParameter CreateParameter();
    AsyncDbDataAdapter CreateDataAdapter();
}
```

## Abstract Base Classes

Follow the `protected abstract *CoreAsync` template method pattern (same as BCL's `DbConnection`/`DbCommand`):

- **`AsyncDbConnection`** — Public methods delegate to `protected abstract` methods (`OpenCoreAsync`, `CloseCoreAsync`, `BeginDbTransactionAsync`, `ChangeDatabaseCoreAsync`, `CreateDbCommand`). Sync methods call `.GetAwaiter().GetResult()` on async counterparts by default.
- **`AsyncDbCommand`** — Same pattern with `ExecuteDbReaderAsync`, `ExecuteNonQueryCoreAsync`, `ExecuteScalarCoreAsync`, `PrepareCoreAsync`.
- **`AsyncDbDataReader`** — Same pattern with `ReadCoreAsync`, `NextResultCoreAsync`, `CloseCoreAsync`. Implements `IAsyncEnumerable<IAsyncDataRecord>` via `GetAsyncEnumerator` that yields `this` during `ReadCoreAsync` loop.

## AsyncDataSet / AsyncDataTable

### Strategy

Reuse `System.Data` in-memory types (`DataColumn`, `DataRow`, `DataRowCollection`, etc.) internally. These types have no I/O — they don't need async. The async value lives in:

- `AsyncDataTable.LoadAsync(IAsyncDataReader)` — async population from a reader
- `AsyncDataAdapter.FillAsync()` / `UpdateAsync()` — async I/O operations
- `AsyncDataTable.ReadXmlAsync()` / `WriteXmlAsync()` — async XML I/O
- `AsyncDataSet.ReadXmlAsync()` / `WriteXmlAsync()` — async XML I/O

### JSON Compatibility

Ship `AsyncDataSetConverter` and `AsyncDataTableConverter` (Newtonsoft.Json `JsonConverter<T>`) that read/write the exact same JSON structure as `Json.Net.DataSetConverters`. This ensures:

1. `DataSet` serialized with `Json.Net.DataSetConverters` → deserializes into `AsyncDataSet`
2. `AsyncDataSet` serialized with `AsyncDataSetConverter` → deserializes into `DataSet`
3. Full round-trip: column types, row states, original/current row versions, constraints, relations

## Adapter Package

### Pattern

Each adapter class wraps the corresponding `System.Data.Common` base class:

| Adapter | Wraps |
|---|---|
| `AdapterDbConnection` | `DbConnection` |
| `AdapterDbCommand` | `DbCommand` |
| `AdapterDbDataReader` | `DbDataReader` |
| `AdapterDbTransaction` | `DbTransaction` |
| `AdapterDbProviderFactory` | `DbProviderFactory` |
| `AdapterDbDataAdapter` | `DbDataAdapter` |

### Key Behaviors

- **Async methods** delegate to the inner object's existing async methods (`DbConnection.OpenAsync`, `DbCommand.ExecuteReaderAsync`, etc.)
- **Sync methods** are overridden with `new` to call the inner object's native sync methods directly — no sync-over-async overhead
- **`InnerConnection` / `InnerCommand` / `InnerReader`** properties expose the wrapped object as an escape hatch for provider-specific features
- **`DbConnectionExtensions.AsAsync()`** — one-liner migration: `new NpgsqlConnection(cs).AsAsync()`

### Provider Model

- **Day one:** Adapter wraps any `DbConnection`-based provider (SqlClient, Npgsql, MySqlConnector, SQLite). All providers work immediately.
- **Future:** Provider authors can implement `AsyncDbConnection` / `IAsyncDbProviderFactory` directly for zero-overhead native async. The interface is already defined; no breaking changes needed.

## DI Integration

```csharp
// Wrap existing provider
builder.Services.AddAsyncData(NpgsqlFactory.Instance);

// Native async provider
builder.Services.AddAsyncData(myNativeAsyncProviderFactory);
```

## Design Decisions

| Decision | Rationale |
|---|---|
| `ValueTask` over `Task` | Most async calls in adapters complete synchronously (hot path) |
| Both sync + async on interfaces | True drop-in replacement requirement |
| Reuse `System.Data` enums/types | `ConnectionState`, `IsolationLevel`, `CommandType`, `CommandBehavior`, `IDataParameterCollection`, `IDbDataParameter` are fine as-is |
| Reuse `System.Data` in-memory types | `DataColumn`, `DataRow`, etc. have no I/O; reimplementing adds risk with no benefit |
| `IAsyncEnumerable` on reader | Modern .NET idiom, composes with LINQ; reader yields `this` for zero allocation |
| `protected abstract *CoreAsync` pattern | Same pattern as BCL, familiar to provider implementors |
| No mapping layer | Stays focused; mapping is Dapper's/EF's job |
| `.slnx` solution format | Consistent with other projects |

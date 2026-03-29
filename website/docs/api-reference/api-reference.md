---
sidebar_position: 1
title: API Reference
---

# API Reference

This page lists all public types and their key members across all AdoNet.Async packages.

## AdoNet.Async

Core interfaces and abstract base classes. Zero dependencies.

```bash
dotnet add package AdoNet.Async
```

### Interfaces

| Type | Description |
|---|---|
| `IAsyncDbConnection` | Async connection interface. Extends `IAsyncDisposable`, `IDisposable`. |
| `IAsyncDbCommand` | Async command interface. Extends `IAsyncDisposable`, `IDisposable`. |
| `IAsyncDbTransaction` | Async transaction interface. Extends `IAsyncDisposable`, `IDisposable`. |
| `IAsyncDataReader` | Async data reader. Extends `IAsyncDataRecord`, `IAsyncEnumerable<IAsyncDataRecord>`, `IAsyncDisposable`, `IDisposable`. |
| `IAsyncDataRecord` | Sync field accessors plus `IsDBNullAsync` and `GetFieldValueAsync<T>`. |
| `IAsyncDbProviderFactory` | Factory for creating connections, commands, and parameters. |

#### IAsyncDbConnection

| Member | Returns | Description |
|---|---|---|
| `ConnectionString` | `string` | Get/set the connection string |
| `ConnectionTimeout` | `int` | Connection timeout in seconds |
| `Database` | `string` | Current database name |
| `State` | `ConnectionState` | Current connection state |
| `OpenAsync(ct)` | `ValueTask` | Open the connection |
| `CloseAsync()` | `ValueTask` | Close the connection |
| `BeginTransactionAsync(ct)` | `ValueTask<IAsyncDbTransaction>` | Begin a transaction |
| `BeginTransactionAsync(il, ct)` | `ValueTask<IAsyncDbTransaction>` | Begin a transaction with isolation level |
| `ChangeDatabaseAsync(name, ct)` | `ValueTask` | Change the current database |
| `CreateCommand()` | `IAsyncDbCommand` | Create a new command |
| `Open()` | `void` | Sync open |
| `Close()` | `void` | Sync close |
| `BeginTransaction()` | `IAsyncDbTransaction` | Sync begin transaction |
| `ChangeDatabase(name)` | `void` | Sync change database |

#### IAsyncDbCommand

| Member | Returns | Description |
|---|---|---|
| `CommandText` | `string` | Get/set the SQL text |
| `CommandTimeout` | `int` | Get/set the timeout |
| `CommandType` | `CommandType` | Get/set the command type |
| `Connection` | `IAsyncDbConnection?` | Get/set the connection |
| `Transaction` | `IAsyncDbTransaction?` | Get/set the transaction |
| `Parameters` | `IDataParameterCollection` | Parameter collection |
| `UpdatedRowSource` | `UpdateRowSource` | Get/set update row source |
| `ExecuteReaderAsync(ct)` | `ValueTask<IAsyncDataReader>` | Execute and return a reader |
| `ExecuteReaderAsync(behavior, ct)` | `ValueTask<IAsyncDataReader>` | Execute with command behavior |
| `ExecuteNonQueryAsync(ct)` | `ValueTask<int>` | Execute and return rows affected |
| `ExecuteScalarAsync(ct)` | `ValueTask<object?>` | Execute and return first column of first row |
| `PrepareAsync(ct)` | `ValueTask` | Prepare the command |
| `CreateParameter()` | `IDbDataParameter` | Create a parameter |
| `Cancel()` | `void` | Cancel execution |

#### IAsyncDbTransaction

| Member | Returns | Description |
|---|---|---|
| `Connection` | `IAsyncDbConnection` | The owning connection |
| `IsolationLevel` | `IsolationLevel` | Transaction isolation level |
| `CommitAsync(ct)` | `ValueTask` | Commit the transaction |
| `RollbackAsync(ct)` | `ValueTask` | Roll back the transaction |
| `Commit()` | `void` | Sync commit |
| `Rollback()` | `void` | Sync rollback |

#### IAsyncDataReader

| Member | Returns | Description |
|---|---|---|
| `Depth` | `int` | Nesting depth |
| `IsClosed` | `bool` | Whether the reader is closed |
| `RecordsAffected` | `int` | Number of rows changed/inserted/deleted |
| `HasRows` | `bool` | Whether the result set has rows |
| `ReadAsync(ct)` | `ValueTask<bool>` | Advance to the next row |
| `NextResultAsync(ct)` | `ValueTask<bool>` | Advance to the next result set |
| `CloseAsync()` | `ValueTask` | Close the reader |
| `GetSchemaTableAsync(ct)` | `ValueTask<DataTable>` | Get the schema table |
| `GetSchemaTable()` | `DataTable` | Sync get schema table |

Also implements `IAsyncEnumerable<IAsyncDataRecord>` for `await foreach` support.

#### IAsyncDataRecord

| Member | Returns | Description |
|---|---|---|
| `FieldCount` | `int` | Number of columns |
| `this[int]` | `object` | Get value by ordinal |
| `this[string]` | `object` | Get value by name |
| `GetBoolean(i)` ... `GetDateTime(i)` | various | Typed field accessors |
| `GetName(i)` | `string` | Column name by ordinal |
| `GetOrdinal(name)` | `int` | Ordinal by column name |
| `GetValue(i)` | `object` | Value as object |
| `GetValues(values)` | `int` | Fill array with values |
| `IsDBNull(i)` | `bool` | Check for null |
| `IsDBNullAsync(i, ct)` | `ValueTask<bool>` | Async null check |
| `GetFieldValueAsync<T>(i, ct)` | `ValueTask<T>` | Async typed field access |

#### IAsyncDbProviderFactory

| Member | Returns | Description |
|---|---|---|
| `CreateConnection()` | `IAsyncDbConnection` | Create a new connection |
| `CreateCommand()` | `IAsyncDbCommand` | Create a new command |
| `CreateParameter()` | `IDbDataParameter` | Create a new parameter |

### Abstract Base Classes

| Type | Implements | Description |
|---|---|---|
| `AsyncDbConnection` | `IAsyncDbConnection` | Base class with sync-over-async bridge |
| `AsyncDbCommand` | `IAsyncDbCommand` | Base class with sync-over-async bridge |
| `AsyncDbDataReader` | `IAsyncDataReader` | Base class with `IAsyncEnumerable` iterator |
| `AsyncDbTransaction` | `IAsyncDbTransaction` | Base class with sync-over-async bridge |

---

## AdoNet.Async.DataSet

Async DataTable, DataSet, DataRow, and DataAdapter. Depends on `ZeroAlloc.AsyncEvents`.

```bash
dotnet add package AdoNet.Async.DataSet
```

| Type | Description |
|---|---|
| `AsyncDataTable` | Async wrapper around `DataTable`. Exposes 9 async events. |
| `AsyncDataTable<TRow>` | Generic base for typed DataTable with typed row collection. |
| `AsyncDataSet` | Async wrapper around `DataSet`. |
| `AsyncDataRow` | Async row with read-only indexers and `SetValueAsync`/`DeleteAsync`. |
| `AsyncDataRowCollection` | Async row collection with `AddAsync`, `RemoveAsync`. |
| `AsyncDataRowCollection<TRow>` | Generic typed row collection. |
| `AsyncDataTableCollection` | Collection of `AsyncDataTable` within an `AsyncDataSet`. |
| `AsyncDataAdapter` | Abstract base for async data adapters with `FillAsync`/`UpdateAsync`. |

#### AsyncDataTable -- Key Members

| Member | Returns | Description |
|---|---|---|
| `TableName` | `string` | Get/set table name |
| `Columns` | `DataColumnCollection` | Column definitions |
| `Rows` | `AsyncDataRowCollection` | Row collection |
| `Constraints` | `ConstraintCollection` | Table constraints |
| `PrimaryKey` | `DataColumn[]` | Get/set primary key columns |
| `DataSet` | `AsyncDataSet?` | Parent DataSet |
| `NewRow()` | `AsyncDataRow` | Create a new row |
| `LoadAsync(reader, ct)` | `ValueTask<int>` | Load from `IAsyncDataReader` |
| `LoadAsync(reader, loadOption, ct)` | `ValueTask<int>` | Load with load option |
| `AcceptChangesAsync(ct)` | `ValueTask` | Accept all changes |
| `ClearAsync(ct)` | `ValueTask` | Clear all rows |
| `Clone()` | `AsyncDataTable` | Clone schema only |
| `Copy()` | `AsyncDataTable` | Clone schema and data |
| `GetChanges()` | `AsyncDataTable?` | Get changed rows |
| `Select(filter, sort)` | `AsyncDataRow[]` | Filter and sort rows |

Async events: `ColumnChangingAsync`, `ColumnChangedAsync`, `RowChangingAsync`, `RowChangedAsync`, `RowDeletingAsync`, `RowDeletedAsync`, `TableClearingAsync`, `TableClearedAsync`, `TableNewRowAsync`.

#### AsyncDataRow -- Key Members

| Member | Returns | Description |
|---|---|---|
| `this[string]` | `object` | Read value by column name |
| `this[int]` | `object` | Read value by ordinal |
| `this[column, version]` | `object` | Read value for a specific version |
| `RowState` | `DataRowState` | Current row state |
| `HasErrors` | `bool` | Whether the row has errors |
| `Table` | `AsyncDataTable` | Parent table |
| `SetValueAsync(columnName, value, ct)` | `ValueTask` | Set column value (fires events) |
| `SetValueAsync(columnIndex, value, ct)` | `ValueTask` | Set column value by index |
| `DeleteAsync(ct)` | `ValueTask` | Mark row as deleted |
| `BeginEditAsync(ct)` | `ValueTask` | Begin edit mode |
| `EndEditAsync(ct)` | `ValueTask` | End edit mode (fires events) |
| `CancelEditAsync(ct)` | `ValueTask` | Cancel edit mode |
| `AcceptChangesAsync(ct)` | `ValueTask` | Accept row changes |
| `RejectChangesAsync(ct)` | `ValueTask` | Reject row changes |

#### AsyncDataRowCollection -- Key Members

| Member | Returns | Description |
|---|---|---|
| `Count` | `int` | Number of rows |
| `this[int]` | `AsyncDataRow` | Row by index |
| `AddAsync(row, ct)` | `ValueTask` | Add a row (fires events) |
| `AddAsync(values, ct)` | `ValueTask` | Add row from value array |
| `RemoveAsync(row, ct)` | `ValueTask` | Remove a row (fires events) |
| `RemoveAtAsync(index, ct)` | `ValueTask` | Remove row by index |

#### AsyncDataAdapter -- Key Members

| Member | Returns | Description |
|---|---|---|
| `SelectCommand` | `IAsyncDbCommand?` | Select command |
| `InsertCommand` | `IAsyncDbCommand?` | Insert command |
| `UpdateCommand` | `IAsyncDbCommand?` | Update command |
| `DeleteCommand` | `IAsyncDbCommand?` | Delete command |
| `AcceptChangesDuringFill` | `bool` | Accept changes after fill (default: `true`) |
| `AcceptChangesDuringUpdate` | `bool` | Accept changes after update (default: `true`) |
| `FillAsync(table, ct)` | `ValueTask<int>` | Fill an `AsyncDataTable` |
| `FillAsync(dataSet, ct)` | `ValueTask<int>` | Fill an `AsyncDataSet` |
| `UpdateAsync(table, ct)` | `ValueTask<int>` | Send changes to database |
| `UpdateAsync(dataSet, ct)` | `ValueTask<int>` | Send changes for all tables |

---

## AdoNet.Async.Adapters

Adapter wrappers and DI extensions. Depends on `Microsoft.Extensions.DependencyInjection.Abstractions`.

```bash
dotnet add package AdoNet.Async.Adapters
```

| Type | Description |
|---|---|
| `AdapterDbConnection` | Wraps `DbConnection` as `IAsyncDbConnection`. |
| `AdapterDbCommand` | Wraps `DbCommand` as `IAsyncDbCommand`. |
| `AdapterDbDataReader` | Wraps `DbDataReader` as `IAsyncDataReader`. |
| `AdapterDbTransaction` | Wraps `DbTransaction` as `IAsyncDbTransaction`. |
| `AdapterDbProviderFactory` | Wraps `DbProviderFactory` as `IAsyncDbProviderFactory`. |
| `AdapterDbDataAdapter` | Concrete `AsyncDataAdapter` using adapter commands. |
| `DbConnectionExtensions` | Extension method `AsAsync()` on `DbConnection`. |
| `ServiceCollectionExtensions` | Extension method `AddAsyncData()` on `IServiceCollection`. |

#### DbConnectionExtensions

| Method | Returns | Description |
|---|---|---|
| `AsAsync(this DbConnection)` | `IAsyncDbConnection` | Wrap any `DbConnection` |

#### ServiceCollectionExtensions

| Method | Returns | Description |
|---|---|---|
| `AddAsyncData(services, DbProviderFactory)` | `IServiceCollection` | Register from `DbProviderFactory` |
| `AddAsyncData(services, IAsyncDbProviderFactory)` | `IServiceCollection` | Register custom factory |

#### Explicit Casts

All adapter types support explicit casts back to the inner type:

```csharp
DbConnection inner = (DbConnection)(AdapterDbConnection)asyncConnection;
DbCommand inner = (DbCommand)(AdapterDbCommand)asyncCommand;
DbDataReader inner = (DbDataReader)(AdapterDbDataReader)asyncReader;
DbTransaction inner = (DbTransaction)(AdapterDbTransaction)asyncTransaction;
```

---

## AdoNet.Async.Serialization.NewtonsoftJson

Newtonsoft.Json converters. Wire-compatible with `Json.Net.DataSetConverters`.

```bash
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson
```

| Type | Description |
|---|---|
| `AsyncDataTableConverter` | `JsonConverter<AsyncDataTable>` for Newtonsoft.Json |
| `AsyncDataSetConverter` | `JsonConverter<AsyncDataSet>` for Newtonsoft.Json |

Namespace: `System.Data.Async.Converters`

---

## AdoNet.Async.Serialization.SystemTextJson

System.Text.Json converters. Same wire format as Newtonsoft.Json converters.

```bash
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

| Type | Description |
|---|---|
| `AsyncDataTableJsonConverter` | `JsonConverter<AsyncDataTable>` for System.Text.Json |
| `AsyncDataSetJsonConverter` | `JsonConverter<AsyncDataSet>` for System.Text.Json |

Namespace: `System.Data.Async.Converters.SystemTextJson`

---

## AdoNet.Async.DataSet.Generator

Roslyn source generator that produces typed async DataSet classes from `.xsd` schema files.

```bash
dotnet add package AdoNet.Async.DataSet.Generator
```

This is a compile-time-only package. It does not ship any runtime types. Instead, it generates:

| Generated Type | Base Class | Description |
|---|---|---|
| `Async{DataSetName}` | `AsyncDataSet` | Typed DataSet with typed table properties |
| `Async{TableName}DataTable` | `AsyncDataTable<TRow>` | Typed table with `Add{Table}RowAsync`, `FindBy{PK}` |
| `Async{TableName}Row` | `AsyncDataRow` | Typed row with property accessors, `Set{Col}Async`, `Is{Col}Null`, `Set{Col}Null` |
| `Async{TableName}RowChangeEventArgs` | `EventArgs` | Typed event args for row changes |

### Supported XSD Annotations

| Annotation | Attribute | Description |
|---|---|---|
| `msdata:AutoIncrement` | Column | Auto-increment column |
| `msdata:AutoIncrementSeed` | Column | Auto-increment seed value |
| `msdata:AutoIncrementStep` | Column | Auto-increment step |
| `msdata:ReadOnly` | Column | Read-only column |
| `msdata:PrimaryKey` | Constraint | Mark as primary key |
| `codegen:typedName` | Column | Override property name |
| `codegen:typedPlural` | Table | Override collection name |
| `codegen:typedParent` | Relation | Parent navigation property name |
| `codegen:typedChildren` | Relation | Child navigation property name |
| `codegen:nullValue` | Column | Null behavior: `_throw`, `_null`, `_empty`, or replacement value |
| `msdata:DefaultValue` | Column | Default value |
| `msdata:Expression` | Column | Computed expression |
| `msdata:DataType` | Column | Override the CLR data type |

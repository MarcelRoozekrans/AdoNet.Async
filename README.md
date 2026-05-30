<p align="center">
  <img src="assets/logo.svg" alt="AdoNet.Async" width="128" />
</p>

<h1 align="center">AdoNet.Async</h1>

<p align="center">Async-first interfaces and base classes for ADO.NET. A drop-in replacement that brings modern <code>async/await</code>, <code>IAsyncEnumerable</code>, and <code>ValueTask</code> support to <code>System.Data</code>.</p>

[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.svg)](https://www.nuget.org/packages/AdoNet.Async)
[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.DataSet.svg)](https://www.nuget.org/packages/AdoNet.Async.DataSet)
[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.Serialization.NewtonsoftJson.svg)](https://www.nuget.org/packages/AdoNet.Async.Serialization.NewtonsoftJson)
[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.Serialization.SystemTextJson.svg)](https://www.nuget.org/packages/AdoNet.Async.Serialization.SystemTextJson)
[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.Adapters.svg)](https://www.nuget.org/packages/AdoNet.Async.Adapters)
[![NuGet](https://img.shields.io/nuget/v/AdoNet.Async.DataSet.Generator.svg)](https://www.nuget.org/packages/AdoNet.Async.DataSet.Generator)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://marcelroozekrans.github.io/AdoNet.Async/)

> **Full documentation:** [marcelroozekrans.github.io/AdoNet.Async](https://marcelroozekrans.github.io/AdoNet.Async/)

## Installation

```bash
# Core interfaces and abstract base classes (zero dependencies)

[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

dotnet add package AdoNet.Async

# Async DataTable, DataSet, DataAdapter
dotnet add package AdoNet.Async.DataSet

# Newtonsoft.Json converters (Json.Net.DataSetConverters wire format)
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson

# System.Text.Json converters (same wire format)
dotnet add package AdoNet.Async.Serialization.SystemTextJson

# Adapter wrappers for existing ADO.NET providers + DI extensions
dotnet add package AdoNet.Async.Adapters

# Typed DataSet source generator (generates typed async classes from .xsd)
dotnet add package AdoNet.Async.DataSet.Generator
```

## Quick Start

### Migrate existing code with `.AsAsync()`

Wrap any `DbConnection` to get a fully async interface:

```csharp
using System.Data.Async.Adapters;

DbConnection sqlConnection = new SqlConnection(connectionString);
IAsyncDbConnection connection = sqlConnection.AsAsync();

await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

IAsyncDbCommand cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name FROM Users";
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
```

### Iterate results with `await foreach`

`IAsyncDataReader` implements `IAsyncEnumerable<IAsyncDataRecord>`, so you can stream rows naturally:

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    await foreach (IAsyncDataRecord record in reader)
    {
        Console.WriteLine($"{record.GetInt32(0)}: {record.GetString(1)}");
    }
}
```

### Send multiple statements as a single batch

`IAsyncDbBatch` mirrors `System.Data.Common.DbBatch` — each statement carries its own
parameters and the whole batch ships to the server in one round-trip. Useful for
"head + detail" reads or grouped INSERTs:

```csharp
if (!connection.CanCreateBatch)
{
    // Provider does not support batches. Fall back to ;-joined SQL on a single command.
    return;
}

await using var batch = connection.CreateBatch();

var head = batch.CreateBatchCommand();
head.CommandText = "SELECT Id, Total FROM Orders WHERE Id = @id;";
var pHead = head.CreateParameter();
pHead.ParameterName = "@id";
pHead.Value = orderId;
head.Parameters.Add(pHead);
batch.BatchCommands.Add(head);

var lines = batch.CreateBatchCommand();
lines.CommandText = "SELECT Sku, Quantity FROM OrderLines WHERE OrderId = @id;";
var pLines = lines.CreateParameter();
pLines.ParameterName = "@id";
pLines.Value = orderId;
lines.Parameters.Add(pLines);
batch.BatchCommands.Add(lines);

await using var reader = await batch.ExecuteReaderAsync();
// reader.ReadAsync(), reader.NextResultAsync(), reader.ReadAsync(), ...
```

Always branch on `IAsyncDbConnection.CanCreateBatch` first — Npgsql 6+,
Microsoft.Data.Sqlite 9+, and SqlClient support batches; older providers throw
`NotSupportedException` from `CreateBatch()`.

### Fill an AsyncDataTable

Use `FillAsync` with the `AdapterDbDataAdapter` to populate tables asynchronously:

```csharp
using System.Data.Async.DataSet;
using System.Data.Async.Adapters;

var table = new AsyncDataTable("Users");
var adapter = new AdapterDbDataAdapter(cmd);
int rowCount = await adapter.FillAsync(table);

foreach (AsyncDataRow row in table.Rows)
{
    Console.WriteLine(row["Name"]);
}
```

### Mutate rows asynchronously

Row mutations go through async methods on `AsyncDataRow`. Indexers are read-only; all writes use `SetValueAsync`:

```csharp
// Add a row
var row = table.NewRow();
await row.SetValueAsync("Name", "Alice");
await row.SetValueAsync("Age", 30);
await table.Rows.AddAsync(row);

// Or pass values as an array
await table.Rows.AddAsync(["Alice", 30]);

// Modify an existing row
await table.Rows[0].SetValueAsync("Name", "Bob");

// Delete a row
await table.Rows[0].DeleteAsync();

// Accept / reject changes
await table.AcceptChangesAsync();
await table.ClearAsync();
```

### Async events on AsyncDataTable

`AsyncDataTable` exposes 9 async events backed by `ZeroAlloc.AsyncEvents` (zero-alloc, sequential dispatch):

```csharp
var table = new AsyncDataTable("Orders");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Total", typeof(decimal));

// Subscribe to async events
table.RowChangingAsync += async (args, ct) =>
{
    await ValidateAsync(args.Row, ct);
};

table.ColumnChangedAsync += async (args, ct) =>
{
    await AuditAsync(args.Column!.ColumnName, args.ProposedValue, ct);
};

table.RowDeletedAsync += async (args, ct) =>
{
    await LogDeletionAsync(args.Row, ct);
};

// Mutations fire async events automatically
var row = table.NewRow();
await table.Rows.AddAsync(row);                       // fires TableNewRowAsync + RowChangedAsync
await row.SetValueAsync("Total", 99.99m);             // fires RowChangingAsync + ColumnChangingAsync + ColumnChangedAsync + RowChangedAsync
await row.DeleteAsync();                               // fires RowDeletingAsync + RowDeletedAsync
await table.ClearAsync();                             // fires TableClearingAsync + TableClearedAsync
```

Sync event subscribers (e.g. `table.RowChanged += ...`) continue to work unchanged via the inner `DataTable`.

### Typed DataSets from .xsd

Add `.xsd` schema files as `AdditionalFiles` and the source generator produces fully typed async DataSet classes:

```xml
<!-- In your .csproj -->
<ItemGroup>
  <PackageReference Include="AdoNet.Async.DataSet.Generator" />
  <AdditionalFiles Include="Schemas\OrdersDS.xsd" />
</ItemGroup>
```

The generator reads your `.xsd` and creates strongly typed classes:

```csharp
using System.Data.Async.DataSet;

// Fully typed — no string column names, no casts
using var ds = new AsyncOrdersDS();

var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice");
await customer.SetEmailAsync("alice@example.com");

// FK-aware: pass parent row instead of raw FK value
var order = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m);

// Typed relation navigation
AsyncOrdersDSOrderRow[] orders = customer.GetOrderRows();
AsyncOrdersDSCustomerRow? parent = order.CustomerRow;

// Typed null handling
if (!customer.IsEmailNull())
    Console.WriteLine(customer.Email);

// Primary key lookup
var found = ds.Customer.FindByCustomerId(1);
```

All `codegen:` and `msdata:` XSD annotations are supported: `typedName`, `typedPlural`, `typedParent`, `typedChildren`, `nullValue` (_throw, _null, _empty, replacement), `AutoIncrement`, `DefaultValue`, `ReadOnly`, `Expression`, `DataType`, composite primary keys, and foreign key constraints. Columns may be declared as `xs:element` (inside `xs:sequence`) or as `xs:attribute` directly in the table's `xs:complexType` — both are fully supported, with `use="required"` marking an attribute column non-nullable. Namespace-prefixed XPath selectors (e.g. `mstns:TableName`) are handled transparently.

### JSON serialization with Newtonsoft.Json

`AsyncDataTable` and `AsyncDataSet` include converters compatible with the Json.Net.DataSetConverters format:

```csharp
using System.Data.Async.Converters;
using Newtonsoft.Json;

var settings = new JsonSerializerSettings();
settings.Converters.Add(new AsyncDataTableConverter());
settings.Converters.Add(new AsyncDataSetConverter());

// Serialize
string json = JsonConvert.SerializeObject(table, settings);

// Deserialize
var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);
```

### JSON serialization with System.Text.Json

`AsyncDataTable` and `AsyncDataSet` also work with `System.Text.Json`, producing the same wire format:

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Text.Json;

var options = new JsonSerializerOptions();
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());

// Serialize
string json = JsonSerializer.Serialize(table, options);

// Deserialize
var restored = JsonSerializer.Deserialize<AsyncDataTable>(json, options);
```

Both serializers produce identical JSON — you can serialize with one and deserialize with the other.

### Dependency Injection

Register an async provider factory from any existing `DbProviderFactory`:

```csharp
using System.Data.Async.Adapters;

services.AddAsyncData(SqlClientFactory.Instance);

// Then inject IAsyncDbProviderFactory anywhere:
public class MyRepository(IAsyncDbProviderFactory factory)
{
    public async Task<string> GetNameAsync(int id)
    {
        await using var conn = factory.CreateConnection();
        conn.ConnectionString = "...";
        await conn.OpenAsync();
        // ...
    }
}
```

## Packages

| Package | Description | Dependencies | NativeAOT |
|---------|-------------|-------------|---|
| **AdoNet.Async** | Core async interfaces (`IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, etc.) and abstract base classes | None | ✅ |
| **AdoNet.Async.DataSet** | `AsyncDataTable`, `AsyncDataSet`, `AsyncDataRow`, `AsyncDataRowCollection`, `AsyncDataAdapter`, and 9 async events | ZeroAlloc.AsyncEvents | ❌ * |
| **AdoNet.Async.Serialization.NewtonsoftJson** | `AsyncDataTableConverter`, `AsyncDataSetConverter` for Newtonsoft.Json. Wire-compatible with `Json.Net.DataSetConverters`. | AdoNet.Async.DataSet, Newtonsoft.Json | ❌ |
| **AdoNet.Async.Serialization.SystemTextJson** | `AsyncDataTableJsonConverter`, `AsyncDataSetJsonConverter` for System.Text.Json. Same wire format. | AdoNet.Async.DataSet | ⚠️ ** |
| **AdoNet.Async.Adapters** | Adapter wrappers (`AdapterDbConnection`, etc.), `.AsAsync()` extension, DI registration | Microsoft.Extensions.DependencyInjection.Abstractions | ✅ |
| **AdoNet.Async.DataSet.Generator** | Roslyn source generator — produces typed `AsyncDataTable<TRow>`, `AsyncDataRow` subclasses from `.xsd` schema files. Supports all `codegen:` and `msdata:` annotations. | AdoNet.Async.DataSet (at compile time) | N/A *** |

\* The wrapper code itself is reflection-free, but `AsyncDataTable.WriteXmlAsync` / `ReadXmlAsync` / `LoadAsync` (via `DataColumnCollection.Add(String, Type)`) call BCL members annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Marking the package AOT-compatible would require propagating those annotations across ~8 methods + resolving an IL2072 type-flow on `DataColumn.Add` — tracked as a separate workstream.
\** Uses reflection-based `JsonSerializer` by default; pass a `JsonTypeInfo<T>` / `JsonSerializerContext` to make consumer call sites AOT-clean.
\*** Build-time-only assembly; never loaded by the user's runtime, so AOT compatibility doesn't apply.

### NativeAOT compatibility

Three of the six packages declare [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) and are verified end-to-end by the [`aot-smoke`](.github/workflows/aot-smoke.yml) CI job, which publishes `tests/System.Data.Async.AotSmoke` with `PublishAot=true` against an in-memory SQLite database. The smoke test exercises the `AsAsync()` extension path, the DI registration path, and multi-result-set walking via `NextResultAsync` — the same shapes a downstream source-generator data-access library would emit.

Consumer-side requirements for AOT use:

- Pass the `DbProviderFactory` instance directly to `services.AddAsyncData(NpgsqlFactory.Instance)` — do not rely on `DbProviderFactories.GetFactory(invariantName)` (the reflective registration path is not AOT-safe).
- Use an AOT-compatible provider — Microsoft.Data.Sqlite is verified; Npgsql 8.0+ supports AOT as a raw driver.
- If you reference `AdoNet.Async.DataSet`, avoid the `DataTable.WriteXmlSchema` / `ReadXml` entry points (BCL-internal reflection).
- If you reference `AdoNet.Async.Serialization.SystemTextJson`, supply a `JsonSerializerContext` so System.Text.Json's source generator owns the type metadata.

## Validation & Benchmarks

The library includes a comprehensive validation test suite (40 tests) that proves behavioral parity with raw ADO.NET, an integration test suite (35 tests) covering interop and cross-serialization compatibility, and a benchmark suite measuring async wrapper and serialization overhead.

### Running

```bash
# Run all validation tests
dotnet test tests/System.Data.Async.Validation.Tests

# Run benchmarks (Release mode required)
dotnet run --project tests/System.Data.Async.Benchmarks -c Release
```

### Benchmark Results

Measured on Intel Core i9-12900HK, .NET 10.0.4, BenchmarkDotNet v0.15.8 (ShortRun).

#### Command Execution

| Method                      | Mean      | Ratio | Allocated | Alloc Ratio |
|---------------------------- |----------:|------:|----------:|------------:|
| Raw_ExecuteScalar           |  6.581 us |  1.00 |     720 B |        1.00 |
| Async_ExecuteScalar         | 12.069 us |  1.83 |     912 B |        1.27 |
| Raw_ExecuteNonQuery         |  9.355 us |  1.42 |     480 B |        0.67 |
| Async_ExecuteNonQuery       | 16.063 us |  2.44 |     528 B |        0.73 |
| Raw_ExecuteReader_Iterate   | 10.087 us |  1.53 |     704 B |        0.98 |
| Async_ExecuteReader_Iterate | 16.023 us |  2.44 |     992 B |        1.38 |

#### Connection Open/Close

| Method          | Mean     | Ratio | Allocated | Alloc Ratio |
|---------------- |---------:|------:|----------:|------------:|
| Raw_OpenClose   | 15.54 us |  1.00 |     384 B |        1.00 |
| Async_OpenClose | 14.35 us |  0.92 |     408 B |        1.06 |

#### Data Reader Iteration (50 rows)

| Method                     | Mean     | Ratio | Allocated | Alloc Ratio |
|--------------------------- |---------:|------:|----------:|------------:|
| Raw_ReadAll_Fields         | 20.99 us |  1.00 |    3.7 KB |        1.00 |
| Async_ReadAll_ManualLoop   | 33.13 us |  1.58 |   3.98 KB |        1.08 |
| Async_ReadAll_AwaitForeach | 30.74 us |  1.46 |   4.09 KB |        1.11 |

#### DataAdapter Fill

| Method     | RowLimit | Mean       | Ratio | Allocated  | Alloc Ratio |
|----------- |--------- |-----------:|------:|-----------:|------------:|
| Raw_Fill   | 10       |   595.5 us |  1.00 |  94.42 KB  |        1.00 |
| Async_Fill | 10       |   872.3 us |  1.46 |  78.88 KB  |        0.84 |
| Raw_Fill   | 100      | 1,293.8 us |  1.00 | 160.21 KB  |        1.00 |
| Async_Fill | 100      | 1,173.8 us |  0.92 | 117.40 KB  |        0.73 |

#### Serialization (AsyncDataTable, 10 / 100 rows)

| Method | RowCount | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|---|
| Newtonsoft_Serialize | 10 | 12.30 µs | 1.00 | 29.36 KB | 1.00 |
| STJ_Serialize | 10 | 10.05 µs | 0.84 | 11.74 KB | 0.40 |
| Newtonsoft_Deserialize | 10 | 60.52 µs | 5.04 | 34.38 KB | 1.17 |
| STJ_Deserialize | 10 | 54.70 µs | 4.55 | 32.73 KB | 1.12 |
| Newtonsoft_Serialize | 100 | 70.08 µs | 1.00 | 114.63 KB | 1.00 |
| STJ_Serialize | 100 | 39.28 µs | 0.57 | 65.62 KB | 0.57 |
| Newtonsoft_Deserialize | 100 | 165.16 µs | 2.41 | 101.45 KB | 0.88 |
| STJ_Deserialize | 100 | 143.26 µs | 2.09 | 98.68 KB | 0.86 |

> Measured on .NET 10, Release mode, Windows 11. Ratio is relative to `Newtonsoft_Serialize` per row count.

#### Transactions

| Method              | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------------- |-----------:|------:|----------:|------------:|
| Raw_BeginCommit     |   5.948 us |  1.00 |   1.64 KB |        1.00 |
| Async_BeginCommit   | 240.697 us | 40.47 |   1.79 KB |        1.09 |
| Raw_BeginRollback   |   6.040 us |  1.00 |   1.65 KB |        1.00 |
| Async_BeginRollback | 233.939 us | 39.33 |   1.73 KB |        1.06 |

> **Note:** Transaction overhead is high in this microbenchmark because SQLite serializes write transactions. In real-world usage with network-bound databases (SQL Server, PostgreSQL), the async overhead is negligible compared to I/O latency. Memory allocation overhead is consistently minimal (< 200 bytes per operation).

### Validation Coverage

| Test Class | Tests | What it validates |
|---|---|---|
| ConnectionParityTests | 3 | State transitions, repeated open/close, timeout |
| CommandExecutionParityTests | 7 | ExecuteScalar, ExecuteNonQuery, ExecuteReader, parameters, Prepare, CommandBehavior |
| ReaderParityTests | 11 | Field access, schema, IsDBNull, await foreach, HasRows, NextResult, GetFieldValueAsync, Close/IsClosed, Depth, typed accessors |
| TransactionParityTests | 3 | Commit, Rollback, IsolationLevel |
| DataAdapterParityTests | 3 | Fill DataTable/DataSet, Update roundtrip |
| SerializationParityTests | 4 | XML data/schema roundtrip, JSON roundtrip |
| DataTableInteropTests | 7 | `DataTable` ↔ `AsyncDataTable` wrapping is lossless (all row states, constraints, extended properties) |
| DataSetInteropTests | 6 | `DataSet` ↔ `AsyncDataSet` wrapping is lossless (relations, constraints, row states across tables) |
| NewtonsoftJsonCrossCompatibilityTests | 12 | Wire-compatibility with `Json.Net.DataSetConverters` in both directions; all row states, types, AutoIncrement |
| SystemTextJsonCrossCompatibilityTests | 10 | STJ produces identical JSON as Newtonsoft; cross-deserializer round-trips; `AsyncDataSet` round-trip |
| EventParityTests | 5 | Row/Column/Table events fire in same order |
| EdgeCaseParityTests | 4 | Empty/large results, cancellation, empty fill |

## Design Decisions

- **`ValueTask` everywhere** -- All async methods return `ValueTask` or `ValueTask<T>` for zero-allocation on synchronous completion paths.
- **Dual sync/async** -- Every interface exposes both synchronous and asynchronous members, enabling gradual migration without breaking existing code.
- **`IAsyncEnumerable<IAsyncDataRecord>`** -- `IAsyncDataReader` implements `IAsyncEnumerable`, enabling `await foreach` iteration over result sets.
- **Adapter pattern** -- Existing `DbConnection`/`DbCommand`/`DbDataReader` instances are wrapped, not replaced. No provider-specific code needed.
- **Zero core dependencies** -- The `System.Data.Async` package has no external dependencies; adapters and DataSet packages only reference what they need.
- **Async events via `AsyncDataRow`** -- Row mutations on `AsyncDataTable` go through `AsyncDataRow`, whose indexers are read-only. All writes use `SetValueAsync`/`DeleteAsync`/etc., which fire async events before and after each mutation. This makes async event subscribers a compile-time guarantee rather than an opt-in. Sync events on the inner `DataTable` continue to fire for backward-compatible sync consumers. Async events use `ZeroAlloc.AsyncEvents` in `Sequential` mode (zero-alloc, faster than a sync multicast delegate with no subscribers).

## License

MIT

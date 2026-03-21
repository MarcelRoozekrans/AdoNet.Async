# System.Data.Async

Async-first interfaces and base classes for ADO.NET. A drop-in replacement that brings modern `async/await`, `IAsyncEnumerable`, and `ValueTask` support to `System.Data`.

[![NuGet](https://img.shields.io/nuget/v/System.Data.Async.svg)](https://www.nuget.org/packages/System.Data.Async)
[![NuGet](https://img.shields.io/nuget/v/System.Data.Async.DataSet.svg)](https://www.nuget.org/packages/System.Data.Async.DataSet)
[![NuGet](https://img.shields.io/nuget/v/System.Data.Async.Adapters.svg)](https://www.nuget.org/packages/System.Data.Async.Adapters)

## Installation

```bash
# Core interfaces and abstract base classes (zero dependencies)
dotnet add package System.Data.Async

# Async DataTable, DataSet, DataAdapter + JSON converters
dotnet add package System.Data.Async.DataSet

# Adapter wrappers for existing ADO.NET providers + DI extensions
dotnet add package System.Data.Async.Adapters
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

### Fill an AsyncDataTable

Use `FillAsync` with the `AdapterDbDataAdapter` to populate tables asynchronously:

```csharp
using System.Data.Async.DataSet;
using System.Data.Async.Adapters;

var table = new AsyncDataTable("Users");
var adapter = new AdapterDbDataAdapter(cmd);
int rowCount = await adapter.FillAsync(table);

foreach (DataRow row in table.Rows)
{
    Console.WriteLine(row["Name"]);
}
```

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

| Package | Description | Dependencies |
|---------|-------------|-------------|
| **System.Data.Async** | Core async interfaces (`IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, etc.) and abstract base classes | None |
| **System.Data.Async.DataSet** | `AsyncDataTable`, `AsyncDataSet`, `AsyncDataAdapter` + Newtonsoft.Json converters | Newtonsoft.Json |
| **System.Data.Async.Adapters** | Adapter wrappers (`AdapterDbConnection`, etc.), `.AsAsync()` extension, DI registration | Microsoft.Extensions.DependencyInjection.Abstractions |

## Design Decisions

- **`ValueTask` everywhere** -- All async methods return `ValueTask` or `ValueTask<T>` for zero-allocation on synchronous completion paths.
- **Dual sync/async** -- Every interface exposes both synchronous and asynchronous members, enabling gradual migration without breaking existing code.
- **`IAsyncEnumerable<IAsyncDataRecord>`** -- `IAsyncDataReader` implements `IAsyncEnumerable`, enabling `await foreach` iteration over result sets.
- **Adapter pattern** -- Existing `DbConnection`/`DbCommand`/`DbDataReader` instances are wrapped, not replaced. No provider-specific code needed.
- **Zero core dependencies** -- The `System.Data.Async` package has no external dependencies; adapters and DataSet packages only reference what they need.

## License

MIT

---
sidebar_position: 1
title: Migrate Existing Code
---

# Migrate Existing Code

This guide walks through migrating a traditional ADO.NET codebase to AdoNet.Async, step by step. Each step is independent -- you can stop at any point and have a working application.

## Step 1: Add Packages

```bash
# Core interfaces + adapter wrappers
dotnet add package AdoNet.Async
dotnet add package AdoNet.Async.Adapters

# If using DataTable/DataSet
dotnet add package AdoNet.Async.DataSet

# If using JSON serialization (pick one or both)
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

## Step 2: Wrap Connections with `.AsAsync()`

**Before:**

```csharp
using System.Data.Common;
using Microsoft.Data.SqlClient;

DbConnection connection = new SqlConnection(connectionString);
connection.Open();
```

**After:**

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

IAsyncDbConnection connection = new SqlConnection(connectionString).AsAsync();
await connection.OpenAsync();
```

The `.AsAsync()` extension method wraps the connection. All objects created from this connection (commands, transactions, readers) are automatically wrapped.

:::tip
If your code already uses `DbConnection` variables rather than concrete types like `SqlConnection`, the change is minimal -- just add `.AsAsync()` and change the variable type to `IAsyncDbConnection`.
:::

## Step 3: Change ExecuteReader to ExecuteReaderAsync

**Before:**

```csharp
DbCommand cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name FROM Users WHERE Active = @active";

var param = cmd.CreateParameter();
param.ParameterName = "@active";
param.Value = true;
cmd.Parameters.Add(param);

IDataReader reader = cmd.ExecuteReader();
```

**After:**

```csharp
IAsyncDbCommand cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name FROM Users WHERE Active = @active";

var param = cmd.CreateParameter();
param.ParameterName = "@active";
param.Value = true;
cmd.Parameters.Add(param);

IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
```

The parameter handling code is identical. Only the command type, execute call, and reader type change.

Similarly for other execute methods:

```csharp
// Before
int affected = cmd.ExecuteNonQuery();
object? scalar = cmd.ExecuteScalar();

// After
int affected = await cmd.ExecuteNonQueryAsync();
object? scalar = await cmd.ExecuteScalarAsync();
```

## Step 4: Change while(reader.Read()) to await foreach

**Before:**

```csharp
using (IDataReader reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        int id = reader.GetInt32(0);
        string name = reader.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

**After (option A -- await foreach):**

```csharp
await using (IAsyncDataReader reader = await cmd.ExecuteReaderAsync())
{
    await foreach (IAsyncDataRecord record in reader)
    {
        int id = record.GetInt32(0);
        string name = record.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

**After (option B -- manual async loop):**

```csharp
await using (IAsyncDataReader reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
    {
        int id = reader.GetInt32(0);
        string name = reader.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

:::info
`await foreach` is the recommended approach. It is slightly more concise and the reader handles the async enumeration internally. Both approaches yield the same performance characteristics.
:::

## Step 5: Change DataTable to AsyncDataTable

**Before:**

```csharp
using System.Data;

var table = new DataTable("Users");
var adapter = new SqlDataAdapter("SELECT * FROM Users", connection);
adapter.Fill(table);

foreach (DataRow row in table.Rows)
{
    Console.WriteLine(row["Name"]);
}
```

**After:**

```csharp
using System.Data.Async.DataSet;
using System.Data.Async.Adapters;

var table = new AsyncDataTable("Users");
var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT * FROM Users";

var adapter = new AdapterDbDataAdapter(cmd);
await adapter.FillAsync(table);

foreach (AsyncDataRow row in table.Rows)
{
    Console.WriteLine(row["Name"]);
}
```

Key differences:
- `DataTable` becomes `AsyncDataTable`
- `SqlDataAdapter` becomes `AdapterDbDataAdapter` (provider-agnostic)
- `adapter.Fill()` becomes `await adapter.FillAsync()`
- `DataRow` becomes `AsyncDataRow` (indexer read access is identical)

## Step 6: Change Row Writes to SetValueAsync

**Before:**

```csharp
DataRow row = table.NewRow();
row["Name"] = "Alice";
row["Age"] = 30;
table.Rows.Add(row);

// Modify
table.Rows[0]["Name"] = "Bob";

// Delete
table.Rows[0].Delete();

table.AcceptChanges();
```

**After:**

```csharp
AsyncDataRow row = table.NewRow();
await row.SetValueAsync("Name", "Alice");
await row.SetValueAsync("Age", 30);
await table.Rows.AddAsync(row);

// Modify
await table.Rows[0].SetValueAsync("Name", "Bob");

// Delete
await table.Rows[0].DeleteAsync();

await table.AcceptChangesAsync();
```

Key differences:
- `row["col"] = value` becomes `await row.SetValueAsync("col", value)`
- `table.Rows.Add(row)` becomes `await table.Rows.AddAsync(row)`
- `row.Delete()` becomes `await row.DeleteAsync()`
- `table.AcceptChanges()` becomes `await table.AcceptChangesAsync()`
- `table.Clear()` becomes `await table.ClearAsync()`

:::warning
On `AsyncDataTable`, the `AcceptChanges()` and `Clear()` methods are marked `[Obsolete(error: true)]` and will produce a compile error. This is intentional -- it ensures you use the async versions that properly fire async events.
:::

Reading values through indexers (`row["Name"]`, `row[0]`, `row[column]`) remains unchanged and synchronous.

## Step 7: Subscribe to Async Events for Validation

**Before (sync events):**

```csharp
table.RowChanging += (sender, e) =>
{
    // Cannot call async methods here
    if ((string)e.Row["Name"] == "")
        throw new InvalidOperationException("Name required");
};
```

**After (async events):**

```csharp
table.RowChangingAsync += async (args, ct) =>
{
    // Full async support
    bool exists = await CheckNameExistsAsync(args.Row["Name"].ToString()!, ct);
    if (exists)
        throw new InvalidOperationException("Name already exists");
};
```

Async events fire during `SetValueAsync`, `AddAsync`, `DeleteAsync`, and other async mutations. Sync events (`RowChanging`, `RowChanged`, etc.) continue to work on the inner `DataTable` for backward compatibility.

For a detailed guide, see [Async Validation Events](./async-validation-events.md).

## Complete Before/After Example

### Before (traditional ADO.NET)

```csharp
using System.Data;
using Microsoft.Data.SqlClient;

public class UserService
{
    private readonly string _connectionString;

    public UserService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DataTable GetUsers()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";

        var table = new DataTable("Users");
        using var reader = cmd.ExecuteReader();
        table.Load(reader);

        return table;
    }

    public void UpdateUser(int id, string name)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = @name WHERE Id = @id";

        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@id", id);

        cmd.ExecuteNonQuery();
        tx.Commit();
    }
}
```

### After (AdoNet.Async)

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using Microsoft.Data.SqlClient;

public class UserService
{
    private readonly IAsyncDbProviderFactory _factory;
    private readonly string _connectionString;

    public UserService(IAsyncDbProviderFactory factory, string connectionString)
    {
        _factory = factory;
        _connectionString = connectionString;
    }

    public async Task<AsyncDataTable> GetUsersAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";

        var table = new AsyncDataTable("Users");
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(table, ct);

        return table;
    }

    public async Task UpdateUserAsync(int id, string name, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = @name WHERE Id = @id";

        var nameParam = cmd.CreateParameter();
        nameParam.ParameterName = "@name";
        nameParam.Value = name;
        cmd.Parameters.Add(nameParam);

        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        idParam.Value = id;
        cmd.Parameters.Add(idParam);

        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

Register in DI:

```csharp
services.AddAsyncData(SqlClientFactory.Instance);
services.AddScoped<UserService>();
```

## Next Steps

- [Wrapping Providers](../adapters-di/wrapping-providers.md) -- details on the adapter classes
- [Dependency Injection](../adapters-di/dependency-injection.md) -- register `IAsyncDbProviderFactory`
- [Fill Typed DataSet](./fill-typed-dataset.md) -- end-to-end typed DataSet example

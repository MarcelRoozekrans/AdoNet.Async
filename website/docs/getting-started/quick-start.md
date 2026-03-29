---
sidebar_position: 2
title: Quick Start
---

# Quick Start

This page walks through a complete working example: wrapping a database connection, executing a query, iterating results with `await foreach`, filling an `AsyncDataTable`, mutating rows, and subscribing to async events.

## 1. Wrap a connection and execute a query

Any `DbConnection` can be wrapped with `.AsAsync()` to get an `IAsyncDbConnection`:

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient; // or any ADO.NET provider

// Wrap any DbConnection
IAsyncDbConnection connection = new SqlConnection(connectionString).AsAsync();

await connection.OpenAsync();

try
{
    // Create and execute a command
    IAsyncDbCommand cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Email FROM Users WHERE Active = 1";

    IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
    await using (reader)
    {
        // Iterate rows with await foreach
        await foreach (IAsyncDataRecord record in reader)
        {
            int id = record.GetInt32(0);
            string name = record.GetString(1);
            string email = record.GetString(2);
            Console.WriteLine($"{id}: {name} <{email}>");
        }
    }
}
finally
{
    await connection.CloseAsync();
}
```

## 2. Use transactions

Transactions are fully async and support `await using` for automatic disposal:

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

IAsyncDbConnection connection = new SqlConnection(connectionString).AsAsync();
await connection.OpenAsync();

await using IAsyncDbTransaction transaction = await connection.BeginTransactionAsync();

IAsyncDbCommand cmd = connection.CreateCommand();
cmd.Transaction = transaction;
cmd.CommandText = "INSERT INTO Users (Name, Email) VALUES (@name, @email)";

var nameParam = cmd.CreateParameter();
nameParam.ParameterName = "@name";
nameParam.Value = "Alice";
cmd.Parameters.Add(nameParam);

var emailParam = cmd.CreateParameter();
emailParam.ParameterName = "@email";
emailParam.Value = "alice@example.com";
cmd.Parameters.Add(emailParam);

int rowsAffected = await cmd.ExecuteNonQueryAsync();
Console.WriteLine($"Inserted {rowsAffected} row(s).");

await transaction.CommitAsync();
await connection.CloseAsync();
```

## 3. Fill an AsyncDataTable

Use `AdapterDbDataAdapter` to populate an `AsyncDataTable` from a query:

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using Microsoft.Data.SqlClient;

IAsyncDbConnection connection = new SqlConnection(connectionString).AsAsync();
IAsyncDbCommand cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name, Age FROM Users";

var adapter = new AdapterDbDataAdapter(cmd);
var table = new AsyncDataTable("Users");

int rowCount = await adapter.FillAsync(table);
Console.WriteLine($"Loaded {rowCount} rows.");

// Iterate the filled table
foreach (AsyncDataRow row in table.Rows)
{
    Console.WriteLine($"{row["Id"]}: {row["Name"]} (age {row["Age"]})");
}
```

:::info
`FillAsync` automatically opens and closes the connection if it is not already open. You do not need to call `OpenAsync` yourself when using the adapter.
:::

## 4. Mutate rows asynchronously

All writes to an `AsyncDataRow` go through async methods. The indexers are read-only by design -- this guarantees that async events fire for every mutation.

```csharp
using System.Data.Async.DataSet;

// Create a table with columns
var table = new AsyncDataTable("Products");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));

// Create a new row and set values
AsyncDataRow row = table.NewRow();
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Name", "Widget");
await row.SetValueAsync("Price", 9.99m);

// Add the row to the table
await table.Rows.AddAsync(row);

// Or add a row from an array of values
await table.Rows.AddAsync([2, "Gadget", 19.99m]);

// Modify an existing row
await table.Rows[0].SetValueAsync("Price", 12.99m);

// Delete a row (marks it as Deleted)
await table.Rows[1].DeleteAsync();

// Accept all changes (resets row states to Unchanged)
await table.AcceptChangesAsync();

// Clear all rows
await table.ClearAsync();
```

## 5. Subscribe to async events

`AsyncDataTable` fires async events for every mutation. Handlers receive a `CancellationToken` and can perform async work:

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Orders");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Total", typeof(decimal));
table.Columns.Add("Status", typeof(string));

// Validate before a row changes
table.RowChangingAsync += async (args, ct) =>
{
    // Perform async validation
    await Task.Delay(1, ct); // simulate async I/O
    Console.WriteLine($"Row changing: action={args.Action}");
};

// Audit after a column value changes
table.ColumnChangedAsync += async (args, ct) =>
{
    Console.WriteLine($"Column '{args.Column!.ColumnName}' set to '{args.ProposedValue}'");
    await Task.CompletedTask;
};

// Log row deletions
table.RowDeletedAsync += async (args, ct) =>
{
    Console.WriteLine("Row deleted.");
    await Task.CompletedTask;
};

// All mutations now fire async events automatically
var row = table.NewRow();
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Total", 99.99m);
await row.SetValueAsync("Status", "Pending");
await table.Rows.AddAsync(row);       // fires TableNewRowAsync, RowChangedAsync
await row.SetValueAsync("Total", 50m); // fires ColumnChangingAsync, ColumnChangedAsync
await row.DeleteAsync();                // fires RowDeletingAsync, RowDeletedAsync
await table.ClearAsync();              // fires TableClearingAsync, TableClearedAsync
```

## 6. Dependency Injection

Register `IAsyncDbProviderFactory` from any existing `DbProviderFactory`:

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

// In Program.cs or Startup.cs
builder.Services.AddAsyncData(SqlClientFactory.Instance);
```

Then inject it into your services:

```csharp
using System.Data.Async;

public class UserRepository(IAsyncDbProviderFactory factory)
{
    public async Task<string?> GetNameAsync(int id, CancellationToken ct = default)
    {
        await using var connection = factory.CreateConnection();
        connection.ConnectionString = "Server=...;Database=...";
        await connection.OpenAsync(ct);

        IAsyncDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Users WHERE Id = @id";

        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = id;
        cmd.Parameters.Add(param);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }
}
```

## Next steps

- [Async Interfaces](../core-concepts/async-interfaces.md) -- Full reference for every interface
- [Await ForEach](../core-concepts/await-foreach.md) -- Deep dive into `IAsyncEnumerable` row iteration
- [Async DataTable](../core-concepts/async-datatable.md) -- Complete guide to `AsyncDataTable`, `AsyncDataRow`, and `AsyncDataRowCollection`
- [Async Events](../core-concepts/async-events.md) -- All 9 events, firing order, and practical examples

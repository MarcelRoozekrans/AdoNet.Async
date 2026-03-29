---
sidebar_position: 3
title: Async Validation Events
---

# Async Validation Events

`AsyncDataTable` exposes async events that fire during row mutations. This enables validation logic that calls external async services -- something impossible with the traditional synchronous `DataTable` events.

## Subscribe to RowChangingAsync

The `RowChangingAsync` event fires **before** a row change is applied. If the handler throws an exception, the change is rejected.

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Products");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));

table.RowChangingAsync += async (args, ct) =>
{
    var name = args.Row["Name"]?.ToString();
    if (string.IsNullOrWhiteSpace(name))
    {
        throw new InvalidOperationException("Product name is required.");
    }
};
```

## Call an External Async Service

A common scenario is checking uniqueness or validating against a remote API:

```csharp
table.RowChangingAsync += async (args, ct) =>
{
    if (args.Action == DataRowAction.Add || args.Action == DataRowAction.Change)
    {
        var name = args.Row["Name"]?.ToString();
        if (name is not null)
        {
            // Call an async service to check uniqueness
            bool exists = await productService.NameExistsAsync(name, ct);
            if (exists)
            {
                throw new InvalidOperationException(
                    $"Product name '{name}' already exists.");
            }
        }
    }
};
```

The `ct` parameter is the `CancellationToken` passed to the originating mutation (e.g., `SetValueAsync`, `AddAsync`).

## Reject Changes on Validation Failure

When a handler throws, the exception propagates to the caller of the async mutation method:

```csharp
var row = table.NewRow();
await row.SetValueAsync("Name", "Widget");
await row.SetValueAsync("Price", 9.99m);

try
{
    await table.Rows.AddAsync(row);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
    // The row was NOT added to the table
}
```

## CancellationToken Usage

All async mutation methods accept a `CancellationToken` that flows through to event handlers:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

table.RowChangingAsync += async (args, ct) =>
{
    // ct is the same token passed to AddAsync below
    await httpClient.GetAsync($"/api/validate?name={args.Row["Name"]}", ct);
};

try
{
    await table.Rows.AddAsync(row, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Validation timed out");
}
```

After async event handlers complete, the library checks `cancellationToken.ThrowIfCancellationRequested()` before applying the mutation. This ensures that even if a handler does not check the token, a cancellation request will prevent the change.

## Multiple Validators (Sequential Execution)

Async events use `InvokeMode.Sequential` from `ZeroAlloc.AsyncEvents`. Multiple subscribers execute one after another in registration order:

```csharp
// Validator 1: Required field check
table.RowChangingAsync += async (args, ct) =>
{
    if (args.Row.IsNull("Name"))
        throw new InvalidOperationException("Name is required");
};

// Validator 2: Range check
table.RowChangingAsync += async (args, ct) =>
{
    if (args.Row["Price"] is decimal price && price < 0)
        throw new InvalidOperationException("Price must be non-negative");
};

// Validator 3: Async uniqueness check
table.RowChangingAsync += async (args, ct) =>
{
    var name = args.Row["Name"]?.ToString();
    bool exists = await CheckDuplicateAsync(name!, ct);
    if (exists)
        throw new InvalidOperationException($"'{name}' already exists");
};
```

If Validator 1 throws, Validators 2 and 3 do not execute. The exception propagates directly to the caller.

## All Available Async Events

`AsyncDataTable` exposes nine async events:

| Event | Fires when | Args type |
|---|---|---|
| `ColumnChangingAsync` | Before a column value is set | `DataColumnChangeEventArgs` |
| `ColumnChangedAsync` | After a column value is set | `DataColumnChangeEventArgs` |
| `RowChangingAsync` | Before `EndEditAsync` | `DataRowChangeEventArgs` |
| `RowChangedAsync` | After `AddAsync`, `EndEditAsync`, `AcceptChangesAsync` | `DataRowChangeEventArgs` |
| `RowDeletingAsync` | Before `DeleteAsync` or `RemoveAsync` | `DataRowChangeEventArgs` |
| `RowDeletedAsync` | After `DeleteAsync` or `RemoveAsync` | `DataRowChangeEventArgs` |
| `TableClearingAsync` | Before `ClearAsync` | `DataTableClearEventArgs` |
| `TableClearedAsync` | After `ClearAsync` | `DataTableClearEventArgs` |
| `TableNewRowAsync` | After `AddAsync` (new row notification) | `DataTableNewRowEventArgs` |

## Complete Working Example

```csharp
using System.Data.Async.DataSet;

// Set up table
var table = new AsyncDataTable("Employees");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Department", typeof(string));
table.Columns.Add("Salary", typeof(decimal));

// --- Validation: required fields ---
table.ColumnChangingAsync += async (args, ct) =>
{
    if (args.Column!.ColumnName == "Name" &&
        string.IsNullOrWhiteSpace(args.ProposedValue?.ToString()))
    {
        throw new InvalidOperationException("Employee name cannot be empty.");
    }
};

// --- Validation: salary range ---
table.ColumnChangingAsync += async (args, ct) =>
{
    if (args.Column!.ColumnName == "Salary" &&
        args.ProposedValue is decimal salary)
    {
        if (salary < 30_000m || salary > 500_000m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salary), "Salary must be between 30,000 and 500,000.");
        }
    }
};

// --- Audit: log all changes ---
table.RowChangedAsync += async (args, ct) =>
{
    await AuditLogService.LogAsync(
        $"Row {args.Action}: {args.Row["Name"]}", ct);
};

// --- Notification: alert on deletions ---
table.RowDeletingAsync += async (args, ct) =>
{
    await NotificationService.SendAsync(
        $"Deleting employee: {args.Row["Name"]}", ct);
};

// Use the table
var row = table.NewRow();
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Name", "Alice");
await row.SetValueAsync("Department", "Engineering");
await row.SetValueAsync("Salary", 85_000m);
await table.Rows.AddAsync(row);

// This will throw -- salary out of range
try
{
    await row.SetValueAsync("Salary", 1_000_000m);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"Rejected: {ex.Message}");
    // row["Salary"] is still 85,000
}

// This will throw -- empty name
try
{
    await row.SetValueAsync("Name", "");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Rejected: {ex.Message}");
    // row["Name"] is still "Alice"
}
```

:::warning
Async event handlers execute sequentially. If a handler performs expensive I/O (e.g., a database query per row), this can add latency during bulk operations. Consider batching or caching strategies for high-throughput scenarios.
:::

## Sync Events Still Work

Traditional synchronous events on the inner `DataTable` continue to fire for backward compatibility:

```csharp
table.RowChanging += (sender, args) =>
{
    // This still fires (sync, on inner DataTable)
    Console.WriteLine("Sync event: row changing");
};
```

Sync events and async events are independent -- both can be subscribed simultaneously. Async events are fired by the `AsyncDataRow` mutation methods; sync events are fired by the inner `DataTable`.

## Next Steps

- [JSON API with Typed DataSets](./json-api-typed-datasets.md) -- combine validation with API endpoints
- [Migrate Existing Code](./migrate-existing-code.md) -- step-by-step migration guide

---
sidebar_position: 4
title: Async Events
---

# Async Events

`AsyncDataTable` exposes 9 async events that fire when rows, columns, or the table itself are mutated. These events are backed by the [`ZeroAlloc.AsyncEvents`](https://www.nuget.org/packages/ZeroAlloc.AsyncEvents) library, which provides zero-allocation, sequential invocation.

## The 9 async events

| Event | Event Args Type | When it fires |
|---|---|---|
| `ColumnChangingAsync` | `DataColumnChangeEventArgs` | Before a column value is written via `SetValueAsync` |
| `ColumnChangedAsync` | `DataColumnChangeEventArgs` | After a column value is written via `SetValueAsync` |
| `RowChangingAsync` | `DataRowChangeEventArgs` | Before a row edit ends (`EndEditAsync`) |
| `RowChangedAsync` | `DataRowChangeEventArgs` | After a row is added (`AddAsync`), after an edit ends (`EndEditAsync`), after `AcceptChangesAsync` |
| `RowDeletingAsync` | `DataRowChangeEventArgs` | Before a row is deleted (`DeleteAsync`, `RemoveAsync`, `RemoveAtAsync`) |
| `RowDeletedAsync` | `DataRowChangeEventArgs` | After a row is deleted (`DeleteAsync`, `RemoveAsync`, `RemoveAtAsync`) |
| `TableClearingAsync` | `DataTableClearEventArgs` | Before `ClearAsync` removes all rows |
| `TableClearedAsync` | `DataTableClearEventArgs` | After `ClearAsync` removes all rows |
| `TableNewRowAsync` | `DataTableNewRowEventArgs` | When a row is added to the table via `AddAsync` |

## Subscribing to events

Each async event uses the `AsyncEvent<TArgs>` delegate type from ZeroAlloc.AsyncEvents. The handler signature is:

```csharp
ValueTask Handler(TArgs args, CancellationToken cancellationToken)
```

Subscribe using the standard `+=` operator:

```csharp
var table = new AsyncDataTable("Orders");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Total", typeof(decimal));

table.ColumnChangingAsync += async (args, ct) =>
{
    Console.WriteLine($"Changing column '{args.Column!.ColumnName}' to '{args.ProposedValue}'");
    await Task.CompletedTask;
};

table.RowChangedAsync += async (args, ct) =>
{
    Console.WriteLine($"Row changed with action: {args.Action}");
    await Task.CompletedTask;
};
```

Unsubscribe with `-=`:

```csharp
AsyncEvent<DataRowChangeEventArgs> handler = async (args, ct) =>
{
    await AuditRowChangeAsync(args, ct);
};

table.RowChangedAsync += handler;
// ... later ...
table.RowChangedAsync -= handler;
```

## Event firing order by mutation

### SetValueAsync

When you call `row.SetValueAsync("Column", value)`:

1. `ColumnChangingAsync` -- with the column and proposed value
2. Value is written to the inner `DataRow`
3. `ColumnChangedAsync` -- with the column and value that was set

### AddAsync (row)

When you call `table.Rows.AddAsync(row)`:

1. Row is added to the inner `DataRowCollection`
2. `TableNewRowAsync` -- notifying that a new row was added
3. `RowChangedAsync` -- with `DataRowAction.Add`

### AddAsync (values array)

When you call `table.Rows.AddAsync([value1, value2, ...])`:

1. A new `DataRow` is created and populated with the values
2. Row is added to the inner `DataRowCollection`
3. `TableNewRowAsync` -- notifying that a new row was added
4. `RowChangedAsync` -- with `DataRowAction.Add`

### DeleteAsync

When you call `row.DeleteAsync()`:

1. `RowDeletingAsync` -- with `DataRowAction.Delete`
2. `DataRow.Delete()` is called (marks row as `Deleted`)
3. `RowDeletedAsync` -- with `DataRowAction.Delete`

### RemoveAsync / RemoveAtAsync

When you call `table.Rows.RemoveAsync(row)` or `table.Rows.RemoveAtAsync(index)`:

1. `RowDeletingAsync` -- with `DataRowAction.Delete`
2. Row is removed from the inner `DataRowCollection`
3. `RowDeletedAsync` -- with `DataRowAction.Delete`

:::info
`DeleteAsync` marks the row as `Deleted` (it remains in the collection with `RowState = Deleted`). `RemoveAsync` physically removes the row from the collection. Both fire the same deleting/deleted events.
:::

### ClearAsync

When you call `table.ClearAsync()`:

1. `TableClearingAsync` -- before rows are removed
2. `DataTable.Clear()` is called (removes all rows)
3. `TableClearedAsync` -- after rows are removed

### AcceptChangesAsync

When you call `table.AcceptChangesAsync()`:

1. Changed rows (Modified and Added) are snapshot
2. `DataTable.AcceptChanges()` is called (resets all row states to Unchanged)
3. `RowChangedAsync` -- fired for each row that was in the snapshot, with `DataRowAction.Commit`

### EndEditAsync

When you call `row.EndEditAsync()`:

1. `RowChangingAsync` -- with `DataRowAction.Change`
2. `DataRow.EndEdit()` is called
3. `RowChangedAsync` -- with `DataRowAction.Commit`

## CancellationToken propagation

Every async mutation method accepts an optional `CancellationToken`. This token is passed through to each event handler:

```csharp
table.ColumnChangingAsync += async (args, ct) =>
{
    // The ct here is the same token passed to SetValueAsync
    await ValidateValueAsync(args.ProposedValue, ct);
};

// The token flows through to the event handler
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await row.SetValueAsync("Total", 99.99m, cts.Token);
```

After each event handler completes, the mutation method checks `cancellationToken.ThrowIfCancellationRequested()`. This means cancellation is checked between the "changing" and "changed" events -- if an event handler cancels the token, the mutation still occurs (the value is written), but the "changed" event will not fire and the method will throw `OperationCanceledException`.

## ZeroAlloc.AsyncEvents

The async events are backed by `AsyncEventHandler<TArgs>` from the `ZeroAlloc.AsyncEvents` library. Key characteristics:

- **Zero allocation** -- When no handlers are subscribed, invoking the event allocates nothing. Even with subscribers, the invocation avoids closures and delegate allocations on the hot path.
- **Sequential invocation mode** -- All events use `InvokeMode.Sequential`, which means multiple handlers are invoked one after another (not concurrently). This matches the behavior of standard .NET events and ensures predictable ordering.
- **Faster than sync multicast delegates with no subscribers** -- When there are no subscribers, `InvokeAsync` returns `ValueTask.CompletedTask` immediately with no allocation.

## Sync events still work

`AsyncDataTable` also exposes the standard synchronous events from the inner `DataTable`:

```csharp
// Sync events are forwarded to the inner DataTable
table.RowChanged += (sender, args) =>
{
    Console.WriteLine($"[Sync] Row changed: {args.Action}");
};
```

Available sync events: `ColumnChanging`, `ColumnChanged`, `RowChanging`, `RowChanged`, `RowDeleting`, `RowDeleted`, `TableClearing`, `TableCleared`, `TableNewRow`.

:::info
Sync events fire from the inner `DataTable` whenever its data changes. Async events fire from the `AsyncDataTable` wrapper methods. When you use `SetValueAsync`, both the sync events (from the inner `DataTable` reacting to the write) and the async events (from the wrapper) will fire.
:::

## Practical example: async validation

Here is a complete example that validates row changes asynchronously before they are committed:

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Products");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));
table.Columns.Add("Category", typeof(string));

// Validate column values before they are written
table.ColumnChangingAsync += async (args, ct) =>
{
    if (args.Column!.ColumnName == "Price" && args.ProposedValue is decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentException("Price cannot be negative.");
        }

        // Simulate calling an external pricing service
        bool approved = await CheckPriceWithServiceAsync(price, ct);
        if (!approved)
        {
            throw new InvalidOperationException("Price not approved by pricing service.");
        }
    }
};

// Audit all changes after they happen
table.ColumnChangedAsync += async (args, ct) =>
{
    await WriteAuditLogAsync(
        tableName: "Products",
        columnName: args.Column!.ColumnName,
        newValue: args.ProposedValue,
        cancellationToken: ct);
};

// Log row additions
table.TableNewRowAsync += async (args, ct) =>
{
    await WriteAuditLogAsync(
        tableName: "Products",
        action: "RowAdded",
        cancellationToken: ct);
};

// This will trigger validation and audit logging
var row = table.NewRow();
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Name", "Widget");
await row.SetValueAsync("Price", 9.99m);       // validated + audited
await row.SetValueAsync("Category", "Parts");
await table.Rows.AddAsync(row);                  // audit log: RowAdded

// This will throw because the ColumnChangingAsync handler rejects negative prices
try
{
    await row.SetValueAsync("Price", -5m);
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message); // "Price cannot be negative."
}
```

---
sidebar_position: 3
title: Async DataTable
---

# Async DataTable

The `AdoNet.Async.DataSet` package provides async wrappers for `DataTable`, `DataRow`, and `DataRowCollection`. These wrappers ensure that all mutations go through async methods, which in turn fire [async events](./async-events.md).

## AsyncDataTable

`AsyncDataTable` wraps a `System.Data.DataTable`. You can create one from scratch or wrap an existing `DataTable`.

```csharp
using System.Data.Async.DataSet;

// Create from scratch
var table = new AsyncDataTable("Users");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Email", typeof(string));

// Or wrap an existing DataTable
DataTable existing = GetDataTableFromSomewhere();
var wrapped = new AsyncDataTable(existing);
```

### Properties forwarded to the inner DataTable

Most properties are forwarded directly to the inner `DataTable`:

| Property | Type | Description |
|---|---|---|
| `TableName` | `string` | Get/set the table name |
| `Namespace` | `string` | Get/set the XML namespace |
| `Prefix` | `string` | Get/set the XML prefix |
| `CaseSensitive` | `bool` | Get/set case sensitivity for string comparisons |
| `Locale` | `CultureInfo` | Get/set the locale for string comparisons |
| `DisplayExpression` | `string` | Get/set the expression used for display |
| `HasErrors` | `bool` | Whether any row has errors |
| `MinimumCapacity` | `int` | Get/set the initial capacity |
| `IsInitialized` | `bool` | Whether the table has been initialized |
| `PrimaryKey` | `DataColumn[]` | Get/set the primary key columns |

### Collections

| Property | Type | Notes |
|---|---|---|
| `Columns` | `DataColumnCollection` | The standard `DataColumnCollection` -- column definitions are synchronous |
| `Rows` | `AsyncDataRowCollection` | Async wrapper with `AddAsync`, `RemoveAsync`, `RemoveAtAsync` |
| `Constraints` | `ConstraintCollection` | Standard constraint collection |
| `ParentRelations` | `DataRelationCollection` | Standard parent relation collection |
| `ChildRelations` | `DataRelationCollection` | Standard child relation collection |
| `DefaultView` | `DataView` | The default view |
| `ExtendedProperties` | `PropertyCollection` | Custom properties |

### Creating rows

```csharp
// Create a detached row (not yet in the table)
AsyncDataRow row = table.NewRow();

// Set values asynchronously
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Name", "Alice");
await row.SetValueAsync("Email", "alice@example.com");

// Add to the table
await table.Rows.AddAsync(row);
```

### Table-level async operations

```csharp
// Clear all rows (fires TableClearingAsync, then TableClearedAsync)
await table.ClearAsync();

// Accept changes on all rows (fires RowChangedAsync for each modified/added row)
await table.AcceptChangesAsync();
```

:::warning
`AcceptChanges()` and `Clear()` are marked `[Obsolete]` with `error: true` on `AsyncDataTable`. This is deliberate -- calling them would bypass async events. Use `AcceptChangesAsync()` and `ClearAsync()` instead.
:::

### Other methods

`AsyncDataTable` also exposes forwarded methods for common operations:

- `Clone()` / `Copy()` -- Returns a new `AsyncDataTable` (schema only / schema + data)
- `Merge(AsyncDataTable)` -- Merges rows from another table
- `Select()` / `Select(filter)` / `Select(filter, sort)` -- Returns `AsyncDataRow[]` matching the filter
- `GetChanges()` / `GetChanges(DataRowState)` -- Returns a new `AsyncDataTable` with only changed rows
- `GetErrors()` -- Returns `AsyncDataRow[]` for rows with errors
- `ImportRow(AsyncDataRow)` -- Imports a row from another table
- `LoadDataRow(values, fAcceptChanges)` -- Loads values into a matching row or creates a new one
- `Compute(expression, filter)` -- Computes an aggregate expression
- `LoadAsync(IAsyncDataReader)` -- Asynchronously loads rows from a reader

## AsyncDataRow

`AsyncDataRow` wraps a `System.Data.DataRow`. Its indexers are **read-only** -- all writes go through `SetValueAsync`, which fires async events.

### Read-only indexers

```csharp
// Access by column name
object value = row["Name"];

// Access by column index
object value = row[0];

// Access by DataColumn
object value = row[table.Columns["Name"]!];

// Access a specific version
object original = row["Name", DataRowVersion.Original];
object current = row["Name", DataRowVersion.Current];
```

### Async mutations

```csharp
// Set by column name
await row.SetValueAsync("Name", "Bob");

// Set by column index
await row.SetValueAsync(0, 42);

// Set by DataColumn
await row.SetValueAsync(table.Columns["Name"]!, "Charlie");

// Delete the row (marks as Deleted)
await row.DeleteAsync();

// Edit lifecycle
await row.BeginEditAsync();
// ... make changes via SetValueAsync ...
await row.EndEditAsync();     // fires RowChangingAsync + RowChangedAsync
await row.CancelEditAsync();  // discards pending edits

// Accept or reject changes on this row
await row.AcceptChangesAsync();
await row.RejectChangesAsync();
```

### Row state and properties

| Property | Type | Description |
|---|---|---|
| `RowState` | `DataRowState` | `Detached`, `Unchanged`, `Added`, `Modified`, `Deleted` |
| `HasErrors` | `bool` | Whether the row has errors |
| `RowError` | `string` | The error description |
| `Table` | `AsyncDataTable` | The parent table |
| `HasVersion(DataRowVersion)` | `bool` | Whether the row has a specific version |

### Event flow for SetValueAsync

Each call to `SetValueAsync` fires events in this order:

1. `ColumnChangingAsync` -- before the value is set
2. The value is written to the inner `DataRow`
3. `ColumnChangedAsync` -- after the value is set

See [Async Events](./async-events.md) for the complete event firing order for all mutations.

## AsyncDataRowCollection

`AsyncDataRowCollection` wraps `DataRowCollection` and provides async add/remove operations.

### Adding rows

```csharp
// Add a prepared row
AsyncDataRow row = table.NewRow();
await row.SetValueAsync("Name", "Alice");
await table.Rows.AddAsync(row);

// Add from an array of values
await table.Rows.AddAsync([1, "Alice", "alice@example.com"]);
```

### Removing rows

```csharp
// Remove a specific row
await table.Rows.RemoveAsync(row);

// Remove by index
await table.Rows.RemoveAtAsync(0);
```

### Enumeration

`AsyncDataRowCollection` implements `IEnumerable<AsyncDataRow>`, so you can iterate with a standard `foreach`:

```csharp
foreach (AsyncDataRow row in table.Rows)
{
    Console.WriteLine(row["Name"]);
}
```

### Indexing

```csharp
int count = table.Rows.Count;
AsyncDataRow first = table.Rows[0];
bool hasKey = table.Rows.Contains(42);
```

## Accessing the inner DataTable

Sometimes you need access to the underlying `DataTable` -- for example, to pass it to a library that expects `System.Data` types. Use an explicit cast:

```csharp
AsyncDataTable asyncTable = new AsyncDataTable("Users");
// ... populate ...

// Get the inner DataTable via explicit cast
DataTable inner = (DataTable)asyncTable;
```

:::warning
Mutations made directly on the inner `DataTable` bypass async events. Only use the explicit cast when you need interop with code that requires a `DataTable`, and avoid mutating through the inner reference.
:::

## Identity semantics

`AsyncDataTable` maintains a `ConditionalWeakTable<DataRow, AsyncDataRow>` cache. This means that the same underlying `DataRow` always returns the same `AsyncDataRow` wrapper instance:

```csharp
AsyncDataRow row1 = table.Rows[0];
AsyncDataRow row2 = table.Rows[0];
bool same = ReferenceEquals(row1, row2); // true
```

This is important for event handlers that compare row references, and for any code that uses row identity rather than row equality.

The cache uses weak references, so `AsyncDataRow` wrappers are eligible for garbage collection when no longer referenced -- there is no memory leak from the caching mechanism.

## Complete example

```csharp
using System.Data.Async.DataSet;

// Create and configure a table
var table = new AsyncDataTable("Employees");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Department", typeof(string));
table.Columns.Add("Salary", typeof(decimal));
table.PrimaryKey = [table.Columns["Id"]!];

// Subscribe to an async event
table.RowChangedAsync += async (args, ct) =>
{
    Console.WriteLine($"Row changed: {args.Action}");
    await Task.CompletedTask;
};

// Add rows
var row = table.NewRow();
await row.SetValueAsync("Id", 1);
await row.SetValueAsync("Name", "Alice");
await row.SetValueAsync("Department", "Engineering");
await row.SetValueAsync("Salary", 95000m);
await table.Rows.AddAsync(row);

await table.Rows.AddAsync([2, "Bob", "Marketing", 85000m]);
await table.Rows.AddAsync([3, "Charlie", "Engineering", 105000m]);

// Query
AsyncDataRow[] engineers = table.Select("Department = 'Engineering'");
Console.WriteLine($"Engineers: {engineers.Length}"); // 2

// Modify
await table.Rows[1].SetValueAsync("Salary", 90000m);

// Check changes
AsyncDataTable? changes = table.GetChanges(DataRowState.Modified);
Console.WriteLine($"Modified rows: {changes?.Rows.Count}"); // 1

// Accept all changes
await table.AcceptChangesAsync();

// Delete and clear
await table.Rows[0].DeleteAsync();
await table.ClearAsync();
```

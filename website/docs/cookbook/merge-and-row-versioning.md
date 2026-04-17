---
sidebar_position: 6
title: Merge & Row Versioning
---

# Merge & Row Versioning

## Merging Tables

`AsyncDataTable.Merge()` appends or reconciles rows from a source table into the target. It is the standard ADO.NET `DataTable.Merge` wrapped — no async overhead is needed because the operation works on in-memory data.

### Append rows from another table

```csharp
using System.Data.Async.DataSet;

var target = new AsyncDataTable("Products");
target.Columns.Add("Id", typeof(int));
target.Columns.Add("Name", typeof(string));
target.PrimaryKey = [target.Columns["Id"]!];

await target.Rows.AddAsync([1, "Widget"]);
await target.AcceptChangesAsync();

var source = new AsyncDataTable("Products");
source.Columns.Add("Id", typeof(int));
source.Columns.Add("Name", typeof(string));
await source.Rows.AddAsync([2, "Gadget"]);
await source.AcceptChangesAsync();

target.Merge(source);
// target now has rows Id=1 and Id=2
```

### Preserve pending changes during merge

By default (`preserveChanges: false`) the source values overwrite any pending changes in the target. Pass `true` to keep the target's in-flight edits:

```csharp
// Row Id=1 has a pending change in target
await target.Rows[0].SetValueAsync("Name", "Widget (edited)");

target.Merge(source, preserveChanges: true);
// Row Id=1 still shows "Widget (edited)" — not overwritten by source
```

### Handle schema differences with MissingSchemaAction

```csharp
// source has an extra column "Price" that target does not
target.Merge(source, preserveChanges: false, MissingSchemaAction.Add);    // adds the column
target.Merge(source, preserveChanges: false, MissingSchemaAction.Ignore); // silently skips it
target.Merge(source, preserveChanges: false, MissingSchemaAction.Error);  // throws InvalidOperationException
```

---

## Row Versioning

Every `AsyncDataRow` tracks three versions of each cell value: `Original` (state at last `AcceptChanges`), `Current` (the live value), and `Proposed` (value mid-edit, between `BeginEdit` and `EndEdit`).

Access them via the column-name indexer with a `DataRowVersion`:

```csharp
using System.Data;

var row = table.Rows[0];

// After AcceptChanges, Original == Current
var original = row["Name", DataRowVersion.Original]; // "Widget"
var current  = row["Name", DataRowVersion.Current];  // "Widget"

// Make a change without accepting
await row.SetValueAsync("Name", "Widget Pro");

var afterEdit = row["Name", DataRowVersion.Current];   // "Widget Pro"
var before    = row["Name", DataRowVersion.Original];  // "Widget"

// Proposed version is visible between BeginEdit and EndEdit
row.InnerDataRow.BeginEdit();
row.InnerDataRow["Name"] = "Widget Ultra";
var proposed = row["Name", DataRowVersion.Proposed];   // "Widget Ultra"
row.InnerDataRow.EndEdit();
// After EndEdit, Proposed is promoted to Current
```

### Common pattern: get all pending changes

```csharp
await table.AcceptChangesAsync(); // clear baseline

// mutate some rows ...

var changed = table.GetChanges();
if (changed != null)
{
    foreach (var row in changed.Rows.Cast<DataRow>())
    {
        var before = row["Name", DataRowVersion.Original];
        var after  = row["Name", DataRowVersion.Current];
        Console.WriteLine($"  {before} → {after}");
    }
}
```

:::tip
`GetChanges()` returns `null` (not an empty table) when there are no pending changes.
:::

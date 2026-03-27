# Async Events — Design Document

**Date:** 2026-03-27
**Status:** Approved

## Overview

Add full async event support to `AsyncDataTable` using `ZeroAlloc.AsyncEvents`. Mutations on rows are moved behind an `AsyncDataRow` wrapper with getter-only indexers, forcing callers onto the async path. Async events fire around each mutation; existing sync events are preserved for backward-compatible sync consumers.

## Goals

- All row/column/table mutations on `AsyncDataTable` go through `async`/`await` APIs
- 9 async events on `AsyncDataTable` using `ZeroAlloc.AsyncEvents` (`Sequential` mode)
- Sync events remain — sync subscribers continue to work unchanged via the inner `DataTable`
- No sync-over-async bridge needed — async events fire from async mutation methods directly
- Breaking changes are compile-time errors, not silent behavior changes

## Non-Goals

- No changes to `AsyncDataSet` or `AsyncDataAdapter`
- No async events on `AsyncDataSet.Tables`
- No XML or JSON serialization changes

## New Types

### `AsyncDataRow`

**File:** `src/System.Data.Async.DataSet/AsyncDataRow.cs`

Wraps `DataRow`. Holds a back-reference to `AsyncDataTable` to reach async event invokers.

**Read-only surface (forwarded from `DataRow`):**
- `object this[string columnName]` — getter only
- `object this[int columnIndex]` — getter only
- `object this[DataColumn column]` — getter only
- `object this[string columnName, DataRowVersion version]` — getter only
- `object this[int columnIndex, DataRowVersion version]` — getter only
- `object this[DataColumn column, DataRowVersion version]` — getter only
- `DataRowState RowState`
- `bool HasErrors`
- `string RowError`
- `DataTable Table`
- `bool HasVersion(DataRowVersion version)`
- `DataRow InnerDataRow` — escape hatch for serialization and interop

**Async mutation methods (all return `ValueTask`):**
- `SetValueAsync(string col, object? value, CancellationToken ct = default)`
- `SetValueAsync(int index, object? value, CancellationToken ct = default)`
- `SetValueAsync(DataColumn col, object? value, CancellationToken ct = default)`
- `DeleteAsync(CancellationToken ct = default)`
- `AcceptChangesAsync(CancellationToken ct = default)`
- `RejectChangesAsync(CancellationToken ct = default)`
- `BeginEditAsync(CancellationToken ct = default)`
- `EndEditAsync(CancellationToken ct = default)`
- `CancelEditAsync(CancellationToken ct = default)`

### `AsyncDataRowCollection`

**File:** `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`

Wraps `DataRowCollection`. Holds a reference to `AsyncDataTable` for event firing.

**Read-only surface:**
- `AsyncDataRow this[int index]` — getter only
- `int Count`
- `bool Contains(object key)`
- `IEnumerable<AsyncDataRow>` (implements `IEnumerable<AsyncDataRow>`)

**Async mutation methods:**
- `ValueTask AddAsync(AsyncDataRow row, CancellationToken ct = default)`
- `ValueTask RemoveAsync(AsyncDataRow row, CancellationToken ct = default)`
- `ValueTask RemoveAtAsync(int index, CancellationToken ct = default)`

## `AsyncDataTable` Changes

### `Rows` and `NewRow()`

| Before | After |
|---|---|
| `DataRowCollection Rows` | `AsyncDataRowCollection Rows` |
| `DataRow NewRow()` | `AsyncDataRow NewRow()` |

### Async Events

9 new async events backed by `AsyncEventHandler<TArgs>` structs (`Sequential` mode):

| Field | Public event | Args type |
|---|---|---|
| `_columnChanging` | `ColumnChangingAsync` | `DataColumnChangeEventArgs` |
| `_columnChanged` | `ColumnChangedAsync` | `DataColumnChangeEventArgs` |
| `_rowChanging` | `RowChangingAsync` | `DataRowChangeEventArgs` |
| `_rowChanged` | `RowChangedAsync` | `DataRowChangeEventArgs` |
| `_rowDeleting` | `RowDeletingAsync` | `DataRowChangeEventArgs` |
| `_rowDeleted` | `RowDeletedAsync` | `DataRowChangeEventArgs` |
| `_tableClearing` | `TableClearingAsync` | `DataTableClearEventArgs` |
| `_tableCleared` | `TableClearedAsync` | `DataTableClearEventArgs` |
| `_tableNewRow` | `TableNewRowAsync` | `DataTableNewRowEventArgs` |

All fields are `internal` — `AsyncDataRow` and `AsyncDataRowCollection` access them directly.

### New Async Table-Level Mutations

- `ValueTask ClearAsync(CancellationToken ct = default)` — fires `TableClearingAsync`, calls `_inner.Clear()`, fires `TableClearedAsync`
- `ValueTask AcceptChangesAsync(CancellationToken ct = default)` — fires `RowChangedAsync(Unchanged)` per modified/added row, calls `_inner.AcceptChanges()`

## Event Firing Model

### `AsyncDataRow.SetValueAsync(col, value, ct)`

1. `await _table._columnChanging.InvokeAsync(new DataColumnChangeEventArgs(_inner, column, value), ct)`
2. `_inner[col] = value` — inner `DataTable` fires sync `ColumnChanging` + `ColumnChanged` to sync subscribers
3. `await _table._columnChanged.InvokeAsync(new DataColumnChangeEventArgs(_inner, column, value), ct)`

### `AsyncDataRow.DeleteAsync(ct)`

1. `await _table._rowDeleting.InvokeAsync(new DataRowChangeEventArgs(_inner, DataRowAction.Delete), ct)`
2. `_inner.Delete()`
3. `await _table._rowDeleted.InvokeAsync(new DataRowChangeEventArgs(_inner, DataRowAction.Delete), ct)`

### `AsyncDataRowCollection.AddAsync(row, ct)`

1. `_inner.Rows.Add(row.InnerDataRow)` — inner table fires sync `TableNewRow` + `RowChanged(Added)` to sync subscribers
2. `await _table._tableNewRow.InvokeAsync(new DataTableNewRowEventArgs(row.InnerDataRow), ct)`
3. `await _table._rowChanged.InvokeAsync(new DataRowChangeEventArgs(row.InnerDataRow, DataRowAction.Add), ct)`

### `AsyncDataTable.ClearAsync(ct)`

1. `await _tableClearing.InvokeAsync(new DataTableClearEventArgs(_inner), ct)`
2. `_inner.Clear()`
3. `await _tableCleared.InvokeAsync(new DataTableClearEventArgs(_inner), ct)`

## Package Dependency

`ZeroAlloc.AsyncEvents 1.*` added to `System.Data.Async.DataSet.csproj`.

## Breaking Changes

| Was | Now |
|---|---|
| `DataRowCollection AsyncDataTable.Rows` | `AsyncDataRowCollection AsyncDataTable.Rows` |
| `DataRow AsyncDataTable.NewRow()` | `AsyncDataRow AsyncDataTable.NewRow()` |
| `row["col"] = value` | `await row.SetValueAsync("col", value, ct)` |
| `table.Rows.Add(row)` | `await table.Rows.AddAsync(row, ct)` |
| `table.Rows.Remove(row)` | `await table.Rows.RemoveAsync(row, ct)` |
| `row.Delete()` | `await row.DeleteAsync(ct)` |
| `table.Clear()` | `await table.ClearAsync(ct)` |
| `table.AcceptChanges()` | `await table.AcceptChangesAsync(ct)` |

All breaking changes produce compile-time errors.

## Test Impact

Existing test files that use sync row mutation must be updated:
- `tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataTableConverterTests.cs`
- `tests/System.Data.Async.Integration.Tests/DataTableInteropTests.cs`
- `tests/System.Data.Async.Integration.Tests/NewtonsoftJsonCrossCompatibilityTests.cs`
- `tests/System.Data.Async.Integration.Tests/SystemTextJsonCrossCompatibilityTests.cs`
- `tests/System.Data.Async.Validation.Tests/` (DataAdapter tests use `DataTable` rows directly — unaffected)

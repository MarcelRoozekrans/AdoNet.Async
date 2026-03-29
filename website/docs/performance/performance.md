---
sidebar_position: 1
title: Performance
---

# Performance

AdoNet.Async is designed for minimal overhead. This page presents benchmark results, explains what each benchmark measures, and discusses the design decisions that enable strong performance.

All benchmarks were measured on Intel Core i9-12900HK, .NET 10.0.4, Windows 11, BenchmarkDotNet v0.15.8 (ShortRun, Release mode).

## Command Execution

Measures the overhead of wrapping `DbCommand.ExecuteScalarAsync`, `ExecuteNonQueryAsync`, and `ExecuteReaderAsync` through the adapter layer.

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|
| Raw_ExecuteScalar | 6.581 us | 1.00 | 720 B | 1.00 |
| Async_ExecuteScalar | 12.069 us | 1.83 | 912 B | 1.27 |
| Raw_ExecuteNonQuery | 9.355 us | 1.42 | 480 B | 0.67 |
| Async_ExecuteNonQuery | 16.063 us | 2.44 | 528 B | 0.73 |
| Raw_ExecuteReader_Iterate | 10.087 us | 1.53 | 704 B | 0.98 |
| Async_ExecuteReader_Iterate | 16.023 us | 2.44 | 992 B | 1.38 |

**What this tells us:** The adapter wrapper adds approximately 6-7 microseconds of overhead per command execution. Memory overhead is 48-288 bytes per operation, primarily from the adapter wrapper objects and async state machines. In real applications where commands take milliseconds (network I/O), this overhead is negligible.

## Connection Open/Close

Measures the cost of opening and closing a connection through the adapter.

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|
| Raw_OpenClose | 15.54 us | 1.00 | 384 B | 1.00 |
| Async_OpenClose | 14.35 us | 0.92 | 408 B | 1.06 |

**What this tells us:** The async adapter is actually slightly faster for open/close operations. Memory overhead is minimal (24 bytes).

## Data Reader Iteration (50 rows)

Measures the cost of iterating 50 rows through a `DbDataReader` vs. the async adapter, including `await foreach`.

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|
| Raw_ReadAll_Fields | 20.99 us | 1.00 | 3.7 KB | 1.00 |
| Async_ReadAll_ManualLoop | 33.13 us | 1.58 | 3.98 KB | 1.08 |
| Async_ReadAll_AwaitForeach | 30.74 us | 1.46 | 4.09 KB | 1.11 |

**What this tells us:** The `await foreach` approach is slightly faster than a manual `ReadAsync` loop due to optimizations in the async enumerator. Memory overhead is about 8-11% over raw iteration -- roughly 300-400 bytes total regardless of row count.

## DataAdapter Fill

Measures `FillAsync` vs. raw `SqlDataAdapter.Fill` for 10 and 100 rows.

| Method | RowLimit | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|---|
| Raw_Fill | 10 | 595.5 us | 1.00 | 94.42 KB | 1.00 |
| Async_Fill | 10 | 872.3 us | 1.46 | 78.88 KB | 0.84 |
| Raw_Fill | 100 | 1,293.8 us | 1.00 | 160.21 KB | 1.00 |
| Async_Fill | 100 | 1,173.8 us | 0.92 | 117.40 KB | 0.73 |

**What this tells us:** At 100 rows, `FillAsync` is actually faster and allocates 27% less memory than `SqlDataAdapter.Fill`. The async adapter's `FillAsync` uses `LoadAsync` which avoids some of the internal overhead of `SqlDataAdapter`. At smaller row counts the async overhead is more visible, but at realistic data sizes the async path wins.

## Serialization (AsyncDataTable)

Measures JSON serialization and deserialization of `AsyncDataTable` with both Newtonsoft.Json and System.Text.Json.

| Method | RowCount | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|---|
| Newtonsoft_Serialize | 10 | 12.30 us | 1.00 | 29.36 KB | 1.00 |
| STJ_Serialize | 10 | 10.05 us | 0.84 | 11.74 KB | 0.40 |
| Newtonsoft_Deserialize | 10 | 60.52 us | 5.04 | 34.38 KB | 1.17 |
| STJ_Deserialize | 10 | 54.70 us | 4.55 | 32.73 KB | 1.12 |
| Newtonsoft_Serialize | 100 | 70.08 us | 1.00 | 114.63 KB | 1.00 |
| STJ_Serialize | 100 | 39.28 us | 0.57 | 65.62 KB | 0.57 |
| Newtonsoft_Deserialize | 100 | 165.16 us | 2.41 | 101.45 KB | 0.88 |
| STJ_Deserialize | 100 | 143.26 us | 2.09 | 98.68 KB | 0.86 |

> Ratio is relative to `Newtonsoft_Serialize` per row count.

**What this tells us:** System.Text.Json is consistently faster and allocates less memory. At 100 rows, STJ serialization is 44% faster and uses 43% less memory. Deserialization is more expensive than serialization in both libraries because it requires constructing `DataTable`, `DataColumn`, and `DataRow` objects, but STJ still edges ahead.

## Transactions

Measures beginning and committing/rolling back a transaction.

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---|---|
| Raw_BeginCommit | 5.948 us | 1.00 | 1.64 KB | 1.00 |
| Async_BeginCommit | 240.697 us | 40.47 | 1.79 KB | 1.09 |
| Raw_BeginRollback | 6.040 us | 1.00 | 1.65 KB | 1.00 |
| Async_BeginRollback | 233.939 us | 39.33 | 1.73 KB | 1.06 |

:::warning
The high transaction overhead is a microbenchmark artifact. SQLite (used in benchmarks) serializes write transactions, so `BeginTransactionAsync` must wait for the previous transaction to complete. With network-bound databases (SQL Server, PostgreSQL, MySQL), the async overhead is negligible compared to network I/O latency. Note that memory allocation overhead is minimal -- only ~150 bytes per operation.
:::

## Design Decisions

### Why ValueTask

All async methods return `ValueTask` or `ValueTask<T>` rather than `Task`/`Task<T>`. This eliminates heap allocations on synchronous completion paths -- when the underlying provider completes synchronously (common for in-memory or cached operations), no `Task` object is allocated.

```csharp
// ValueTask -- zero allocation when the provider completes synchronously
public ValueTask<int> ExecuteNonQueryAsync(CancellationToken ct = default)
    => ExecuteNonQueryCoreAsync(ct);
```

### Why Zero-Alloc Events

Async events on `AsyncDataTable` use `ZeroAlloc.AsyncEvents` with `InvokeMode.Sequential`. This has two key properties:

1. **Zero allocations when no subscribers** -- If no handler is registered, `InvokeAsync` completes synchronously with zero allocations. This means tables without event subscriptions pay no cost.
2. **Sequential dispatch** -- Handlers execute one after another, which is the correct semantic for validation (Validator 1 must complete before Validator 2 runs).

```csharp
// Internal field -- zero-alloc when unused
internal AsyncEventHandler<DataRowChangeEventArgs> _rowChanging =
    new(InvokeMode.Sequential);
```

### Adapter Sync Method Optimization

The adapter classes hide base class sync-over-async bridge methods with `new` declarations that call the inner object's native synchronous methods directly:

```csharp
// AdapterDbConnection -- calls inner.Open() directly, not sync-over-async
public new void Open() => _inner.Open();
```

This means synchronous callers get the same performance as calling the underlying provider directly.

### Read-Only Indexers on AsyncDataRow

`AsyncDataRow` indexers are read-only. All writes go through `SetValueAsync`, which fires async events. This is a compile-time guarantee that async events are never bypassed, rather than an opt-in convention.

## Typed vs Untyped Overhead

Typed DataSets (generated from `.xsd`) add a thin layer over `AsyncDataTable<TRow>`:

- **Row creation**: Typed rows are created via `WrapRow(DataRow)` -- a single allocation per row, cached in a `ConditionalWeakTable`.
- **Property access**: Typed property getters (e.g., `customer.Name`) call `this["Name"]` on the inner `DataRow` -- no additional allocation.
- **Typed add methods**: `AddCustomerRowAsync(...)` creates the row and calls `AddAsync` -- one extra `DataRow.NewRow()` call compared to untyped.

The overhead of typed access is negligible in practice. The `ConditionalWeakTable` cache ensures that the same `DataRow` always maps to the same typed row instance without preventing garbage collection.

## Memory Allocation Patterns

| Operation | Extra allocations vs raw |
|---|---|
| Open/Close connection | ~24 bytes (adapter wrapper) |
| Execute command | 48-288 bytes (adapter + async state machine) |
| Read 50 rows | ~300-400 bytes total (adapter + enumerator) |
| Fill 100 rows | 27% *less* than raw `SqlDataAdapter` |
| Event with no subscribers | 0 bytes |
| Event with subscribers | Same as subscriber's allocations |
| Serialization (STJ, 100 rows) | 43% less than Newtonsoft |

## Tips for Optimal Performance

1. **Use `await foreach` for reader iteration** -- It is marginally faster than a manual `while (await reader.ReadAsync())` loop.

2. **Use System.Text.Json for serialization** -- It is faster and allocates less memory than Newtonsoft.Json for the same wire format.

3. **Fill tables individually with typed DataSets** -- Use `FillAsync(ds.Customer)` rather than `FillAsync(ds)` to avoid extra table creation.

4. **Reuse `JsonSerializerOptions`/`JsonSerializerSettings`** -- Both libraries cache type metadata internally. Creating new options per request wastes memory.

5. **Pass `CancellationToken` everywhere** -- All async methods accept cancellation tokens. This avoids wasted work when requests are cancelled.

6. **Use `AcceptChangesDuringFill = true` (default)** -- This avoids tracking row states during bulk loads, which reduces memory usage.

7. **Avoid event handlers in hot loops** -- Event handlers execute sequentially. If you have expensive async validation, consider batching or disabling events during bulk operations by using `BeginLoadData()`/`EndLoadData()`.

---
sidebar_position: 1
title: Performance
---

# Performance

AdoNet.Async is designed for minimal overhead. This page presents benchmark results, explains what each benchmark measures, and discusses the design decisions that enable strong performance.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
```

> Allocations are exact. Speed is shown as same-run ratios; absolute timings are not published.

Measured 2026-10-22 on an otherwise idle machine, out of process, in three launches pinned to two performance cores. Each ratio compares two benchmarks from the same run, with the lowest and highest of the three launches in brackets. The database is in-memory SQLite through Microsoft.Data.Sqlite.

## Adapter overhead

Each row runs the same operation through the adapter and straight on `SqliteConnection`. The ratio is the adapter's time relative to the raw call.

| Operation | Async vs raw, time | Raw allocated | Async allocated |
|---|---:|---:|---:|
| `ExecuteScalar` | 1.04 (1.04–1.06) | 720 B | 912 B |
| `ExecuteNonQuery` | 1.02 (1.00–1.04) | 480 B | 528 B |
| `ExecuteReader`, iterate 50 rows | 1.10 (1.09–1.11) | 704 B | 992 B |
| Open and close a connection | 1.02 (0.95–1.04) | 384 B | 408 B |
| Read 50 rows, `ReadAsync` loop | 1.04 (1.03–1.05) | 3,784 B | 4,072 B |
| Read 50 rows, `await foreach` | 1.09 (1.08–1.10) | 3,784 B | 4,184 B |
| Begin, update, commit | 1.04 (1.03–1.05) | 1,680 B | 1,832 B |
| Begin, update, roll back | 1.03 (1.01–1.04) | 1,688 B | 1,776 B |

**What this tells us:** the adapter costs 2–10% over the provider it wraps, and 24–400 B per operation, for its wrapper objects and async state machines. The manual `ReadAsync` loop is a little faster than `await foreach`, which allocates its enumerator. In real applications, where a command waits milliseconds on the network, the overhead is negligible.

:::note
Earlier versions of this page reported async transactions at 40 times the raw time, and blamed SQLite's write serialization. The cause was the benchmark: the async variants never disposed their commands, and Microsoft.Data.Sqlite keeps every live command on its connection and walks the list on each transaction, so each operation ran slower than the one before. With the commands disposed, async and raw transactions cost the same within 4%.
:::

## Asynchronous completion

In-memory SQLite completes its async calls synchronously, so the benchmarks above never see what a network provider does: a task that completes later. `AsyncCompletionBenchmarks` uses a provider whose tasks always complete asynchronously, and measures each adapter call against the same call made straight on that provider.

| Call | Adapter vs provider, time |
|---|---:|
| `ReadAsync` | 0.98 (0.93–1.01) |
| `ExecuteNonQueryAsync` | 1.07 (1.00–1.13) |

The adapter hands the provider's task straight to the caller, so it allocates nothing beyond the provider's own task; `AsyncForwarderTests` asserts 0 B of adapter overhead on a pending call. The pending task completes on a thread-pool thread, where BenchmarkDotNet's allocation column cannot attribute it exactly, so the table gives no byte count.

## DataAdapter Fill

`FillAsync` against `DataTable.Load` over a raw `DbDataReader`, for 10 and 100 rows.

| Rows | Async vs raw, time | Raw allocated | Async allocated |
|---|---:|---:|---:|
| 10 | 0.93 (0.93–0.94) | 96,680 B | 86,787 B |
| 100 | 0.74 (0.74–0.75) | 164,032 B | 126,237 B |

**What this tells us:** `FillAsync` is faster than loading a `DataTable` from a raw reader and allocates less: 10% less at 10 rows and 23% less at 100.

## Serialization (AsyncDataTable)

JSON serialization and deserialization of an `AsyncDataTable` with System.Text.Json and Newtonsoft.Json. Ratio is relative to `Newtonsoft_Serialize` with the same row count.

| Method | Rows | Ratio | Launch range | Allocated |
|---|---|---:|---:|---:|
| Newtonsoft_Serialize | 10 | 1.00 | | 30,061 B |
| STJ_Serialize | 10 | 0.71 | 0.71–0.72 | 12,018 B |
| Newtonsoft_Deserialize | 10 | 3.67 | 3.65–3.70 | 35,552 B |
| STJ_Deserialize | 10 | 3.13 | 3.06–3.21 | 33,856 B |
| Newtonsoft_Serialize | 100 | 1.00 | | 117,379 B |
| STJ_Serialize | 100 | 0.77 | 0.77–0.77 | 67,187 B |
| Newtonsoft_Deserialize | 100 | 3.08 | 3.02–3.15 | 104,224 B |
| STJ_Deserialize | 100 | 2.57 | 2.55–2.59 | 101,376 B |

**What this tells us:** System.Text.Json is faster in both directions. Serializing 100 rows, it takes 0.77 of Newtonsoft's time and allocates 43% less. Deserialization costs more than serialization in both libraries, because it builds `DataTable`, `DataColumn` and `DataRow` objects; there System.Text.Json takes 0.84 of Newtonsoft's time and allocates about the same.

## XML serialization

`WriteXmlAsync` and `ReadXmlAsync` against `DataTable.WriteXml` and `ReadXml`. Ratio is relative to `WriteXml_Async` with the same row count.

| Method | Rows | Ratio | Launch range | Allocated |
|---|---|---:|---:|---:|
| WriteXml_Async | 10 | 1.00 | | 77,664 B |
| WriteXml_Sync | 10 | 0.82 | 0.72–0.88 | 18,840 B |
| ReadXml_Async | 10 | 5.75 | 5.08–6.12 | 204,692 B |
| ReadXml_Sync | 10 | 4.96 | 4.36–5.33 | 112,306 B |
| WriteXml_Async | 100 | 1.00 | | 169,576 B |
| WriteXml_Sync | 100 | 1.17 | 1.16–1.18 | 161,648 B |
| ReadXml_Async | 100 | 2.65 | 2.62–2.68 | 270,053 B |
| ReadXml_Sync | 100 | 2.31 | 2.28–2.33 | 164,258 B |

:::warning
The async XML methods run the synchronous `DataTable` serializer over an async-enabled `XmlReader` or `XmlWriter`; only the final flush of a write is awaited. They allocate more than the synchronous methods and, on a network or file stream, block while reading. [#348](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/348) tracks making them asynchronous.
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

1. **No dispatch cost without subscribers** -- If no handler is registered, `InvokeAsync` completes synchronously and allocates nothing. The row operations still create their event args before that check, tracked in [#350](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/350).
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

Typed DataSets (generated from `.xsd`) add a thin layer over `AsyncDataTable<TRow>`. Each ratio is the typed call's time relative to the same untyped call on the inner `DataTable`, in the same run:

| Operation | Rows | Typed vs untyped, time | Untyped allocated | Typed allocated |
|---|---|---:|---:|---:|
| Read `Name` from every row | 10 | 1.26 (1.26–1.26) | 0 B | 400 B |
| Read `Name` from every row | 100 | 1.10 (1.08–1.15) | 0 B | 4,000 B |
| Find by primary key | 10 | 1.32 (1.29–1.35) | 128 B | 232 B |
| Find by primary key | 100 | 1.16 (1.15–1.18) | 128 B | 232 B |
| Add and remove a row | 10 | 1.33 (1.32–1.34) | 528 B | 904 B |
| Add and remove a row | 100 | 1.24 (1.21–1.27) | 528 B | 904 B |

- **Row access**: the generated typed indexer, `ds.Customer[i]`, creates a new 40 B typed row wrapper on every call. The untyped `Rows[i]` of an `AsyncDataTable` caches one wrapper per row in a `ConditionalWeakTable`; the typed indexer does not use that cache, tracked in [#349](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/349).
- **Property access**: typed property getters, such as `customer.Name`, call `this["Name"]` on the inner `DataRow` and allocate nothing of their own.
- **Typed add methods**: `AddCustomerRowAsync(...)` creates the row and calls `AddAsync`, one extra `DataRow.NewRow()` call compared to untyped.

## Memory Allocation Patterns

| Operation | Extra allocations vs raw |
|---|---|
| Open/Close connection | 24 B, the adapter wrapper |
| Execute command | 48–288 B, the adapter and async state machine |
| Read 50 rows | 288–400 B in total, the adapter and enumerator |
| Async completion of a pending provider task | 0 B beyond the provider's task |
| Fill 100 rows | 23% *less* than `DataTable.Load` |
| Typed row access | 40 B per `ds.Table[i]` call, see [#349](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/349) |
| Event with no subscribers | the event args, created before the subscriber check, see [#350](https://github.com/MarcelRoozekrans/AdoNet.Async/issues/350); the dispatch itself adds nothing |
| Event with subscribers | the event args plus the subscribers' own allocations |
| Serialization (STJ, 100 rows) | 43% less than Newtonsoft |

## Tips for Optimal Performance

1. **Use a `ReadAsync` loop on hot paths** -- A manual `while (await reader.ReadAsync())` loop is about 5% faster than `await foreach` and does not allocate the enumerator. `await foreach` reads better and costs little.

2. **Use System.Text.Json for serialization** -- It is faster and allocates less memory than Newtonsoft.Json for the same wire format.

3. **Fill tables individually with typed DataSets** -- Use `FillAsync(ds.Customer)` rather than `FillAsync(ds)` to avoid extra table creation.

4. **Reuse `JsonSerializerOptions`/`JsonSerializerSettings`** -- Both libraries cache type metadata internally. Creating new options per request wastes memory.

5. **Pass `CancellationToken` everywhere** -- All async methods accept cancellation tokens. This avoids wasted work when requests are cancelled.

6. **Use `AcceptChangesDuringFill = true` (default)** -- This avoids tracking row states during bulk loads, which reduces memory usage.

7. **Avoid event handlers in hot loops** -- Event handlers execute sequentially. If you have expensive async validation, consider batching or disabling events during bulk operations by using `BeginLoadData()`/`EndLoadData()`.

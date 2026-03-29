---
sidebar_position: 1
title: Introduction
---

# Introduction

**AdoNet.Async** is an async-first interface and base-class library for ADO.NET. It provides drop-in async wrappers for `System.Data` types, bringing modern `async/await`, `IAsyncEnumerable`, `ValueTask`, and async event support to the entire ADO.NET programming model.

## The problem

`System.Data` was designed in the .NET Framework era. While `DbConnection` and `DbDataReader` eventually gained a handful of `Task`-returning methods, the core interfaces (`IDbConnection`, `IDbCommand`, `IDataReader`, `IDataRecord`) remain fully synchronous. This means:

- **No async interfaces** -- You cannot program against an async abstraction. `IDbConnection.Open()` is synchronous; only the concrete `DbConnection.OpenAsync()` is available, and it returns `Task`, not `ValueTask`.
- **No `IAsyncEnumerable`** -- Iterating rows requires a manual `while (reader.Read())` loop. There is no way to `await foreach` over a result set.
- **No async events** -- `DataTable` events like `RowChanging` and `ColumnChanged` use synchronous delegates. If your handler needs to call an async API (validation service, audit log, cache invalidation), you are forced into sync-over-async or fire-and-forget patterns.
- **No `ValueTask`** -- The few async methods that exist return `Task`/`Task<T>`, which allocates on every call even when the operation completes synchronously.

## The solution

AdoNet.Async defines a complete set of async interfaces (`IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, `IAsyncDataRecord`, `IAsyncDbTransaction`) that mirror `System.Data` but add:

- **`ValueTask` everywhere** -- Every async method returns `ValueTask` or `ValueTask<T>` for zero-allocation on synchronous completion paths.
- **`IAsyncEnumerable<IAsyncDataRecord>`** -- `IAsyncDataReader` implements `IAsyncEnumerable`, so you can stream rows with `await foreach`.
- **Dual sync/async** -- Every interface exposes both synchronous and asynchronous members, enabling gradual migration without breaking existing code.
- **Async events** -- `AsyncDataTable` exposes 9 async events (column changing/changed, row changing/changed/deleting/deleted, table clearing/cleared, table new row) powered by `ZeroAlloc.AsyncEvents` for zero-allocation, sequential dispatch.
- **Adapter pattern** -- Existing `DbConnection`/`DbCommand`/`DbDataReader` instances are wrapped with a single `.AsAsync()` call. No provider-specific code needed.

## Packages

AdoNet.Async is split into six focused packages so you only take dependencies you need:

| Package | What it provides |
|---------|-----------------|
| **AdoNet.Async** | Core async interfaces (`IAsyncDbConnection`, `IAsyncDbCommand`, `IAsyncDataReader`, etc.) and abstract base classes. Zero external dependencies. |
| **AdoNet.Async.Adapters** | Adapter wrappers (`AdapterDbConnection`, `AdapterDbCommand`, etc.), the `.AsAsync()` extension method, and DI registration via `AddAsyncData()`. |
| **AdoNet.Async.DataSet** | `AsyncDataTable`, `AsyncDataSet`, `AsyncDataRow`, `AsyncDataRowCollection`, `AsyncDataAdapter`, and 9 async events. |
| **AdoNet.Async.DataSet.Generator** | Roslyn source generator that produces strongly typed async DataSet/DataTable/DataRow classes from `.xsd` schema files. |
| **AdoNet.Async.Serialization.NewtonsoftJson** | `AsyncDataTableConverter` and `AsyncDataSetConverter` for Newtonsoft.Json, wire-compatible with `Json.Net.DataSetConverters`. |
| **AdoNet.Async.Serialization.SystemTextJson** | `AsyncDataTableJsonConverter` and `AsyncDataSetJsonConverter` for System.Text.Json, producing the same wire format as the Newtonsoft package. |

## Who is this for?

- **Teams migrating existing ADO.NET code** -- Wrap your existing `DbConnection` with `.AsAsync()` and start using async interfaces immediately. Sync methods still work, so you can migrate incrementally.
- **New projects wanting async-first data access** -- Program against `IAsyncDbConnection` and `IAsyncDbCommand` from day one. Use dependency injection with `IAsyncDbProviderFactory` for clean abstractions.
- **Applications using `DataTable`/`DataSet`** -- If your codebase relies on `DataTable` for in-memory data manipulation, `AsyncDataTable` gives you async mutations, async events, and `await foreach` iteration while keeping full interop with the underlying `DataTable`.
- **Blazor WebAssembly developers** -- The library is WASM-safe: sync methods throw `PlatformNotSupportedException` on the browser platform, guiding you toward the async path.

## Next steps

- [Installation](./getting-started/installation.md) -- Install the packages you need
- [Quick Start](./getting-started/quick-start.md) -- See a complete working example
- [Async Interfaces](./core-concepts/async-interfaces.md) -- Understand the interface hierarchy
- [Await ForEach](./core-concepts/await-foreach.md) -- Stream rows with `IAsyncEnumerable`
- [Async DataTable](./core-concepts/async-datatable.md) -- Work with in-memory data asynchronously
- [Async Events](./core-concepts/async-events.md) -- Subscribe to async mutation events
- [Sync Bridge](./core-concepts/sync-bridge.md) -- Understand when sync methods are safe to use

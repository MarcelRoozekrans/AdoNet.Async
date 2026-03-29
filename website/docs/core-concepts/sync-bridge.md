---
sidebar_position: 5
title: Sync Bridge
---

# Sync Bridge

Every async interface in AdoNet.Async also exposes synchronous methods. This dual sync/async surface enables gradual migration: you can wrap an existing connection and start using async where it matters, while keeping sync calls in code you have not migrated yet.

## How sync methods work

The abstract base classes (`AsyncDbConnection`, `AsyncDbDataReader`, `AsyncDbCommand`, `AsyncDbTransaction`, `AsyncDataAdapter`) implement sync methods by calling the async core method and blocking on the result:

```csharp
// Inside AsyncDbConnection
public void Open()
{
    SyncBridge.ThrowIfBrowser(nameof(OpenAsync));
    OpenCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
}
```

The pattern is the same everywhere:

1. Check if running on WASM browser -- if so, throw `PlatformNotSupportedException`
2. Call the async core method with `CancellationToken.None`
3. Block with `.GetAwaiter().GetResult()`

## WASM browser safety

The `SyncBridge.ThrowIfBrowser` check prevents sync-over-async deadlocks on Blazor WebAssembly, where the browser's single-threaded event loop cannot be blocked:

```csharp
internal static class SyncBridge
{
    internal static void ThrowIfBrowser(string asyncMethodName)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                $"Synchronous operations are not supported on this platform. " +
                $"Use {asyncMethodName}() instead.");
        }
    }
}
```

On all other platforms (server, desktop, console), sync methods work normally.

:::warning
On Blazor WebAssembly, calling any sync method (`Open()`, `Read()`, `ExecuteScalar()`, etc.) will throw `PlatformNotSupportedException`. Always use the async variants in WASM environments.
:::

## Adapter optimization: no sync-over-async

The `AdoNet.Async.Adapters` package provides an important optimization. The adapter wrappers (`AdapterDbConnection`, `AdapterDbDataReader`, `AdapterDbCommand`, `AdapterDbTransaction`) **hide** the base class sync methods with `new` methods that call the native sync method on the inner object directly:

```csharp
// Inside AdapterDbConnection
public new void Open() => _inner.Open();       // calls DbConnection.Open() directly
public new void Close() => _inner.Close();     // calls DbConnection.Close() directly

// Inside AdapterDbDataReader
public new bool Read() => _inner.Read();       // calls DbDataReader.Read() directly
public new bool NextResult() => _inner.NextResult();
public new void Close() => _inner.Close();
```

This means that when you use the adapter wrappers (which is the common case), sync methods do **not** go through the sync-over-async bridge. They call the provider's native synchronous implementation directly, with zero overhead.

### When the optimization applies

| Type | Sync method | What happens |
|---|---|---|
| `AdapterDbConnection` | `Open()` | Calls `DbConnection.Open()` directly |
| `AdapterDbConnection` | `Close()` | Calls `DbConnection.Close()` directly |
| `AdapterDbDataReader` | `Read()` | Calls `DbDataReader.Read()` directly |
| `AdapterDbDataReader` | `NextResult()` | Calls `DbDataReader.NextResult()` directly |
| `AdapterDbDataReader` | `Close()` | Calls `DbDataReader.Close()` directly |

### When the bridge is used

If you implement a custom `AsyncDbConnection` or `AsyncDbDataReader` subclass (without hiding the sync methods), the base class sync bridge is used. This is the less common case, and it still works correctly on all platforms except WASM browser.

The `AsyncDataAdapter` base class also uses the sync bridge for its `Fill()` and `Update()` methods, since there is no native sync adapter to delegate to.

## When to use sync vs async

### Prefer async

- ASP.NET / web applications -- thread pool efficiency
- Blazor WebAssembly -- sync is not available
- High-concurrency applications -- avoid blocking threads
- New code -- no reason to use sync in new async-first code

### Sync is acceptable

- Console applications -- blocking is fine when there is no concurrency concern
- Migration scenarios -- you are wrapping a connection but have not migrated all callers to async yet
- Tests -- simpler test code when async is not under test
- Desktop UI event handlers -- where `async void` would be needed otherwise (though `async Task` with proper error handling is still preferred)

:::tip
When using the adapter wrappers, sync methods have no overhead beyond the native provider's sync implementation. There is no performance penalty for calling `Open()` instead of `await OpenAsync()` on an `AdapterDbConnection`. The only reason to prefer async is for scalability (freeing up threads during I/O).
:::

## AsyncDataTable: blocked sync paths

`AsyncDataTable` takes a stricter approach than the connection/reader types. Two methods that would bypass async events are marked as `[Obsolete]` with `error: true`:

```csharp
[Obsolete("Use AcceptChangesAsync(). Calling AcceptChanges() bypasses async events.", error: true)]
public void AcceptChanges() => throw new NotSupportedException("Use AcceptChangesAsync().");

[Obsolete("Use ClearAsync(). Calling Clear() bypasses async events.", error: true)]
public void Clear() => throw new NotSupportedException("Use ClearAsync().");
```

These are compile-time errors if you try to call them. This is intentional: `AcceptChanges()` and `Clear()` on the inner `DataTable` would skip async event firing, which could silently break event-driven logic. The async equivalents (`AcceptChangesAsync`, `ClearAsync`) are the only supported paths.

Other `AsyncDataRow` methods like `AcceptChangesAsync`, `RejectChangesAsync`, `BeginEditAsync`, `CancelEditAsync`, and `EndEditAsync` are async-only by design -- they have no sync counterparts on `AsyncDataRow`.

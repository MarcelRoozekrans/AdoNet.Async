---
sidebar_position: 2
title: Await ForEach
---

# Await ForEach

`IAsyncDataReader` implements `IAsyncEnumerable<IAsyncDataRecord>`, which means you can iterate rows using `await foreach` instead of writing a manual read loop.

## Basic usage

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    await foreach (IAsyncDataRecord record in reader)
    {
        int id = record.GetInt32(0);
        string name = record.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

This is equivalent to the manual loop pattern:

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    while (await reader.ReadAsync())
    {
        int id = reader.GetInt32(0);
        string name = reader.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

Both patterns produce identical behavior. The `await foreach` version is more concise and harder to get wrong (no risk of forgetting the `Read` call or mixing up the loop condition).

## Cancellation token support

Pass a `CancellationToken` using the `WithCancellation` extension method:

```csharp
IAsyncDataReader reader = await cmd.ExecuteReaderAsync(ct);
await using (reader)
{
    await foreach (IAsyncDataRecord record in reader.WithCancellation(ct))
    {
        int id = record.GetInt32(0);
        string name = record.GetString(1);
        Console.WriteLine($"{id}: {name}");
    }
}
```

The token is passed through to `ReadAsync` on each iteration. If cancellation is requested, the next `ReadAsync` call will throw `OperationCanceledException`.

## How it works internally

The `AsyncDbDataReader` base class implements `GetAsyncEnumerator` as an async iterator that yields `this` on each successful read:

```csharp
public async IAsyncEnumerator<IAsyncDataRecord> GetAsyncEnumerator(
    CancellationToken cancellationToken = default)
{
    while (await ReadCoreAsync(cancellationToken).ConfigureAwait(false))
    {
        yield return this;
    }
}
```

There are a few things to note about this implementation:

1. **The reader yields itself.** Each `yield return this` returns the reader as an `IAsyncDataRecord`. The record's field values change on each iteration because the reader has advanced to the next row. This matches how `DbDataReader` works -- the reader *is* the current record.

2. **No extra allocations per row.** Because the reader yields `this` rather than creating a new object for each row, there is no per-row allocation beyond what the async state machine requires.

3. **The enumerator is one-shot.** Once the reader has been iterated to completion, calling `GetAsyncEnumerator` again will not restart the reader. The rows are consumed, just like a `DbDataReader`.

:::warning
Do not hold references to `IAsyncDataRecord` across iterations. The record is the reader itself, so its field values change on every loop iteration. If you need to keep data from a row, copy the values into your own object before the next iteration.
:::

## Comparison: await foreach vs manual loop

| Aspect | `await foreach` | Manual `ReadAsync` loop |
|---|---|---|
| Conciseness | More concise | Slightly more verbose |
| Cancellation | Via `WithCancellation()` | Pass token to `ReadAsync()` directly |
| Access to reader | Only `IAsyncDataRecord` | Full `IAsyncDataReader` (can check `HasRows`, `Depth`, etc.) |
| Multiple result sets | Need to break out and call `NextResultAsync` | Natural with a nested loop |
| Performance | Essentially identical | Essentially identical |

Use `await foreach` when you are iterating a single result set and only need record-level access. Use a manual loop when you need reader-level properties or multiple result sets.

## Multiple result sets

When a command returns multiple result sets, use `NextResultAsync` to advance to the next result set after consuming the current one:

```csharp
IAsyncDbCommand cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT Id, Name FROM Users;
    SELECT Id, Title FROM Products;";

IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    // First result set: Users
    Console.WriteLine("Users:");
    await foreach (IAsyncDataRecord record in reader)
    {
        Console.WriteLine($"  {record.GetInt32(0)}: {record.GetString(1)}");
    }

    // Advance to second result set
    if (await reader.NextResultAsync())
    {
        Console.WriteLine("Products:");
        await foreach (IAsyncDataRecord record in reader)
        {
            Console.WriteLine($"  {record.GetInt32(0)}: {record.GetString(1)}");
        }
    }
}
```

:::info
After `await foreach` completes (the reader has been fully iterated for the current result set), you can call `NextResultAsync()` to move to the next result set and start another `await foreach` loop.
:::

## Using LINQ with IAsyncEnumerable

Because `IAsyncDataReader` implements `IAsyncEnumerable<IAsyncDataRecord>`, you can use `System.Linq.Async` operators if you add the `System.Linq.Async` NuGet package:

```csharp
using System.Linq.Async;

IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    // Take the first 10 rows
    await foreach (var record in reader.Take(10))
    {
        Console.WriteLine(record.GetString(0));
    }
}
```

```csharp
using System.Linq.Async;

// Project to a list of objects
IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
await using (reader)
{
    List<User> users = await reader
        .Select(r => new User(r.GetInt32(0), r.GetString(1)))
        .ToListAsync();
}
```

:::tip
`System.Linq.Async` is a separate NuGet package (`dotnet add package System.Linq.Async`). It is not required to use `await foreach` -- it only adds LINQ-style operators like `Select`, `Where`, `Take`, `ToListAsync`, etc.
:::

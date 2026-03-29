# API Sealing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Seal the async wrapper API so inner System.Data types don't leak — implicit operators become explicit, methods returning raw types return async wrappers, and row/table wrappers are identity-cached.

**Architecture:** Add `ConditionalWeakTable` caches to `AsyncDataTable` (for rows) and `AsyncDataSet` (for tables). Create `AsyncDataTableCollection` wrapper. Change return types of ~15 methods. Convert 2 implicit operators to explicit. Update all test files that used implicit casts to use explicit casts.

**Tech Stack:** .NET 10, xUnit 2.x, FluentAssertions 8.x, ZeroAlloc.AsyncEvents

---

## Phase 1: Row Caching on AsyncDataTable

### Task 1: Add ConditionalWeakTable row cache to AsyncDataTable

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Modify: `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableTests.cs`

Add a `ConditionalWeakTable<DataRow, AsyncDataRow>` to `AsyncDataTable` and a `GetOrCreateRow` method. Then update `AsyncDataRowCollection` indexer and enumerator to use it instead of `new AsyncDataRow(...)`.

**Step 1: Add cache and method to AsyncDataTable**

In `src/System.Data.Async.DataSet/AsyncDataTable.cs`, add field and method:

```csharp
using System.Runtime.CompilerServices;

// Add field after existing fields:
private readonly ConditionalWeakTable<DataRow, AsyncDataRow> _rowCache = new();

// Add method:
internal virtual AsyncDataRow GetOrCreateRow(DataRow inner)
{
    return _rowCache.GetValue(inner, key => new AsyncDataRow(key, this));
}
```

Note: `virtual` so `AsyncDataTable<TRow>` can override to return typed rows.

**Step 2: Update AsyncDataRowCollection to use cache**

In `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`:
- Change indexer from `new AsyncDataRow(_inner[index], _table)` to `_table.GetOrCreateRow(_inner[index])`
- Change enumerator from `new AsyncDataRow(_inner[i], _table)` to `_table.GetOrCreateRow(_inner[i])`

**Step 3: Update AsyncDataTable.NewRow() to use cache**

In `AsyncDataTable.cs` line 74:
- Change `new AsyncDataRow(_inner.NewRow(), this)` to cache the new row too: create the inner row, then `GetOrCreateRow(innerRow)`

**Step 4: Override in AsyncDataTable\<TRow>**

In `src/System.Data.Async.DataSet/AsyncDataTable{TRow}.cs`, override:

```csharp
internal override AsyncDataRow GetOrCreateRow(DataRow inner)
{
    return _rowCache.GetValue(inner, key => WrapRow(key));
}
```

Wait — `_rowCache` is private on the base. We need it protected or we need to use a separate cache. Simplest: make `_rowCache` a `protected` field... but CA1051. Better: add a protected method `GetOrAddCachedRow` that the base and subclass both use:

Actually, the simplest approach: `GetOrCreateRow` is `internal virtual` on the base. The base implements it with the cache. The generic override calls `WrapRow`. The cache lives on the base class. The key insight: the base's `ConditionalWeakTable` stores `AsyncDataRow` values — since `TRow : AsyncDataRow`, the typed rows can be stored there too. So the override just needs:

```csharp
internal override AsyncDataRow GetOrCreateRow(DataRow inner)
{
    return _rowCache.GetValue(inner, key => WrapRow(key));
}
```

But `_rowCache` is private! Solution: make it `private protected` (accessible to subclasses in the same assembly only — but generated code is in other assemblies). Better: just keep the cache internal and have the method on the base handle it all:

Actually simplest: keep `_rowCache` private on the base. Make `GetOrCreateRow` virtual and use a factory pattern:

```csharp
// In AsyncDataTable:
private readonly ConditionalWeakTable<DataRow, AsyncDataRow> _rowCache = new();

protected virtual AsyncDataRow CreateRow(DataRow inner) => new(inner, this);

internal AsyncDataRow GetOrCreateRow(DataRow inner)
{
    return _rowCache.GetValue(inner, CreateRow);
}
```

Then in `AsyncDataTable<TRow>`:
```csharp
protected override AsyncDataRow CreateRow(DataRow inner) => WrapRow(inner);
```

This way the cache is private, only `CreateRow` is virtual.

**Step 5: Write identity test**

Add to `tests/System.Data.Async.DataSet.Tests/AsyncDataTableTests.cs`:

```csharp
[Fact]
public void Row_Indexer_Returns_Same_Instance()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));
    table.Rows.AddAsync(new object?[] { 1 }).GetAwaiter().GetResult();

    var row1 = table.Rows[0];
    var row2 = table.Rows[0];

    row1.Should().BeSameAs(row2);
}
```

**Step 6: Run all DataSet tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests -v n`
Expected: All pass.

**Step 7: Commit**

```bash
git commit -m "feat: add identity-preserving row cache to AsyncDataTable"
```

---

## Phase 2: Implicit to Explicit Operators

### Task 2: Convert implicit operators to explicit

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs` (line 238)
- Modify: `src/System.Data.Async.DataSet/AsyncDataSet.cs` (line 130)
- Modify: All test files using implicit casts (21 occurrences across 12 files)
- Modify: `src/System.Data.Async.Serialization.NewtonsoftJson/` — converters use implicit cast
- Modify: `src/System.Data.Async.Serialization.SystemTextJson/` — converters use implicit cast
- Modify: `src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs` — uses implicit cast
- Modify: `src/System.Data.Async.DataSet.Generator/Emit/` — generated code uses implicit cast

**Step 1: Change operators**

In `AsyncDataTable.cs` line 238:
```csharp
// Before:
public static implicit operator DataTable(AsyncDataTable asyncTable) => asyncTable._inner;
// After:
public static explicit operator DataTable(AsyncDataTable asyncTable) => asyncTable._inner;
```

In `AsyncDataSet.cs` line 130:
```csharp
// Before:
public static implicit operator System.Data.DataSet(AsyncDataSet asyncDataSet) => asyncDataSet._inner;
// After:
public static explicit operator System.Data.DataSet(AsyncDataSet asyncDataSet) => asyncDataSet._inner;
```

**Step 2: Fix all compilation errors**

Every place that used `DataTable dt = asyncTable;` must become `DataTable dt = (DataTable)asyncTable;` or `var dt = (DataTable)asyncTable;`.

Files to fix (search for compilation errors):
- Test files: ~21 occurrences across 12 test files
- Serialization converters: both Newtonsoft and STJ converters
- AdapterDbDataAdapter: `FillAsync` / `UpdateAsync` methods
- Generator emitters: generated code that casts `((DataTable)this)`

**Step 3: Run full solution build**

Run: `dotnet build System.Data.Async.slnx`
Expected: Build succeeds with 0 errors.

**Step 4: Run all tests**

Run: `dotnet test System.Data.Async.slnx -v n`
Expected: All pass.

**Step 5: Commit**

```bash
git commit -m "refactor!: convert implicit DataTable/DataSet operators to explicit"
```

---

## Phase 3: Wrap Return Types on AsyncDataTable

### Task 3: Wrap Select, GetErrors, GetChanges, Clone, Copy, ImportRow, LoadDataRow

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableTests.cs`

**Step 1: Change method signatures**

In `AsyncDataTable.cs`:

```csharp
// Select — return AsyncDataRow[] using cache
public AsyncDataRow[] Select()
    => _inner.Select().Select(GetOrCreateRow).ToArray();
public AsyncDataRow[] Select(string? filterExpression)
    => _inner.Select(filterExpression).Select(GetOrCreateRow).ToArray();
public AsyncDataRow[] Select(string? filterExpression, string? sort)
    => _inner.Select(filterExpression, sort).Select(GetOrCreateRow).ToArray();
public AsyncDataRow[] Select(string? filterExpression, string? sort, DataViewRowState recordStates)
    => _inner.Select(filterExpression, sort, recordStates).Select(GetOrCreateRow).ToArray();

// GetErrors
public AsyncDataRow[] GetErrors()
    => _inner.GetErrors().Select(GetOrCreateRow).ToArray();

// GetChanges
public AsyncDataTable? GetChanges()
{
    var changes = _inner.GetChanges();
    return changes != null ? new AsyncDataTable(changes) : null;
}
public AsyncDataTable? GetChanges(DataRowState rowStates)
{
    var changes = _inner.GetChanges(rowStates);
    return changes != null ? new AsyncDataTable(changes) : null;
}

// Clone/Copy
public AsyncDataTable Clone() => new(_inner.Clone());
public AsyncDataTable Copy() => new(_inner.Copy());

// ImportRow
public void ImportRow(AsyncDataRow row) => _inner.ImportRow(row.InnerDataRow);

// LoadDataRow
public AsyncDataRow LoadDataRow(object?[] values, bool fAcceptChanges)
    => GetOrCreateRow(_inner.LoadDataRow(values, fAcceptChanges));
public AsyncDataRow LoadDataRow(object?[] values, LoadOption loadOption)
    => GetOrCreateRow(_inner.LoadDataRow(values, loadOption));

// Merge — accept AsyncDataTable
public void Merge(AsyncDataTable table) => _inner.Merge(table.InnerDataTable);
public void Merge(AsyncDataTable table, bool preserveChanges) => _inner.Merge(table.InnerDataTable, preserveChanges);
public void Merge(AsyncDataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
    => _inner.Merge(table.InnerDataTable, preserveChanges, missingSchemaAction);

// DataSet property — return AsyncDataSet (but we don't have a parent reference yet, leave for Phase 4)
```

**Step 2: Add using for LINQ**

Add `using System.Linq;` to AsyncDataTable.cs if not present.

**Step 3: Write tests for wrapped returns**

Add to tests:

```csharp
[Fact]
public void Select_Returns_AsyncDataRow_Array()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));
    table.Rows.AddAsync(new object?[] { 1 }).GetAwaiter().GetResult();
    table.Rows.AddAsync(new object?[] { 2 }).GetAwaiter().GetResult();

    var rows = table.Select();
    rows.Should().HaveCount(2);
    rows.Should().AllBeOfType<AsyncDataRow>();
}

[Fact]
public void Select_Returns_Cached_Rows()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));
    table.Rows.AddAsync(new object?[] { 1 }).GetAwaiter().GetResult();

    var row1 = table.Select()[0];
    var row2 = table.Rows[0];

    row1.Should().BeSameAs(row2);
}

[Fact]
public void GetChanges_Returns_AsyncDataTable()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));
    table.Rows.AddAsync(new object?[] { 1 }).GetAwaiter().GetResult();

    var changes = table.GetChanges();
    changes.Should().BeOfType<AsyncDataTable>();
}

[Fact]
public void Clone_Returns_AsyncDataTable()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));

    var clone = table.Clone();
    clone.Should().BeOfType<AsyncDataTable>();
}
```

**Step 4: Fix any test compilation errors from changed signatures**

Some existing tests may use `Select()` expecting `DataRow[]` — fix them to expect `AsyncDataRow[]`.

**Step 5: Run all tests**

Run: `dotnet test System.Data.Async.slnx -v n`

**Step 6: Commit**

```bash
git commit -m "refactor!: wrap AsyncDataTable return types to prevent inner type leaks"
```

---

## Phase 4: AsyncDataTableCollection + Wrap AsyncDataSet Return Types

### Task 4: Create AsyncDataTableCollection and seal AsyncDataSet

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataTableCollection.cs`
- Modify: `src/System.Data.Async.DataSet/AsyncDataSet.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataSetTests.cs`

**Step 1: Create AsyncDataTableCollection**

```csharp
using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Data.Async.DataSet;

public class AsyncDataTableCollection : IEnumerable<AsyncDataTable>
{
    private readonly DataTableCollection _inner;
    private readonly AsyncDataSet _parent;

    internal AsyncDataTableCollection(DataTableCollection inner, AsyncDataSet parent)
    {
        _inner = inner;
        _parent = parent;
    }

    public int Count => _inner.Count;

    public AsyncDataTable this[string name]
    {
        get
        {
            var dt = _inner[name] ?? throw new ArgumentException($"Table '{name}' not found.", nameof(name));
            return _parent.GetOrCreateTable(dt);
        }
    }

    public AsyncDataTable this[int index] => _parent.GetOrCreateTable(_inner[index]);

    public bool Contains(string name) => _inner.Contains(name);

    public void Add(AsyncDataTable table) => _inner.Add(table.InnerDataTable);
    public void Remove(AsyncDataTable table) => _inner.Remove(table.InnerDataTable);
    public void Remove(string name) => _inner.Remove(name);

#pragma warning disable HLQ006
    public IEnumerator<AsyncDataTable> GetEnumerator()
    {
        for (int i = 0; i < _inner.Count; i++)
            yield return _parent.GetOrCreateTable(_inner[i]);
    }
#pragma warning restore HLQ006

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

**Step 2: Add table cache and method to AsyncDataSet**

In `AsyncDataSet.cs`:

```csharp
using System.Runtime.CompilerServices;

// Add fields:
private readonly ConditionalWeakTable<DataTable, AsyncDataTable> _tableCache = new();
private readonly AsyncDataTableCollection _tables;

// Update constructors to init _tables:
public AsyncDataSet() { _inner = new DataSet(); _tables = new AsyncDataTableCollection(_inner.Tables, this); }
// etc for all constructors

// Add method:
internal virtual AsyncDataTable GetOrCreateTable(DataTable inner)
{
    return _tableCache.GetValue(inner, key => new AsyncDataTable(key));
}

// Change Tables property:
public AsyncDataTableCollection Tables => _tables;
```

**Step 3: Wrap AsyncDataSet return types**

```csharp
// GetChanges
public AsyncDataSet? GetChanges()
{
    var changes = _inner.GetChanges();
    return changes != null ? new AsyncDataSet(changes) : null;
}
public AsyncDataSet? GetChanges(DataRowState rowStates)
{
    var changes = _inner.GetChanges(rowStates);
    return changes != null ? new AsyncDataSet(changes) : null;
}

// Clone/Copy
public AsyncDataSet Clone() => new(_inner.Clone());
public AsyncDataSet Copy() => new(_inner.Copy());

// Merge — accept async types
public void Merge(AsyncDataSet dataSet) => _inner.Merge(dataSet._inner);
public void Merge(AsyncDataTable table) => _inner.Merge(table.InnerDataTable);
public void Merge(AsyncDataSet dataSet, bool preserveChanges) => _inner.Merge(dataSet._inner, preserveChanges);
public void Merge(AsyncDataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
    => _inner.Merge(table.InnerDataTable, preserveChanges, missingSchemaAction);
```

**Step 4: Write tests**

```csharp
[Fact]
public void Tables_Returns_AsyncDataTableCollection()
{
    using var ds = new AsyncDataSet("Test");
    ds.Tables.Should().BeOfType<AsyncDataTableCollection>();
}

[Fact]
public void Tables_Indexer_Returns_Cached_Instance()
{
    using var ds = new AsyncDataSet("Test");
    ((DataSet)ds).Tables.Add(new DataTable("T"));

    var t1 = ds.Tables["T"];
    var t2 = ds.Tables["T"];
    t1.Should().BeSameAs(t2);
}

[Fact]
public void Clone_Returns_AsyncDataSet()
{
    using var ds = new AsyncDataSet("Test");
    var clone = ds.Clone();
    clone.Should().BeOfType<AsyncDataSet>();
}
```

**Step 5: Fix any compilation errors across solution**

The `Tables` property now returns `AsyncDataTableCollection` not `DataTableCollection`. Some code may need updating.

**Step 6: Run all tests**

Run: `dotnet test System.Data.Async.slnx -v n`

**Step 7: Commit**

```bash
git commit -m "refactor!: add AsyncDataTableCollection and seal AsyncDataSet return types"
```

---

## Phase 5: Update Generated Code

### Task 5: Update emitters for sealed API

**Files:**
- Modify: `src/System.Data.Async.DataSet.Generator/Emit/DataSetEmitter.cs`
- Modify: `src/System.Data.Async.DataSet.Generator/Emit/DataTableEmitter.cs`
- Modify: `src/System.Data.Async.DataSet.Generator/Emit/DataRowEmitter.cs`

**Step 1: Update DataSetEmitter**

The generated `InitClass` currently casts via `(DataTable)table` (the now-explicit operator). Change to use `table.InnerDataTable` (which is `protected internal`).

The generated `InitVars` accesses `Tables` which is now `AsyncDataTableCollection`. It should cast typed tables from the inner DataSet: `(DataSet)this` → `Tables["{name}"]`.

Actually, generated code runs in external assemblies. `InnerDataTable` is `protected internal` — accessible to subclasses. The emitter-generated code inherits from `AsyncDataTable<TRow>` and `AsyncDataSet`, so it CAN access `InnerDataTable` and `InnerDataSet` (wait — `InnerDataSet` is `internal`, not `protected internal`).

We may need to make `InnerDataSet` `protected internal` too (like we did for `InnerDataTable` in Phase 1 of the generator plan).

**Step 2: Fix emitter casts**

Wherever the emitter generates `((global::System.Data.DataTable)this)`, change to `InnerDataTable` since generated tables inherit from `AsyncDataTable<TRow>`.

Wherever the emitter generates `((global::System.Data.DataSet)this)`, make `InnerDataSet` protected internal and use that instead.

**Step 3: Update DataRowEmitter relation navigation**

The emitter's `GetChildRows` and parent row accessors create typed tables. They should use the parent DataSet's table cache if possible. Since generated rows hold a reference to their typed table, relation navigation should look up tables via the DataSet wrapper.

**Step 4: Rebuild and test**

Run: `dotnet build System.Data.Async.slnx`
Run: `dotnet test System.Data.Async.slnx -v n`

**Step 5: Commit**

```bash
git commit -m "refactor: update generated code for sealed API (explicit casts, InnerDataTable)"
```

---

## Phase 6: Fix Serialization Converters

### Task 6: Update converters for explicit casts

**Files:**
- Modify: `src/System.Data.Async.Serialization.NewtonsoftJson/Converters/AsyncDataTableConverter.cs`
- Modify: `src/System.Data.Async.Serialization.NewtonsoftJson/Converters/AsyncDataSetConverter.cs`
- Modify: `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataTableJsonConverter.cs`
- Modify: `src/System.Data.Async.Serialization.SystemTextJson/Converters/AsyncDataSetJsonConverter.cs`

The converters internally cast `AsyncDataTable` → `DataTable` for serialization. Change all implicit casts to explicit casts.

**Step 1: Update converter casts**

Search for any implicit cast usage and add explicit `(DataTable)` or `(DataSet)` casts.

The converters also need to handle `AsyncDataTable<TRow>` subclasses — since `CanConvert` checks type assignment, a `JsonConverter<AsyncDataTable>` should already handle subclasses.

**Step 2: Run serialization tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "Converter" -v n`
Run: `dotnet test tests/System.Data.Async.Integration.Tests -v n`
Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Integration.Tests --filter "Serialization" -v n`

**Step 3: Commit**

```bash
git commit -m "fix: update serialization converters for explicit cast operators"
```

---

## Phase 7: Update Adapter

### Task 7: Update AdapterDbDataAdapter for explicit casts

**Files:**
- Modify: `src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs`

The adapter's `FillAsync` and `UpdateAsync` methods access `InnerDataTable` via cast. Change implicit to explicit.

**Step 1: Fix casts**

Change `DataTable dt = asyncTable;` to `var dt = (DataTable)asyncTable;` or use `asyncTable.InnerDataTable` if accessible (it's `protected internal` — accessible from same assembly? The adapter is in a different assembly, so use explicit cast).

**Step 2: Run adapter tests**

Run: `dotnet test tests/System.Data.Async.Adapters.Tests -v n`

**Step 3: Commit**

```bash
git commit -m "fix: update adapter for explicit cast operators"
```

---

## Phase 8: DataSet property on AsyncDataTable

### Task 8: Return AsyncDataSet from AsyncDataTable.DataSet property

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Modify: `src/System.Data.Async.DataSet/AsyncDataSet.cs`

Currently `AsyncDataTable.DataSet` returns `System.Data.DataSet?`. It should return `AsyncDataSet?`.

The challenge: a table added to a DataSet doesn't know about the `AsyncDataSet` wrapper. We need a reverse lookup or parent reference.

**Approach:** When `AsyncDataSet.GetOrCreateTable` wraps a table, store a back-reference on the `AsyncDataTable`. Add an internal `AsyncDataSet? _parentDataSet` field on `AsyncDataTable`, set it in `GetOrCreateTable`.

```csharp
// In AsyncDataTable:
internal AsyncDataSet? ParentAsyncDataSet { get; set; }

public AsyncDataSet? DataSet => ParentAsyncDataSet;
```

```csharp
// In AsyncDataSet.GetOrCreateTable:
internal virtual AsyncDataTable GetOrCreateTable(DataTable inner)
{
    return _tableCache.GetValue(inner, key =>
    {
        var t = new AsyncDataTable(key);
        t.ParentAsyncDataSet = this;
        return t;
    });
}
```

**Step 1: Implement**
**Step 2: Test**

```csharp
[Fact]
public void DataSet_Property_Returns_AsyncDataSet()
{
    using var ds = new AsyncDataSet("Test");
    var table = new AsyncDataTable("T");
    ds.Tables.Add(table);

    // Access via collection to get cached wrapper
    var t = ds.Tables["T"];
    t.DataSet.Should().BeSameAs(ds);
}
```

**Step 3: Commit**

```bash
git commit -m "feat: AsyncDataTable.DataSet returns AsyncDataSet with parent back-reference"
```

---

## Phase 9: Full Verification

### Task 9: Run full test suite and verify no regressions

**Step 1: Build in Release mode**

Run: `dotnet build System.Data.Async.slnx -c Release`

**Step 2: Run all tests**

Run: `dotnet test System.Data.Async.slnx -v n`

**Step 3: Verify no implicit operator usage remains**

Search codebase for any remaining implicit casts:

```bash
grep -rn "DataTable [a-z].*=.*async\|DataSet [a-z].*=.*async" src/
```

**Step 4: Commit any final fixes**

```bash
git commit -m "fix: resolve final issues from full verification"
```

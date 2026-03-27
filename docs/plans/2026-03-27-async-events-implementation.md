# Async Events Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add `AsyncDataRow`, `AsyncDataRowCollection`, and 9 async events to `AsyncDataTable` using `ZeroAlloc.AsyncEvents`, forcing all row mutations through `async`/`await` APIs.

**Architecture:** `AsyncDataRow` wraps `DataRow` with getter-only indexers and `ValueTask`-returning mutation methods; it holds a back-reference to `AsyncDataTable` to reach the `internal AsyncEventHandler<T>` fields. `AsyncDataRowCollection` wraps `DataRowCollection`, returning `AsyncDataRow` from its getter-only indexer and exposing `AddAsync`/`RemoveAsync`/`RemoveAtAsync`. `AsyncDataTable` initialises an `AsyncDataRowCollection` in every constructor and exposes 9 async events. Sync events (`RowChanged`, `ColumnChanging`, …) are preserved — sync subscribers continue working via the inner `DataTable`.

**Tech Stack:** .NET 10, `ZeroAlloc.AsyncEvents 1.*` (`AsyncEventHandler<T>` struct, `AsyncEvent<T>` delegate, `InvokeMode.Sequential`), xUnit, FluentAssertions.

---

### Task 1: Add ZeroAlloc.AsyncEvents dependency

**Files:**
- Modify: `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj`

**Step 1: Add the package reference**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.DataSet</RootNamespace>
    <PackageId>AdoNet.Async.DataSet</PackageId>
    <Title>AdoNet.Async.DataSet</Title>
    <Description>Async DataSet and DataTable for ADO.NET (System.Data). Includes AsyncDataTable, AsyncDataSet, and AsyncDataAdapter.</Description>
    <PackageTags>system.data.async;async;ado.net;dataset;datatable;dataadapter;valuetask</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async\System.Data.Async.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ZeroAlloc.AsyncEvents" Version="1.*" />
  </ItemGroup>

</Project>
```

**Step 2: Verify build**

```bash
dotnet build src/System.Data.Async.DataSet -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

**Step 3: Commit**

```bash
git add src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj
git commit -m "feat: add ZeroAlloc.AsyncEvents dependency to DataSet package"
```

---

### Task 2: Create AsyncDataRow

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataRow.cs`
- Create: `tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs`

**Step 1: Write the failing tests**

Create `tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs`:

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowTests
{
    private static (AsyncDataTable table, AsyncDataRow row) BuildRow()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        var row = table.NewRow();
        return (table, row);
    }

    [Fact]
    public void NewRow_Returns_AsyncDataRow()
    {
        var (table, row) = BuildRow();
        row.Should().BeOfType<AsyncDataRow>();
        row.InnerDataRow.Should().NotBeNull();
    }

    [Fact]
    public void Indexer_Returns_Value_By_ColumnName()
    {
        var (_, row) = BuildRow();
        row.InnerDataRow["Id"] = 42;
        row["Id"].Should().Be(42);
    }

    [Fact]
    public void Indexer_Returns_Value_By_ColumnIndex()
    {
        var (_, row) = BuildRow();
        row.InnerDataRow["Id"] = 7;
        row[0].Should().Be(7);
    }

    [Fact]
    public async Task SetValueAsync_By_ColumnName_Mutates_Row()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        await row.SetValueAsync("Name", "Alice");

        row["Name"].Should().Be("Alice");
        row.RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public async Task SetValueAsync_By_ColumnIndex_Mutates_Row()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        await row.SetValueAsync(1, "Bob");

        row[1].Should().Be("Bob");
    }

    [Fact]
    public async Task SetValueAsync_By_DataColumn_Mutates_Row()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        var col = table.Columns["Name"]!;
        await row.SetValueAsync(col, "Carol");

        row["Name"].Should().Be("Carol");
    }

    [Fact]
    public async Task SetValueAsync_Fires_ColumnChangingAsync_Before_Mutation()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        row.InnerDataRow["Name"] = "Before";
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        string? capturedName = null;
        table.ColumnChangingAsync += (args, ct) =>
        {
            capturedName = (string?)row["Name"]; // still "Before" at this point
            return ValueTask.CompletedTask;
        };

        await row.SetValueAsync("Name", "After");

        capturedName.Should().Be("Before");
        row["Name"].Should().Be("After");
    }

    [Fact]
    public async Task SetValueAsync_Fires_ColumnChangedAsync_After_Mutation()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        string? capturedName = null;
        table.ColumnChangedAsync += (args, ct) =>
        {
            capturedName = (string?)row["Name"];
            return ValueTask.CompletedTask;
        };

        await row.SetValueAsync("Name", "Alice");

        capturedName.Should().Be("Alice");
    }

    [Fact]
    public async Task DeleteAsync_Sets_RowState_To_Deleted()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        await row.DeleteAsync();

        row.RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_Fires_RowDeletingAsync_And_RowDeletedAsync()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        var deletingFired = false;
        var deletedFired = false;
        table.RowDeletingAsync += (_, _) => { deletingFired = true; return ValueTask.CompletedTask; };
        table.RowDeletedAsync += (_, _) => { deletedFired = true; return ValueTask.CompletedTask; };

        await row.DeleteAsync();

        deletingFired.Should().BeTrue();
        deletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task AcceptChangesAsync_Accepts_Pending_Changes()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        row.InnerDataRow["Name"] = "Alice";
        table.InnerDataTable.Rows.Add(row.InnerDataRow);

        await row.AcceptChangesAsync();

        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task RejectChangesAsync_Reverts_Pending_Changes()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        row.InnerDataRow["Name"] = "Alice";
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        await row.SetValueAsync("Name", "Rejected");
        await row.RejectChangesAsync();

        row["Name"].Should().Be("Alice");
        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task SetValueAsync_Respects_CancellationToken()
    {
        var (table, row) = BuildRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        table.InnerDataTable.AcceptChanges();

        using var cts = new CancellationTokenSource();
        table.ColumnChangingAsync += (_, ct) => { cts.Cancel(); return ValueTask.CompletedTask; };

        Func<Task> act = async () => await row.SetValueAsync("Name", "X", cts.Token);

        // Handler fires and cancels; subsequent awaits in the chain throw
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowTests" -c Release
```

Expected: FAIL — `AsyncDataRow` does not exist yet.

**Step 3: Create AsyncDataRow**

Create `src/System.Data.Async.DataSet/AsyncDataRow.cs`:

```csharp
using ZeroAlloc.AsyncEvents;

namespace System.Data.Async.DataSet;

public sealed class AsyncDataRow
{
    private readonly DataRow _inner;
    private readonly AsyncDataTable _table;

    internal AsyncDataRow(DataRow inner, AsyncDataTable table)
    {
        _inner = inner;
        _table = table;
    }

    public DataRow InnerDataRow => _inner;
    public DataRowState RowState => _inner.RowState;
    public bool HasErrors => _inner.HasErrors;
    public string RowError => _inner.RowError;
    public bool HasVersion(DataRowVersion version) => _inner.HasVersion(version);

    // Getter-only indexers
    public object this[string columnName] => _inner[columnName];
    public object this[int columnIndex] => _inner[columnIndex];
    public object this[DataColumn column] => _inner[column];
    public object this[string columnName, DataRowVersion version] => _inner[columnName, version];
    public object this[int columnIndex, DataRowVersion version] => _inner[columnIndex, version];
    public object this[DataColumn column, DataRowVersion version] => _inner[column, version];

    public ValueTask SetValueAsync(string columnName, object? value, CancellationToken cancellationToken = default)
    {
        var column = _inner.Table.Columns[columnName]
            ?? throw new ArgumentException($"Column '{columnName}' not found.", nameof(columnName));
        return SetValueCoreAsync(column, value, cancellationToken);
    }

    public ValueTask SetValueAsync(int columnIndex, object? value, CancellationToken cancellationToken = default)
    {
        var column = _inner.Table.Columns[columnIndex];
        return SetValueCoreAsync(column, value, cancellationToken);
    }

    public ValueTask SetValueAsync(DataColumn column, object? value, CancellationToken cancellationToken = default)
        => SetValueCoreAsync(column, value, cancellationToken);

    private async ValueTask SetValueCoreAsync(DataColumn column, object? value, CancellationToken cancellationToken)
    {
        var rowArgs = new DataRowChangeEventArgs(_inner, DataRowAction.Change);
        var colArgs = new DataColumnChangeEventArgs(_inner, column, value);

        await _table._rowChanging.InvokeAsync(rowArgs, cancellationToken).ConfigureAwait(false);
        await _table._columnChanging.InvokeAsync(colArgs, cancellationToken).ConfigureAwait(false);
        _inner[column] = value ?? DBNull.Value;
        await _table._columnChanged.InvokeAsync(colArgs, cancellationToken).ConfigureAwait(false);
        await _table._rowChanged.InvokeAsync(rowArgs, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(CancellationToken cancellationToken = default)
    {
        var args = new DataRowChangeEventArgs(_inner, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.Delete();
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask BeginEditAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.BeginEdit();
        return ValueTask.CompletedTask;
    }

    public async ValueTask EndEditAsync(CancellationToken cancellationToken = default)
    {
        var args = new DataRowChangeEventArgs(_inner, DataRowAction.Change);
        await _table._rowChanging.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.EndEdit();
        await _table._rowChanged.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask CancelEditAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.CancelEdit();
        return ValueTask.CompletedTask;
    }

    public ValueTask AcceptChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.AcceptChanges();
        return ValueTask.CompletedTask;
    }

    public ValueTask RejectChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.RejectChanges();
        return ValueTask.CompletedTask;
    }
}
```

Note: `AsyncDataRow` references `_table._rowChanging` etc. — these `internal` fields are added to `AsyncDataTable` in Task 4. The project will not build until Task 4 is complete; that is expected.

**Step 4: Run tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowTests" -c Release
```

Expected: FAIL (compile error) until `AsyncDataTable` is updated in Task 4. Proceed to Task 3.

---

### Task 3: Create AsyncDataRowCollection

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`
- Create: `tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionTests.cs`

**Step 1: Write the failing tests**

Create `tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionTests.cs`:

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowCollectionTests
{
    private static AsyncDataTable BuildTable()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        return table;
    }

    [Fact]
    public async Task AddAsync_With_Values_Adds_Row()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Id"].Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public async Task AddAsync_With_AsyncDataRow_Adds_Row()
    {
        var table = BuildTable();
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 2;
        row.InnerDataRow["Name"] = "Bob";
        await table.Rows.AddAsync(row);
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task Indexer_Returns_AsyncDataRow()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.Rows[0].Should().BeOfType<AsyncDataRow>();
    }

    [Fact]
    public async Task Count_Reflects_Added_Rows()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "A"]);
        await table.Rows.AddAsync([2, "B"]);
        table.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Count_Includes_Deleted_Rows()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "A"]);
        table.AcceptChanges();
        await table.Rows[0].DeleteAsync();
        table.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_Removes_Row()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        var row = table.Rows[0];

        await table.Rows.RemoveAsync(row);

        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAtAsync_Removes_Row_By_Index()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);
        table.AcceptChanges();

        await table.Rows.RemoveAtAsync(0);

        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Id"].Should().Be(2);
    }

    [Fact]
    public async Task AddAsync_Fires_TableNewRowAsync_And_RowChangedAsync()
    {
        var table = BuildTable();
        var newRowFired = false;
        var rowChangedFired = false;
        table.TableNewRowAsync += (_, _) => { newRowFired = true; return ValueTask.CompletedTask; };
        table.RowChangedAsync += (_, _) => { rowChangedFired = true; return ValueTask.CompletedTask; };

        await table.Rows.AddAsync([1, "Alice"]);

        newRowFired.Should().BeTrue();
        rowChangedFired.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_Fires_RowDeletingAsync_And_RowDeletedAsync()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        var row = table.Rows[0];

        var deletingFired = false;
        var deletedFired = false;
        table.RowDeletingAsync += (_, _) => { deletingFired = true; return ValueTask.CompletedTask; };
        table.RowDeletedAsync += (_, _) => { deletedFired = true; return ValueTask.CompletedTask; };

        await table.Rows.RemoveAsync(row);

        deletingFired.Should().BeTrue();
        deletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task Enumerate_Returns_All_Rows_As_AsyncDataRow()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);

        var names = new List<string>();
        foreach (var row in table.Rows)
        {
            names.Add((string)row["Name"]);
        }

        names.Should().Equal("Alice", "Bob");
    }
}
```

**Step 2: Run to verify they fail**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowCollectionTests" -c Release
```

Expected: FAIL — compile errors until Task 4 wires everything up.

**Step 3: Create AsyncDataRowCollection**

Create `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`:

```csharp
using System.Collections;

namespace System.Data.Async.DataSet;

public sealed class AsyncDataRowCollection : IEnumerable<AsyncDataRow>
{
    private readonly DataRowCollection _inner;
    private readonly AsyncDataTable _table;

    internal AsyncDataRowCollection(DataRowCollection inner, AsyncDataTable table)
    {
        _inner = inner;
        _table = table;
    }

    public int Count => _inner.Count;
    public bool Contains(object key) => _inner.Contains(key);

    public AsyncDataRow this[int index] => new(_inner[index], _table);

    public async ValueTask AddAsync(AsyncDataRow row, CancellationToken cancellationToken = default)
    {
        _inner.Add(row.InnerDataRow);
        await _table._tableNewRow.InvokeAsync(new DataTableNewRowEventArgs(row.InnerDataRow), cancellationToken).ConfigureAwait(false);
        await _table._rowChanged.InvokeAsync(new DataRowChangeEventArgs(row.InnerDataRow, DataRowAction.Add), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AddAsync(object?[] values, CancellationToken cancellationToken = default)
    {
        var row = _table.InnerDataTable.NewRow();
        for (int i = 0; i < values.Length; i++)
        {
            row[i] = values[i] ?? DBNull.Value;
        }
        _inner.Add(row);
        await _table._tableNewRow.InvokeAsync(new DataTableNewRowEventArgs(row), cancellationToken).ConfigureAwait(false);
        await _table._rowChanged.InvokeAsync(new DataRowChangeEventArgs(row, DataRowAction.Add), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(AsyncDataRow row, CancellationToken cancellationToken = default)
    {
        var args = new DataRowChangeEventArgs(row.InnerDataRow, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.Remove(row.InnerDataRow);
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAtAsync(int index, CancellationToken cancellationToken = default)
    {
        var innerRow = _inner[index];
        var args = new DataRowChangeEventArgs(innerRow, DataRowAction.Delete);
        await _table._rowDeleting.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
        _inner.RemoveAt(index);
        await _table._rowDeleted.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public IEnumerator<AsyncDataRow> GetEnumerator()
    {
        for (int i = 0; i < _inner.Count; i++)
        {
            yield return new AsyncDataRow(_inner[i], _table);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

---

### Task 4: Update AsyncDataTable — wire up new types and add async events

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`

**Step 1: Replace the file with the updated version**

Replace the entire contents of `src/System.Data.Async.DataSet/AsyncDataTable.cs` with the following. Key changes from the current file:
- Add `using ZeroAlloc.AsyncEvents;`
- Add 9 `internal AsyncEventHandler<T>` fields
- Add `private readonly AsyncDataRowCollection _rows` field, initialised in every constructor
- Change `public DataRowCollection Rows` → `public AsyncDataRowCollection Rows`
- Change `public DataRow NewRow()` → `public AsyncDataRow NewRow()`
- Remove `public void Clear()` and `public void AcceptChanges()` — replace with async + keep sync
- Add 9 public async event properties
- Add `ClearAsync` method

```csharp
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;
using ZeroAlloc.AsyncEvents;

namespace System.Data.Async.DataSet;

public class AsyncDataTable : IDisposable
{
    private readonly DataTable _inner;
    private readonly AsyncDataRowCollection _rows;

    // Internal async event backing fields — accessed by AsyncDataRow and AsyncDataRowCollection
    internal AsyncEventHandler<DataColumnChangeEventArgs> _columnChanging = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataColumnChangeEventArgs> _columnChanged = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowChanging = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowChanged = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowDeleting = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataRowChangeEventArgs> _rowDeleted = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableClearEventArgs> _tableClearing = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableClearEventArgs> _tableCleared = new(InvokeMode.Sequential);
    internal AsyncEventHandler<DataTableNewRowEventArgs> _tableNewRow = new(InvokeMode.Sequential);

    public AsyncDataTable()
    {
        _inner = new DataTable();
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(string tableName)
    {
        _inner = new DataTable(tableName);
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(string tableName, string tableNamespace)
    {
        _inner = new DataTable(tableName, tableNamespace);
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public AsyncDataTable(DataTable inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _rows = new AsyncDataRowCollection(_inner.Rows, this);
    }

    public DataTable InnerDataTable => _inner;

    // Properties - all delegate to _inner
    public string TableName { get => _inner.TableName; set => _inner.TableName = value; }
    public string Namespace { get => _inner.Namespace; set => _inner.Namespace = value; }
    public string Prefix { get => _inner.Prefix; set => _inner.Prefix = value; }
    public bool CaseSensitive { get => _inner.CaseSensitive; set => _inner.CaseSensitive = value; }
    public CultureInfo Locale { get => _inner.Locale; set => _inner.Locale = value; }
    public string DisplayExpression { get => _inner.DisplayExpression; set => _inner.DisplayExpression = value; }
    public bool HasErrors => _inner.HasErrors;
    public int MinimumCapacity { get => _inner.MinimumCapacity; set => _inner.MinimumCapacity = value; }
    public SerializationFormat RemotingFormat { get => _inner.RemotingFormat; set => _inner.RemotingFormat = value; }
    public bool IsInitialized => _inner.IsInitialized;

    // Collections
    public DataColumnCollection Columns => _inner.Columns;
    public AsyncDataRowCollection Rows => _rows;
    public ConstraintCollection Constraints => _inner.Constraints;
    public DataRelationCollection ParentRelations => _inner.ParentRelations;
    public DataRelationCollection ChildRelations => _inner.ChildRelations;
    public DataView DefaultView => _inner.DefaultView;
    public PropertyCollection ExtendedProperties => _inner.ExtendedProperties;
    public DataColumn[] PrimaryKey { get => _inner.PrimaryKey; set => _inner.PrimaryKey = value; }
    public System.Data.DataSet? DataSet => _inner.DataSet;

    // Methods - all delegate to _inner
    public AsyncDataRow NewRow() => new(_inner.NewRow(), this);
    public void ImportRow(DataRow row) => _inner.ImportRow(row);
    public void AcceptChanges() => _inner.AcceptChanges();
    public void RejectChanges() => _inner.RejectChanges();
    public DataTable? GetChanges() => _inner.GetChanges();
    public DataTable? GetChanges(DataRowState rowStates) => _inner.GetChanges(rowStates);
    public void Clear() => _inner.Clear();
    public DataTable Clone() => _inner.Clone();
    public DataTable Copy() => _inner.Copy();
    public void Merge(DataTable table) => _inner.Merge(table);
    public void Merge(DataTable table, bool preserveChanges) => _inner.Merge(table, preserveChanges);

    public void Merge(DataTable table, bool preserveChanges, MissingSchemaAction missingSchemaAction)
        => _inner.Merge(table, preserveChanges, missingSchemaAction);

    public DataRow[] Select() => _inner.Select();
    public DataRow[] Select(string? filterExpression) => _inner.Select(filterExpression);
    public DataRow[] Select(string? filterExpression, string? sort) => _inner.Select(filterExpression, sort);

    public DataRow[] Select(string? filterExpression, string? sort, DataViewRowState recordStates)
        => _inner.Select(filterExpression, sort, recordStates);

    public DataRow LoadDataRow(object?[] values, bool fAcceptChanges) => _inner.LoadDataRow(values, fAcceptChanges);
    public DataRow LoadDataRow(object?[] values, LoadOption loadOption) => _inner.LoadDataRow(values, loadOption);
    public object Compute(string? expression, string? filter) => _inner.Compute(expression, filter);
    public void BeginInit() => _inner.BeginInit();
    public void EndInit() => _inner.EndInit();
    public void BeginLoadData() => _inner.BeginLoadData();
    public void EndLoadData() => _inner.EndLoadData();
    public DataRow[] GetErrors() => _inner.GetErrors();
    public void Reset() => _inner.Reset();

    // Async table-level mutations
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _tableClearing.InvokeAsync(new DataTableClearEventArgs(_inner), cancellationToken).ConfigureAwait(false);
        _inner.Clear();
        await _tableCleared.InvokeAsync(new DataTableClearEventArgs(_inner), cancellationToken).ConfigureAwait(false);
    }

    public ValueTask AcceptChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inner.AcceptChanges();
        return ValueTask.CompletedTask;
    }

    // Sync I/O - delegate to _inner
    public void Load(IDataReader reader) => _inner.Load(reader);
    public void Load(IDataReader reader, LoadOption loadOption) => _inner.Load(reader, loadOption);
    public XmlReadMode ReadXml(Stream stream) => _inner.ReadXml(stream);
    public void ReadXmlSchema(Stream stream) => _inner.ReadXmlSchema(stream);
    public void WriteXml(Stream stream) => _inner.WriteXml(stream);
    public void WriteXml(Stream stream, XmlWriteMode mode) => _inner.WriteXml(stream, mode);
    public void WriteXmlSchema(Stream stream) => _inner.WriteXmlSchema(stream);

    // Async XML I/O
    public ValueTask ReadXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Note: DataTable.ReadXml does not support async I/O internally.
        // This method provides API consistency but executes synchronously.
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        _inner.ReadXml(reader);
        return default;
    }

    public async ValueTask WriteXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true });
        await using (writer.ConfigureAwait(false))
        {
            _inner.WriteXml(writer);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    public ValueTask ReadXmlSchemaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Note: DataTable.ReadXmlSchema does not support async I/O internally.
        // This method provides API consistency but executes synchronously.
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        _inner.ReadXmlSchema(reader);
        return default;
    }

    public async ValueTask WriteXmlSchemaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true });
        await using (writer.ConfigureAwait(false))
        {
            _inner.WriteXmlSchema(writer);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    // Async loading from IAsyncDataReader
    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, CancellationToken cancellationToken = default)
    {
        return await LoadAsync(reader, LoadOption.OverwriteChanges, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, LoadOption loadOption, CancellationToken cancellationToken = default)
    {
        // Build columns from schema if table has no columns yet
        if (_inner.Columns.Count == 0)
        {
            var schemaTable = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);
            if (schemaTable != null)
            {
                foreach (DataRow schemaRow in schemaTable.Rows)
                {
                    var columnName = (string)schemaRow["ColumnName"];
                    var dataType = (Type)schemaRow["DataType"];
                    if (!_inner.Columns.Contains(columnName))
                    {
                        _inner.Columns.Add(columnName, dataType);
                    }
                }
            }
        }

        int count = 0;
        _inner.BeginLoadData();
        try
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                _inner.LoadDataRow(values, loadOption);
                count++;
            }
        }
        finally
        {
            _inner.EndLoadData();
        }

        return count;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
    }

    // Implicit conversion
    public static implicit operator DataTable(AsyncDataTable asyncTable) => asyncTable._inner;

    // Sync events (preserved — sync subscribers continue working via inner DataTable)
    public event DataColumnChangeEventHandler? ColumnChanged
    {
        add => _inner.ColumnChanged += value;
        remove => _inner.ColumnChanged -= value;
    }

    public event DataColumnChangeEventHandler? ColumnChanging
    {
        add => _inner.ColumnChanging += value;
        remove => _inner.ColumnChanging -= value;
    }

    public event DataRowChangeEventHandler? RowChanged
    {
        add => _inner.RowChanged += value;
        remove => _inner.RowChanged -= value;
    }

    public event DataRowChangeEventHandler? RowChanging
    {
        add => _inner.RowChanging += value;
        remove => _inner.RowChanging -= value;
    }

    public event DataRowChangeEventHandler? RowDeleted
    {
        add => _inner.RowDeleted += value;
        remove => _inner.RowDeleted -= value;
    }

    public event DataRowChangeEventHandler? RowDeleting
    {
        add => _inner.RowDeleting += value;
        remove => _inner.RowDeleting -= value;
    }

    public event DataTableClearEventHandler? TableCleared
    {
        add => _inner.TableCleared += value;
        remove => _inner.TableCleared -= value;
    }

    public event DataTableClearEventHandler? TableClearing
    {
        add => _inner.TableClearing += value;
        remove => _inner.TableClearing -= value;
    }

    public event DataTableNewRowEventHandler? TableNewRow
    {
        add => _inner.TableNewRow += value;
        remove => _inner.TableNewRow -= value;
    }

    // Async events
    public event AsyncEvent<DataColumnChangeEventArgs> ColumnChangingAsync
    {
        add => _columnChanging.Register(value);
        remove => _columnChanging.Unregister(value);
    }

    public event AsyncEvent<DataColumnChangeEventArgs> ColumnChangedAsync
    {
        add => _columnChanged.Register(value);
        remove => _columnChanged.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowChangingAsync
    {
        add => _rowChanging.Register(value);
        remove => _rowChanging.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowChangedAsync
    {
        add => _rowChanged.Register(value);
        remove => _rowChanged.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowDeletingAsync
    {
        add => _rowDeleting.Register(value);
        remove => _rowDeleting.Unregister(value);
    }

    public event AsyncEvent<DataRowChangeEventArgs> RowDeletedAsync
    {
        add => _rowDeleted.Register(value);
        remove => _rowDeleted.Unregister(value);
    }

    public event AsyncEvent<DataTableClearEventArgs> TableClearingAsync
    {
        add => _tableClearing.Register(value);
        remove => _tableClearing.Unregister(value);
    }

    public event AsyncEvent<DataTableClearEventArgs> TableClearedAsync
    {
        add => _tableCleared.Register(value);
        remove => _tableCleared.Unregister(value);
    }

    public event AsyncEvent<DataTableNewRowEventArgs> TableNewRowAsync
    {
        add => _tableNewRow.Register(value);
        remove => _tableNewRow.Unregister(value);
    }
}
```

**Step 2: Build the DataSet project**

```bash
dotnet build src/System.Data.Async.DataSet -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

**Step 3: Run the new unit tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowTests|AsyncDataRowCollectionTests" -c Release
```

Expected: All pass.

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataRow.cs \
        src/System.Data.Async.DataSet/AsyncDataRowCollection.cs \
        src/System.Data.Async.DataSet/AsyncDataTable.cs \
        tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs \
        tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionTests.cs
git commit -m "feat: add AsyncDataRow, AsyncDataRowCollection and async events to AsyncDataTable"
```

---

### Task 5: Fix AsyncDataTableConverterTests

These tests break because `table.Rows` is now `AsyncDataRowCollection` (no `Add(params object[])`) and `row["col"] = value` is a compile error.

**Files:**
- Modify: `tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataTableConverterTests.cs`

**Step 1: Run tests to see current failures**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests -c Release 2>&1 | grep -E "error|FAILED"
```

Expected: Multiple compile errors for `Rows.Add(...)` and `row["col"] = ...` assignments.

**Step 2: Apply the migration pattern**

For every test in `AsyncDataTableConverterTests.cs`:

1. Change `[Fact]\npublic void` → `[Fact]\npublic async Task`
2. Change `table.Rows.Add(v1, v2)` → `await table.Rows.AddAsync([v1, v2])`
3. Change `table.Rows[i]["Col"] = value` → `await table.Rows[i].SetValueAsync("Col", value)`
4. Change `table.Rows[i].Delete()` → `await table.Rows[i].DeleteAsync()`

The full updated file:

```csharp
using System.Data.Async.Converters;
using System.Data.Async.DataSet;

using FluentAssertions;

using Newtonsoft.Json;

using Xunit;

namespace System.Data.Async.DataSet.Tests.Converters;

public class AsyncDataTableConverterTests
{
    private static JsonSerializerSettings CreateSettings()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());
        return settings;
    }

    [Fact]
    public async Task Should_Roundtrip_Simple_Table()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);
        table.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["Name"].Should().Be("Bob");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task Should_Handle_Modified_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        await table.Rows[0].SetValueAsync("Name", "Alicia");

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Modified);
        result.Rows[0]["Name"].Should().Be("Alicia");
        result.Rows[0]["Name", DataRowVersion.Original].Should().Be("Alice");
    }

    [Fact]
    public async Task Should_Handle_Added_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
        result.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public async Task Should_Handle_Deleted_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        await table.Rows[0].DeleteAsync();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task Should_Handle_DBNull()
    {
        var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, DBNull.Value]);
        table.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task Should_Handle_Decimal_Precision()
    {
        var table = new AsyncDataTable("Test");
        table.Columns.Add("Amount", typeof(decimal));
        await table.Rows.AddAsync([123.456789012345678901234567890m]);
        table.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        ((decimal)result.Rows[0]["Amount"]).Should().Be(123.456789012345678901234567890m);
    }

    [Fact]
    public async Task Should_Handle_Constraints()
    {
        var table = new AsyncDataTable("Users");
        var idCol = table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.PrimaryKey = [idCol];
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.PrimaryKey.Should().HaveCount(1);
        result.PrimaryKey[0].ColumnName.Should().Be("Id");
    }

    [Fact]
    public void Should_Handle_Empty_Table()
    {
        var table = new AsyncDataTable("Empty");
        table.Columns.Add("Id", typeof(int));

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.TableName.Should().Be("Empty");
        result.Columns.Count.Should().Be(1);
        result.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task Should_Handle_Multiple_DataTypes()
    {
        var table = new AsyncDataTable("Types");
        table.Columns.Add("Int", typeof(int));
        table.Columns.Add("String", typeof(string));
        table.Columns.Add("Bool", typeof(bool));
        table.Columns.Add("Double", typeof(double));
        table.Columns.Add("DateTime", typeof(DateTime));
        table.Columns.Add("Long", typeof(long));

        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        await table.Rows.AddAsync([42, "hello", true, 3.14, dt, 9876543210L]);
        table.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Int"].Should().Be(42);
        result.Rows[0]["String"].Should().Be("hello");
        result.Rows[0]["Bool"].Should().Be(true);
        result.Rows[0]["Double"].Should().Be(3.14);
        result.Rows[0]["Long"].Should().Be(9876543210L);
    }

    [Fact]
    public void Should_Handle_Null_Value()
    {
        var settings = CreateSettings();
        var result = JsonConvert.DeserializeObject<AsyncDataTable>("null", settings);
        result.Should().BeNull();
    }

    [Fact]
    public void Should_Serialize_Null_Value()
    {
        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject((AsyncDataTable?)null, settings);
        json.Should().Be("null");
    }

    [Fact]
    public void Should_Preserve_Table_Properties()
    {
        var table = new AsyncDataTable("Test");
        table.CaseSensitive = true;
        table.MinimumCapacity = 100;
        table.Namespace = "http://test.com";
        table.Prefix = "t";
        table.Columns.Add("Id", typeof(int));

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.CaseSensitive.Should().BeTrue();
        result.MinimumCapacity.Should().Be(100);
        result.Namespace.Should().Be("http://test.com");
        result.Prefix.Should().Be("t");
    }

    [Fact]
    public async Task Should_Handle_Mixed_Row_States()
    {
        var table = new AsyncDataTable("Mixed");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        await table.Rows.AddAsync([1, "Unchanged"]);
        await table.Rows.AddAsync([2, "ToModify"]);
        await table.Rows.AddAsync([3, "ToDelete"]);
        table.AcceptChanges();

        await table.Rows[1].SetValueAsync("Name", "Modified");
        await table.Rows[2].DeleteAsync();
        await table.Rows.AddAsync([4, "Added"]);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows.Count.Should().Be(4);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
        result.Rows[2].RowState.Should().Be(DataRowState.Deleted);
        result.Rows[3].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public async Task Should_Serialize_Proposed_Version_When_Row_In_BeginEdit()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Original"]);
        table.AcceptChanges();
        table.Rows[0].InnerDataRow.BeginEdit();
        table.Rows[0].InnerDataRow["Name"] = "Proposed";
        // EndEdit NOT called — row has DataRowVersion.Proposed

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        json.Should().Contain("Proposed");
        json.Should().NotContain("\"Original\"");

        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;
        result.Rows[0]["Name"].Should().Be("Proposed");
    }

    [Fact]
    public void Should_Deserialize_Detached_RowState_As_Added()
    {
        var intTypeName = typeof(int).AssemblyQualifiedName!;
        var json = $$"""
            {
              "CaseSensitive": false,
              "DisplayExpression": "",
              "Locale": "",
              "MinimumCapacity": 50,
              "Namespace": "",
              "Prefix": "",
              "RemotingFormat": 0,
              "TableName": "T",
              "Columns": [
                {
                  "AllowDBNull": true,
                  "AutoIncrement": false,
                  "AutoIncrementSeed": 0,
                  "AutoIncrementStep": 1,
                  "Caption": "Id",
                  "ColumnMapping": 1,
                  "ColumnName": "Id",
                  "DataType": "{{intTypeName}}",
                  "DefaultValue": null,
                  "Expression": "",
                  "ExtendedProperties": [],
                  "MaxLength": -1,
                  "Namespace": "",
                  "Prefix": "",
                  "ReadOnly": false
                }
              ],
              "Constraints": [],
              "Rows": [
                {
                  "OriginalRow": null,
                  "Id": 1,
                  "RowState": 64
                }
              ]
            }
            """;

        var settings = CreateSettings();
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Id"].Should().Be(1);
        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Should_Handle_Column_Properties()
    {
        var table = new AsyncDataTable("Test");
        var col = table.Columns.Add("Id", typeof(int));
        col.AutoIncrement = true;
        col.AutoIncrementSeed = 10;
        col.AutoIncrementStep = 5;
        col.Caption = "Identifier";
        col.AllowDBNull = false;

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        var resultCol = result.Columns["Id"]!;
        resultCol.AutoIncrement.Should().BeTrue();
        resultCol.AutoIncrementSeed.Should().Be(10);
        resultCol.AutoIncrementStep.Should().Be(5);
        resultCol.Caption.Should().Be("Identifier");
        resultCol.AllowDBNull.Should().BeFalse();
    }
}
```

**Step 3: Run DataSet.Tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests -c Release
```

Expected: All pass.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataTableConverterTests.cs
git commit -m "test: migrate AsyncDataTableConverterTests to async row mutation API"
```

---

### Task 6: Fix integration tests

**Files:**
- Modify: `tests/System.Data.Async.Integration.Tests/NewtonsoftJsonCrossCompatibilityTests.cs`
- Modify: `tests/System.Data.Async.Integration.Tests/SystemTextJsonCrossCompatibilityTests.cs`

**Step 1: Run integration tests to see failures**

```bash
dotnet test tests/System.Data.Async.Integration.Tests -c Release 2>&1 | grep -E "error|Failed"
```

Expected: Compile errors on all tests that use `AsyncDataTable` with sync row mutations.

**Step 2: Migration pattern for integration tests**

The tests in both files that construct `AsyncDataTable` directly (not wrapping a raw `DataTable`) and call `table.Rows.Add(...)` or `table.Rows[i]["col"] = value` need the same migration as Task 5:

1. `[Fact] public void` → `[Fact] public async Task`
2. `table.Rows.Add(v1, v2)` → `await table.Rows.AddAsync([v1, v2])`
3. `table.Rows[i]["Col"] = value` → `await table.Rows[i].SetValueAsync("Col", value)`
4. `table.Rows[i].Delete()` → `await table.Rows[i].DeleteAsync()`

Tests that only read from `AsyncDataTable` (e.g., `result.Rows[0]["Name"].Should().Be(...)`) need no change — the getter-only indexer compiles fine.

Apply the migration to every affected test in both files. The pattern is mechanical: find each line that writes through `AsyncDataTable.Rows` and replace it.

**Step 3: Run integration tests**

```bash
dotnet test tests/System.Data.Async.Integration.Tests -c Release
```

Expected: All 35 tests pass.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Integration.Tests/NewtonsoftJsonCrossCompatibilityTests.cs \
        tests/System.Data.Async.Integration.Tests/SystemTextJsonCrossCompatibilityTests.cs
git commit -m "test: migrate integration tests to async row mutation API"
```

---

### Task 7: Run full suite

**Step 1: Build entire solution**

```bash
dotnet build -c Release
```

Expected: `0 Warning(s) 0 Error(s)`

**Step 2: Run all tests**

```bash
dotnet test --no-build -c Release
```

Expected: All tests pass (249 existing + new `AsyncDataRowTests` + `AsyncDataRowCollectionTests`).

**Step 3: Commit**

No additional commit needed if all tests pass from prior commits. If any remaining compile errors exist in other test projects (e.g., `Adapters.Tests`, `Validation.Tests`), apply the same migration pattern and commit.

---

### Task 8: Update README and solution file

**Files:**
- Modify: `README.md`
- Modify: `System.Data.Async.slnx` (no change needed — new source files auto-discovered within existing projects)

**Step 1: Update README**

In the **Design Decisions** section of `README.md`, add a row to the design decisions table:

```markdown
- **Async events via `ZeroAlloc.AsyncEvents`** -- `AsyncDataTable` exposes 9 async events (`RowChangedAsync`, `ColumnChangingAsync`, etc.) backed by zero-allocation `AsyncEventHandler<T>` structs. Row mutations go through `AsyncDataRow.SetValueAsync` / `DeleteAsync` / `AcceptChangesAsync`, forcing callers onto the async path. Sync events are preserved for backward-compatible consumers.
```

Also add a usage example under the `### Fill an AsyncDataTable` section:

```markdown
### Subscribe to async row events

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Orders");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Status", typeof(string));

// Subscribe to async events
table.RowChangedAsync += async (args, ct) =>
{
    await NotifyDownstreamAsync(args.Row, args.Action, ct);
};

// Add a row — fires TableNewRowAsync and RowChangedAsync
var row = table.NewRow();
row.InnerDataRow["Id"] = 1;
await table.Rows.AddAsync(row);

// Mutate — fires RowChangingAsync, ColumnChangingAsync, ColumnChangedAsync, RowChangedAsync
await table.Rows[0].SetValueAsync("Status", "Shipped");
```
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: document async events API in README"
```

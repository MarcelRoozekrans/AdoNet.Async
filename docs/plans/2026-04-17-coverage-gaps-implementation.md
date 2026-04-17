# Coverage Gaps Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add missing unit tests for delegated `AsyncDataTable` methods and three new Docusaurus cookbook pages covering XML I/O, Merge/row-versioning, and Compute/constraints/relations.

**Architecture:** One new test file (`AsyncDataTableAdvancedTests.cs`) in the existing DataSet test project; three new Markdown files under `website/docs/cookbook/`. No production source changes.

**Tech Stack:** xunit 2.x, FluentAssertions 8.x, `System.Data.Async.DataSet`, Docusaurus 3.x Markdown with frontmatter.

---

### Task 1: Create the test file with Merge tests

**Files:**
- Create: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs`

**Step 1: Create the file with Merge tests**

```csharp
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableAdvancedTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AsyncDataTable MakeTable(string name, params (string col, Type type)[] columns)
    {
        var t = new AsyncDataTable(name);
        foreach (var (col, type) in columns)
            t.Columns.Add(col, type);
        return t;
    }

    // ------------------------------------------------------------------
    // Merge
    // ------------------------------------------------------------------

    [Fact]
    public async Task Merge_AppendRows_From_Source()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([2, "Bob"]);
        await source.AcceptChangesAsync();

        target.Merge(source);

        target.Rows.Count.Should().Be(2);
        target.Rows[1]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task Merge_PreserveChanges_True_Keeps_Target_Values()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        target.PrimaryKey = [target.Columns["Id"]!];
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();
        target.Rows[0]["Name"] = "AliceModified";   // pending change

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([1, "AliceFromSource"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: true);

        target.Rows[0]["Name"].Should().Be("AliceModified");
    }

    [Fact]
    public async Task Merge_PreserveChanges_False_Overwrites_With_Source()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        target.PrimaryKey = [target.Columns["Id"]!];
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();
        target.Rows[0]["Name"] = "AliceModified";   // pending change

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([1, "AliceFromSource"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false);

        target.Rows[0]["Name"].Should().Be("AliceFromSource");
    }

    [Fact]
    public async Task Merge_MissingSchemaAction_Add_Adds_Missing_Columns()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));
        await target.Rows.AddAsync([1]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Extra", typeof(string)));
        await source.Rows.AddAsync([2, "extra"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false, MissingSchemaAction.Add);

        target.Columns.Contains("Extra").Should().BeTrue();
        target.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Merge_MissingSchemaAction_Ignore_Skips_Missing_Columns()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));
        await target.Rows.AddAsync([1]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Extra", typeof(string)));
        await source.Rows.AddAsync([2, "extra"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false, MissingSchemaAction.Ignore);

        target.Columns.Contains("Extra").Should().BeFalse();
        target.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Merge_Into_Empty_Table_Appends_All_Rows()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));

        using var source = MakeTable("T", ("Id", typeof(int)));
        await source.Rows.AddAsync([1]);
        await source.Rows.AddAsync([2]);
        await source.AcceptChangesAsync();

        target.Merge(source);

        target.Rows.Count.Should().Be(2);
    }
}
```

**Step 2: Run the tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/ --filter "AsyncDataTableAdvancedTests"
```

Expected: all 6 Merge tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs
git commit -m "test: add Merge unit tests for AsyncDataTable"
```

---

### Task 2: Add Compute tests

**Files:**
- Modify: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs`

**Step 1: Append Compute tests to the class (before the closing `}`)**

```csharp
    // ------------------------------------------------------------------
    // Compute
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compute_Sum_Returns_Correct_Total()
    {
        using var t = MakeTable("T", ("Price", typeof(decimal)));
        await t.Rows.AddAsync([10m]);
        await t.Rows.AddAsync([20m]);
        await t.Rows.AddAsync([30m]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Sum(Price)", null);

        result.Should().Be(60m);
    }

    [Fact]
    public async Task Compute_Count_Returns_Row_Count()
    {
        using var t = MakeTable("T", ("Id", typeof(int)));
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([2]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Count(Id)", null);

        Convert.ToInt32(result).Should().Be(2);
    }

    [Fact]
    public async Task Compute_With_Filter_Counts_Matching_Rows()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Active", typeof(bool)));
        await t.Rows.AddAsync([1, true]);
        await t.Rows.AddAsync([2, false]);
        await t.Rows.AddAsync([3, true]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Count(Id)", "Active = true");

        Convert.ToInt32(result).Should().Be(2);
    }

    [Fact]
    public async Task Compute_Min_And_Max()
    {
        using var t = MakeTable("T", ("Score", typeof(int)));
        await t.Rows.AddAsync([5]);
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([9]);
        await t.AcceptChangesAsync();

        t.Compute("Min(Score)", null).Should().Be(1);
        t.Compute("Max(Score)", null).Should().Be(9);
    }

    [Fact]
    public async Task Compute_On_Empty_Table_Returns_DBNull()
    {
        using var t = MakeTable("T", ("Price", typeof(decimal)));
        await t.AcceptChangesAsync();

        var result = t.Compute("Sum(Price)", null);

        result.Should().Be(DBNull.Value);
    }
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/ --filter "AsyncDataTableAdvancedTests"
```

Expected: all Compute tests pass.

**Step 3: Commit**

```bash
git commit -am "test: add Compute unit tests for AsyncDataTable"
```

---

### Task 3: Add LoadDataRow tests

**Files:**
- Modify: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs`

**Step 1: Append LoadDataRow tests to the class**

```csharp
    // ------------------------------------------------------------------
    // LoadDataRow
    // ------------------------------------------------------------------

    [Fact]
    public void LoadDataRow_Bool_True_Accepts_Row()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];

        var row = t.LoadDataRow([1, "Alice"], fAcceptChanges: true);

        t.Rows.Count.Should().Be(1);
        row.RowState.Should().Be(DataRowState.Unchanged);
        row["Name"].Should().Be("Alice");
    }

    [Fact]
    public void LoadDataRow_Bool_False_Leaves_Row_Added()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];

        var row = t.LoadDataRow([1, "Alice"], fAcceptChanges: false);

        row.RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void LoadDataRow_Upsert_Updates_Existing_Row()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);

        t.LoadDataRow([1, "AliceUpdated"], LoadOption.Upsert);

        t.Rows.Count.Should().Be(1);
        t.Rows[0]["Name"].Should().Be("AliceUpdated");
    }

    [Fact]
    public void LoadDataRow_OverwriteChanges_Overwrites_Current_And_Original()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);
        t.Rows[0]["Name"] = "AliceEdited";  // pending change

        t.LoadDataRow([1, "AliceOverwrite"], LoadOption.OverwriteChanges);

        t.Rows[0]["Name", DataRowVersion.Current].Should().Be("AliceOverwrite");
        t.Rows[0]["Name", DataRowVersion.Original].Should().Be("AliceOverwrite");
    }

    [Fact]
    public void LoadDataRow_PreserveChanges_Keeps_Current_Updates_Original()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);
        t.Rows[0]["Name"] = "AliceEdited";  // pending change

        t.LoadDataRow([1, "AlicePreserved"], LoadOption.PreserveChanges);

        t.Rows[0]["Name", DataRowVersion.Current].Should().Be("AliceEdited");
        t.Rows[0]["Name", DataRowVersion.Original].Should().Be("AlicePreserved");
    }
```

**Step 2: Run tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/ --filter "AsyncDataTableAdvancedTests"
```

Expected: all LoadDataRow tests pass.

**Step 3: Commit**

```bash
git commit -am "test: add LoadDataRow unit tests for AsyncDataTable"
```

---

### Task 4: Add BeginInit/EndInit, BeginLoadData/EndLoadData, Reset tests

**Files:**
- Modify: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableAdvancedTests.cs`

**Step 1: Append remaining tests to the class**

```csharp
    // ------------------------------------------------------------------
    // BeginInit / EndInit
    // ------------------------------------------------------------------

    [Fact]
    public void BeginInit_And_EndInit_Do_Not_Throw()
    {
        using var t = new AsyncDataTable("T");

        var act = () =>
        {
            t.BeginInit();
            t.Columns.Add("Id", typeof(int));
            t.EndInit();
        };

        act.Should().NotThrow();
        t.Columns.Contains("Id").Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // BeginLoadData / EndLoadData
    // ------------------------------------------------------------------

    [Fact]
    public async Task BeginLoadData_And_EndLoadData_Do_Not_Throw()
    {
        using var t = MakeTable("T", ("Id", typeof(int)));

        t.BeginLoadData();
        await t.Rows.AddAsync([1]);
        t.EndLoadData();

        t.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task BeginLoadData_Suppresses_RowChanged_Events()
    {
        using var t = MakeTable("T", ("Id", typeof(int)));
        var eventFired = false;
        t.RowChanged += (_, _) => eventFired = true;

        t.BeginLoadData();
        await t.Rows.AddAsync([1]);
        await t.AcceptChangesAsync();
        t.EndLoadData();

        // RowChanged is a sync DataTable event — suppressed during BeginLoadData
        eventFired.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Reset
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reset_Clears_Rows_And_Columns()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await t.Rows.AddAsync([1, "Alice"]);
        await t.AcceptChangesAsync();

        t.Reset();

        t.Rows.Count.Should().Be(0);
        t.Columns.Count.Should().Be(0);
    }

    [Fact]
    public void Reset_On_Empty_Table_Does_Not_Throw()
    {
        using var t = new AsyncDataTable("T");

        var act = () => t.Reset();

        act.Should().NotThrow();
    }
```

**Step 2: Run all advanced tests**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/ --filter "AsyncDataTableAdvancedTests"
```

Expected: all tests pass.

**Step 3: Run full test suite**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/
```

Expected: all tests pass (existing 109 + new tests).

**Step 4: Commit**

```bash
git commit -am "test: add BeginInit, BeginLoadData, Reset unit tests for AsyncDataTable"
```

---

### Task 5: Add XML I/O cookbook page

**Files:**
- Create: `website/docs/cookbook/xml-io.md`

**Step 1: Create the file**

```markdown
---
sidebar_position: 5
title: XML I/O
---

# XML I/O

`AsyncDataTable` and `AsyncDataSet` expose async versions of the standard ADO.NET XML methods. Use them to persist and restore table data without blocking the thread pool.

## Write a Table to XML

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Orders");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Product", typeof(string));
table.Columns.Add("Total", typeof(decimal));

await table.Rows.AddAsync([1, "Widget", 19.99m]);
await table.Rows.AddAsync([2, "Gadget", 49.99m]);
await table.AcceptChangesAsync();

// Write XML to a file
await using var stream = File.Create("orders.xml");
await table.WriteXmlAsync(stream);
```

## Read a Table from XML

```csharp
using var table = new AsyncDataTable();

await using var stream = File.OpenRead("orders.xml");
await table.ReadXmlAsync(stream);

Console.WriteLine(table.Rows.Count); // 2
Console.WriteLine(table.Rows[0]["Product"]); // Widget
```

## Round-Trip via MemoryStream

```csharp
using var ms = new MemoryStream();
await table.WriteXmlAsync(ms);

ms.Position = 0;
using var restored = new AsyncDataTable();
await restored.ReadXmlAsync(ms);
```

## Write and Read the Schema Only

Use `WriteXmlSchemaAsync` / `ReadXmlSchemaAsync` to persist column definitions independently of the data:

```csharp
// Write schema
await using var schemaStream = File.Create("orders.xsd");
await table.WriteXmlSchemaAsync(schemaStream);

// Read schema into a new empty table
using var emptyTable = new AsyncDataTable();
await using var readSchema = File.OpenRead("orders.xsd");
await emptyTable.ReadXmlSchemaAsync(readSchema);

Console.WriteLine(emptyTable.Columns.Count); // 3 (Id, Product, Total)
Console.WriteLine(emptyTable.Rows.Count);    // 0
```

## Write an Entire DataSet

`AsyncDataSet` exposes the same methods and writes all tables in one document:

```csharp
using var ds = new AsyncDataSet("Store");
ds.Tables.Add(ordersTable.InnerDataTable);
ds.Tables.Add(customersTable.InnerDataTable);

await using var stream = File.Create("store.xml");
await ds.WriteXmlAsync(stream);
```

:::tip
Always position the stream back to `0` before reading if you wrote to a `MemoryStream` in the same scope.
:::
```

**Step 2: Commit**

```bash
git add website/docs/cookbook/xml-io.md
git commit -m "docs: add XML I/O cookbook page"
```

---

### Task 6: Add Merge and row-versioning cookbook page

**Files:**
- Create: `website/docs/cookbook/merge-and-row-versioning.md`

**Step 1: Create the file**

```markdown
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
target.Rows[0]["Name"] = "Widget (edited)";

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
row["Name"] = "Widget Pro";

var afterEdit = row["Name", DataRowVersion.Current];   // "Widget Pro"
var before    = row["Name", DataRowVersion.Original];  // "Widget"
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
```

**Step 2: Commit**

```bash
git add website/docs/cookbook/merge-and-row-versioning.md
git commit -m "docs: add Merge and row versioning cookbook page"
```

---

### Task 7: Add Compute, constraints, and relations cookbook page

**Files:**
- Create: `website/docs/cookbook/compute-constraints-relations.md`

**Step 1: Create the file**

```markdown
---
sidebar_position: 7
title: Compute, Constraints & Relations
---

# Compute, Constraints & Relations

## Compute Aggregate Expressions

`AsyncDataTable.Compute()` evaluates an aggregate expression over the rows (optionally filtered). It delegates directly to the underlying `DataTable` — no async overhead needed.

```csharp
using System.Data.Async.DataSet;

var table = new AsyncDataTable("Orders");
table.Columns.Add("CustomerId", typeof(int));
table.Columns.Add("Total", typeof(decimal));

await table.Rows.AddAsync([1, 100m]);
await table.Rows.AddAsync([1, 200m]);
await table.Rows.AddAsync([2, 50m]);
await table.AcceptChangesAsync();

// Sum all orders
var total = (decimal)table.Compute("Sum(Total)", null);       // 350

// Average order total for customer 1
var avg = (decimal)table.Compute("Avg(Total)", "CustomerId = 1"); // 150

// Count rows for customer 2
var count = Convert.ToInt32(table.Compute("Count(Total)", "CustomerId = 2")); // 1
```

Supported aggregate functions: `Sum`, `Avg`, `Min`, `Max`, `Count`, `StDev`, `Var`.

The filter string uses the same syntax as `DataTable.Select()` — column names, comparison operators, `AND`/`OR`, and string literals in single quotes.

---

## Adding Constraints Manually

### UniqueConstraint

```csharp
using System.Data;

var table = new AsyncDataTable("Users");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Email", typeof(string));

// Add a unique constraint on Email
var uc = new UniqueConstraint("UQ_Email", table.Columns["Email"]!);
table.InnerDataTable.Constraints.Add(uc);
```

### ForeignKeyConstraint

```csharp
using var ds = new AsyncDataSet("Store");

var orders = new AsyncDataTable("Orders");
orders.Columns.Add("OrderId", typeof(int));
orders.Columns.Add("CustomerId", typeof(int));
orders.PrimaryKey = [orders.Columns["OrderId"]!];

var customers = new AsyncDataTable("Customers");
customers.Columns.Add("CustomerId", typeof(int));
customers.PrimaryKey = [customers.Columns["CustomerId"]!];

ds.Tables.Add(orders.InnerDataTable);
ds.Tables.Add(customers.InnerDataTable);

var fk = new ForeignKeyConstraint(
    "FK_Orders_Customers",
    customers.Columns["CustomerId"]!,
    orders.Columns["CustomerId"]!);
fk.UpdateRule = Rule.Cascade;
fk.DeleteRule = Rule.SetNull;

orders.InnerDataTable.Constraints.Add(fk);
```

:::note
Constraints live on the inner `DataTable`. Access them via `table.InnerDataTable.Constraints`.
:::

---

## Adding Relations

A `DataRelation` links a parent table's column to a child table's column and enables parent→child and child→parent navigation.

```csharp
using var ds = new AsyncDataSet("Store");
ds.Tables.Add(customers.InnerDataTable);
ds.Tables.Add(orders.InnerDataTable);

// Add the relation
var relation = new DataRelation(
    "Customers_Orders",
    customers.Columns["CustomerId"]!,
    orders.Columns["CustomerId"]!);

ds.Relations.Add(relation);
```

### Navigate parent → children

```csharp
var customerRow = customers.Rows[0];

// GetChildRows returns plain DataRow[] — wrap in AsyncDataRow if needed
var childRows = customerRow.InnerDataRow.GetChildRows("Customers_Orders");
foreach (var child in childRows)
    Console.WriteLine(child["OrderId"]);
```

### Navigate child → parent

```csharp
var orderRow = orders.Rows[0];
var parent = orderRow.InnerDataRow.GetParentRow("Customers_Orders");
Console.WriteLine(parent?["Email"]);
```

:::tip
For typed DataSets generated from `.xsd` files, the generator emits strongly-typed `GetChildRows` and parent-row accessors — you rarely need to call these manually. See the [Typed Datasets](../typed-datasets/overview.md) section.
:::
```

**Step 2: Commit**

```bash
git add website/docs/cookbook/compute-constraints-relations.md
git commit -m "docs: add Compute, constraints, and relations cookbook page"
```

---

### Task 8: Build & verify, then push

**Step 1: Run the full test suite**

```bash
dotnet test tests/System.Data.Async.DataSet.Tests/
dotnet test tests/System.Data.Async.DataSet.Generator.Tests/
```

Expected: all tests pass (no regressions).

**Step 2: Check the Docusaurus site builds**

```bash
cd website && npm run build 2>&1 | tail -20
```

Expected: `Generated static files in "build".` with no errors.

**Step 3: Push and open PR**

```bash
git push -u origin feat/coverage-gaps
gh pr create --title "feat: fill test and docs coverage gaps" --body "$(cat <<'EOF'
## Summary

- Add `AsyncDataTableAdvancedTests` covering `Merge`, `Compute`, `LoadDataRow`, `BeginInit/EndInit`, `BeginLoadData/EndLoadData`, and `Reset`
- Add three new Docusaurus cookbook pages: XML I/O, Merge & row versioning, Compute/constraints/relations

## Test plan

- [ ] All DataSet tests pass
- [ ] All Generator tests pass
- [ ] Docusaurus build succeeds (`npm run build` in `website/`)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

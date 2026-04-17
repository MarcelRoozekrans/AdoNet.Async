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

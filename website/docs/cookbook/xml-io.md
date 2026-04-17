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

---
sidebar_position: 1
title: Overview
---

# Typed DataSets Overview

The **AdoNet.Async.DataSet.Generator** is a Roslyn source generator that reads `.xsd` schema files at build time and produces strongly-typed, async-first wrappers around `DataSet`, `DataTable`, `DataRow`, and row-change event args. You get compile-time safety, full IntelliSense, and an API that follows the same async patterns as the rest of the AdoNet.Async library -- all without writing any boilerplate.

## The Problem: Untyped Access

Working with raw ADO.NET `DataSet` / `DataRow` objects means casting everywhere, using magic strings for column names, and getting zero help from the compiler:

```csharp
// Untyped -- compiles fine, but "Naem" won't fail until runtime
var name = (string)row["Naem"];
row["OrderDate"] = DateTime.Now;            // no async, no safety
var total = (decimal)row["Total"];           // wrong cast? runtime boom
```

Every column access is a potential `InvalidCastException` or `ArgumentException` waiting to happen at runtime.

## The Solution: Generated Typed Wrappers

With the source generator, the same code becomes:

```csharp
// Typed -- compiler catches typos, wrong types, and missing columns
string name = row.Name;                                 // property, not indexer
await row.SetOrderDateAsync(DateTime.Now);              // async setter
decimal total = row.Total;                              // correct type guaranteed
```

Misspell a column name? The build fails. Use the wrong type? The build fails. Forget that a column is nullable? The compiler tells you.

## Generated Type Hierarchy

For each table defined in your `.xsd` file, the generator produces four classes. Given a DataSet named `OrdersDS` with a table named `Customer`:

| Generated Class | Base Class | Purpose |
|---|---|---|
| `AsyncOrdersDS` | `AsyncDataSet` | Typed DataSet with table and relation properties |
| `AsyncCustomerDataTable` | `AsyncDataTable<AsyncCustomerRow>` | Typed table with column properties, `FindBy`, `AddRowAsync` |
| `AsyncCustomerRow` | `AsyncDataRow` | Typed row with property getters and async setters |
| `AsyncCustomerRowChangeEvent` | *(none)* | Typed event args for row-change notifications |

The naming convention is consistent: every generated class is prefixed with `Async`.

## Generic Base Classes

The generated table classes inherit from `AsyncDataTable<TRow>`, which provides:

- **`Rows`** -- an `AsyncDataRowCollection<TRow>` that supports `AddAsync`, `RemoveAsync`, `RemoveAtAsync`, typed enumeration, and `Contains`
- **`WrapRow`** -- an abstract method that the generator overrides to produce the correct typed row from a raw `DataRow`
- **Typed indexer** -- `table[0]` returns `AsyncCustomerRow`, not `AsyncDataRow`
- **Typed events** -- `RowChangedAsync`, `RowChangingAsync` with typed event args

The `AsyncDataRow` base class provides indexers, `SetValueAsync`, `BeginEditAsync` / `EndEditAsync` / `CancelEditAsync`, `DeleteAsync`, `AcceptChangesAsync`, `RejectChangesAsync`, and `IsNull` -- all the building blocks the generated row classes use internally.

## What You Get

### Compile-Time Column Safety

Every column in your schema becomes a strongly-typed property. Typos and type mismatches are caught at build time, not at runtime.

### IntelliSense Everywhere

Table properties, column properties, `FindBy` methods, relation navigation methods -- everything shows up in your IDE's autocomplete with correct types and documentation.

### Typed Relation Navigation

Foreign key relationships defined in your `.xsd` produce navigation methods:

```csharp
// Parent to children
AsyncOrderRow[] orders = customer.GetOrderRows();

// Child to parent
AsyncCustomerRow? customer = order.CustomerRow;
```

### Typed Events

Row-change events carry the correct row type:

```csharp
ds.Customer.RowChangedAsync += (args, ct) =>
{
    AsyncCustomerRow row = args.Row;   // no cast needed
    return ValueTask.CompletedTask;
};
```

### Async Setters

All value mutations go through async methods, consistent with the rest of AdoNet.Async:

```csharp
await row.SetNameAsync("Alice");
await row.SetEmailNullAsync();
```

### Null-Value Handling

Nullable columns get `IsXxxNull()` and `SetXxxNullAsync()` methods. You can control behavior when reading a null value -- throw, return null, return empty, or substitute a replacement -- via [XSD annotations](./annotations-reference.md).

### Partial Classes for Extension

Every generated class is declared `partial`, so you can add custom methods, validation logic, or computed properties in a separate file without touching generated code.

## Next Steps

- [Generating from XSD](./generating-from-xsd.md) -- step-by-step project setup
- [Typed Access](./typed-access.md) -- property getters, async setters, null handling, `FindBy`, `AddRowAsync`
- [Relations](./relations.md) -- parent/child navigation, FK-aware row creation
- [Annotations Reference](./annotations-reference.md) -- full reference of all supported XSD annotations

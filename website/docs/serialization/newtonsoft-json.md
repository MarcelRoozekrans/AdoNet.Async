---
sidebar_position: 1
title: Newtonsoft.Json
---

# Newtonsoft.Json Serialization

The `AdoNet.Async.Serialization.NewtonsoftJson` package provides JSON converters for `AsyncDataTable` and `AsyncDataSet` that are **wire-compatible** with the [Json.Net.DataSetConverters](https://github.com/nickvdyck/Json.Net.DataSetConverters) format. This means existing JSON produced by `Json.Net.DataSetConverters` can be deserialized directly into `AsyncDataTable` and `AsyncDataSet`, and vice versa.

## Installation

```bash
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson
```

This package depends on:
- `AdoNet.Async.DataSet`
- `Newtonsoft.Json` (13.x)

## Converter Registration

Register the converters on your `JsonSerializerSettings`:

```csharp
using System.Data.Async.Converters;
using Newtonsoft.Json;

var settings = new JsonSerializerSettings();
settings.Converters.Add(new AsyncDataTableConverter());
settings.Converters.Add(new AsyncDataSetConverter());
```

| Converter | Handles |
|---|---|
| `AsyncDataTableConverter` | `AsyncDataTable` |
| `AsyncDataSetConverter` | `AsyncDataSet` |

Both converters live in the `System.Data.Async.Converters` namespace.

## Serializing an AsyncDataTable

```csharp
using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using Newtonsoft.Json;

var table = new AsyncDataTable("Products");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));

await table.Rows.AddAsync([1, "Widget", 9.99m]);
await table.Rows.AddAsync([2, "Gadget", 19.99m]);
await table.AcceptChangesAsync();

var settings = new JsonSerializerSettings();
settings.Converters.Add(new AsyncDataTableConverter());

string json = JsonConvert.SerializeObject(table, Formatting.Indented, settings);
```

## Deserializing an AsyncDataTable

```csharp
var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);

foreach (AsyncDataRow row in restored!.Rows)
{
    Console.WriteLine($"{row["Id"]}: {row["Name"]} - ${row["Price"]}");
}
```

## Serializing an AsyncDataSet

```csharp
using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using Newtonsoft.Json;

var ds = new AsyncDataSet("OrderSystem");

var customers = new AsyncDataTable("Customers");
customers.Columns.Add("CustomerId", typeof(int));
customers.Columns.Add("Name", typeof(string));
customers.PrimaryKey = [customers.Columns["CustomerId"]!];
ds.Tables.Add(customers);

var orders = new AsyncDataTable("Orders");
orders.Columns.Add("OrderId", typeof(int));
orders.Columns.Add("CustomerId", typeof(int));
orders.Columns.Add("Total", typeof(decimal));
ds.Tables.Add(orders);

// Add a relation
ds.Relations.Add("FK_Customer_Orders",
    customers.Columns["CustomerId"]!,
    orders.Columns["CustomerId"]!);

await customers.Rows.AddAsync([1, "Alice"]);
await orders.Rows.AddAsync([100, 1, 49.99m]);
await customers.AcceptChangesAsync();
await orders.AcceptChangesAsync();

var settings = new JsonSerializerSettings();
settings.Converters.Add(new AsyncDataSetConverter());
settings.Converters.Add(new AsyncDataTableConverter());

string json = JsonConvert.SerializeObject(ds, Formatting.Indented, settings);
```

## Deserializing an AsyncDataSet

```csharp
var restored = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings);

foreach (AsyncDataTable table in restored!.Tables)
{
    Console.WriteLine($"Table: {table.TableName}, Rows: {table.Rows.Count}");
}
```

## Wire Format

The JSON wire format preserves the full fidelity of `DataTable` and `DataSet`, including:

- **Table metadata** -- `TableName`, `CaseSensitive`, `Locale`, `Namespace`, `Prefix`, `MinimumCapacity`, `RemotingFormat`, `DisplayExpression`
- **Column definitions** -- `ColumnName`, `DataType` (assembly-qualified), `AllowDBNull`, `AutoIncrement`/`Seed`/`Step`, `Caption`, `ColumnMapping`, `DateTimeMode`, `DefaultValue`, `Expression`, `ExtendedProperties`, `MaxLength`, `ReadOnly`
- **Constraints** -- `UniqueConstraint` with column names, constraint name, and `IsPrimaryKey`
- **Relations** (DataSet) -- `RelationName`, parent/child tables and columns, `Nested`, `ParentKeyConstraint`, `ChildKeyConstraint` (with `AcceptRejectRule`, `DeleteRule`, `UpdateRule`)
- **Extended properties** -- on columns, constraints, relations, and the DataSet itself

### Wire Format Compatibility with Json.Net.DataSetConverters

The converters produce JSON that is structurally identical to `Json.Net.DataSetConverters`. This means:

- JSON produced by `Json.Net.DataSetConverters` for `DataTable`/`DataSet` can be deserialized as `AsyncDataTable`/`AsyncDataSet` using these converters.
- JSON produced by these converters can be deserialized by `Json.Net.DataSetConverters` back into `DataTable`/`DataSet`.

:::tip
This wire compatibility enables gradual migration. You can start serializing with `AsyncDataTable` and existing consumers that use `Json.Net.DataSetConverters` will continue to work without changes.
:::

## Row States in JSON

The converters preserve `DataRowState` across serialization round-trips. Each row in the JSON has an `OriginalRow` field and a `RowState` field:

| RowState | OriginalRow | Behavior |
|---|---|---|
| `Unchanged` | `null` | Current values only; row is accepted on deserialize |
| `Added` | `null` | Current values only; row is set to `Added` state on deserialize |
| `Modified` | Object with original values | Both original and current values are written; on deserialize, the original is loaded first and accepted, then current values overwrite |
| `Deleted` | Object with original values | Original values are written; on deserialize, the row is loaded then `Delete()` is called |
| `Detached` | `null` | Serialized like `Added`; deserialized as `Added` |

### Example: Preserving Row States

```csharp
var table = new AsyncDataTable("Items");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));

// Add rows in different states
await table.Rows.AddAsync([1, "Original"]);
await table.AcceptChangesAsync();

// Modify a row
await table.Rows[0].SetValueAsync("Name", "Modified");

// Add a new row (will be in Added state)
await table.Rows.AddAsync([2, "NewItem"]);

// Serialize -- row states are preserved
var settings = new JsonSerializerSettings();
settings.Converters.Add(new AsyncDataTableConverter());
string json = JsonConvert.SerializeObject(table, settings);

// Deserialize -- row states are restored
var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;
// restored.Rows[0].RowState == DataRowState.Modified
// restored.Rows[1].RowState == DataRowState.Added
```

## Typed DataSet Serialization

Typed DataSets (generated from `.xsd` schemas) serialize as their base `AsyncDataTable`/`AsyncDataSet` type. The converters work with the underlying `DataTable`/`DataSet`, so all data and metadata is preserved. When deserializing, you get back an `AsyncDataTable`/`AsyncDataSet` -- not the typed subclass. This is the standard behavior for JSON serialization of DataSets.

```csharp
// Serialize a typed DataSet -- works because it is an AsyncDataSet
string json = JsonConvert.SerializeObject(typedDataSet, settings);

// Deserialize returns AsyncDataSet, not the typed subclass
var restored = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings);
```

:::info
All typed metadata (column types, constraints, relations) is preserved in the JSON. Only the C# class type information is lost -- the data itself is fully intact.
:::

## Supported Data Types

The converters handle all standard `System.Data` column types:

| Type | JSON representation |
|---|---|
| `int`, `long`, `short`, `byte`, `uint`, `ulong`, `sbyte`, `ushort` | Number |
| `float`, `double` | Number |
| `decimal` | String (28 decimal places, lossless) |
| `bool` | Boolean |
| `string` | String |
| `DateTime`, `DateTimeOffset` | String (ISO 8601) |
| `Guid` | String |
| `TimeSpan` | String (invariant) |
| `byte[]` | String (Base64) |
| `DBNull` | `null` |

## Cross-Library Compatibility

JSON produced by the Newtonsoft.Json converters can be deserialized by the [System.Text.Json converters](./system-text-json.md), and vice versa. Both libraries produce the same wire format.

```csharp
// Serialize with Newtonsoft.Json
var newtonsoftSettings = new JsonSerializerSettings();
newtonsoftSettings.Converters.Add(new AsyncDataTableConverter());
string json = JsonConvert.SerializeObject(table, newtonsoftSettings);

// Deserialize with System.Text.Json
var stjOptions = new JsonSerializerOptions();
stjOptions.Converters.Add(new AsyncDataTableJsonConverter());
var restored = JsonSerializer.Deserialize<AsyncDataTable>(json, stjOptions);
```

See the [System.Text.Json](./system-text-json.md) page for details on the System.Text.Json converters.

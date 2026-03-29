---
sidebar_position: 2
title: System.Text.Json
---

# System.Text.Json Serialization

The `AdoNet.Async.Serialization.SystemTextJson` package provides `System.Text.Json` converters for `AsyncDataTable` and `AsyncDataSet`. These converters produce the **same wire format** as the [Newtonsoft.Json converters](./newtonsoft-json.md), enabling full cross-library compatibility.

## Installation

```bash
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

This package depends on:
- `AdoNet.Async.DataSet`
- `System.Text.Json` (included in the .NET SDK)

## Converter Registration

Register the converters on your `JsonSerializerOptions`:

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Text.Json;

var options = new JsonSerializerOptions();
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());
```

| Converter | Handles |
|---|---|
| `AsyncDataTableJsonConverter` | `AsyncDataTable` |
| `AsyncDataSetJsonConverter` | `AsyncDataSet` |

Both converters live in the `System.Data.Async.Converters.SystemTextJson` namespace.

## Serializing an AsyncDataTable

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using System.Text.Json;

var table = new AsyncDataTable("Products");
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));

await table.Rows.AddAsync([1, "Widget", 9.99m]);
await table.Rows.AddAsync([2, "Gadget", 19.99m]);
await table.AcceptChangesAsync();

var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new AsyncDataTableJsonConverter());

string json = JsonSerializer.Serialize(table, options);
```

## Deserializing an AsyncDataTable

```csharp
var restored = JsonSerializer.Deserialize<AsyncDataTable>(json, options);

foreach (AsyncDataRow row in restored!.Rows)
{
    Console.WriteLine($"{row["Id"]}: {row["Name"]} - ${row["Price"]}");
}
```

## Serializing an AsyncDataSet

```csharp
var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());

string json = JsonSerializer.Serialize(dataSet, options);
```

## Deserializing an AsyncDataSet

```csharp
var restored = JsonSerializer.Deserialize<AsyncDataSet>(json, options);

foreach (AsyncDataTable table in restored!.Tables)
{
    Console.WriteLine($"Table: {table.TableName}, Rows: {table.Rows.Count}");
}
```

## Same Wire Format as Newtonsoft.Json

Both serialization libraries produce structurally identical JSON. The wire format includes:

- Table and DataSet metadata (name, locale, case sensitivity, etc.)
- Full column definitions with assembly-qualified type names
- Constraints (unique/primary key)
- Row data with row state preservation (`Unchanged`, `Added`, `Modified`, `Deleted`)
- Relations (in DataSet serialization)
- Extended properties

For a detailed breakdown of the wire format and row state handling, see the [Newtonsoft.Json](./newtonsoft-json.md) page.

## Cross-Library Compatibility

Because the wire format is identical, you can freely mix serializers:

```csharp
using System.Data.Async.Converters;
using System.Data.Async.Converters.SystemTextJson;
using Newtonsoft.Json;
using STJ = System.Text.Json;

// --- Serialize with System.Text.Json, deserialize with Newtonsoft.Json ---

var stjOptions = new STJ.JsonSerializerOptions();
stjOptions.Converters.Add(new AsyncDataTableJsonConverter());
string json = STJ.JsonSerializer.Serialize(table, stjOptions);

var newtonsoftSettings = new JsonSerializerSettings();
newtonsoftSettings.Converters.Add(new AsyncDataTableConverter());
var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, newtonsoftSettings);

// --- Serialize with Newtonsoft.Json, deserialize with System.Text.Json ---

string json2 = JsonConvert.SerializeObject(table, newtonsoftSettings);
var restored2 = STJ.JsonSerializer.Deserialize<AsyncDataTable>(json2, stjOptions);
```

:::tip
This cross-library compatibility is validated by the `SystemTextJsonCrossCompatibilityTests` suite, which verifies that JSON produced by one library round-trips through the other for all row states and data types.
:::

## Performance Comparison

System.Text.Json converters are generally faster and allocate less memory than the Newtonsoft.Json converters, especially for serialization:

| Operation | 10 rows | 100 rows |
|---|---|---|
| STJ Serialize | 10 us / 12 KB | 39 us / 66 KB |
| Newtonsoft Serialize | 12 us / 29 KB | 70 us / 115 KB |
| STJ Deserialize | 55 us / 33 KB | 143 us / 99 KB |
| Newtonsoft Deserialize | 61 us / 34 KB | 165 us / 101 KB |

For detailed benchmark data, see the [Performance](../performance/performance.md) page.

## ASP.NET Core Integration

When using System.Text.Json with ASP.NET Core (the default serializer), configure the converters in `Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new AsyncDataTableJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AsyncDataSetJsonConverter());
    });
```

Or for minimal APIs:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new AsyncDataTableJsonConverter());
    options.SerializerOptions.Converters.Add(new AsyncDataSetJsonConverter());
});
```

See the [JSON API with Typed DataSets](../cookbook/json-api-typed-datasets.md) cookbook for a complete example.

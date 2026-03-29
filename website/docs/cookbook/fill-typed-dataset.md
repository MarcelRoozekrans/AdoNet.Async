---
sidebar_position: 2
title: Fill Typed DataSet
---

# Fill a Typed DataSet

This cookbook walks through a complete end-to-end example: define an XSD schema, generate typed async classes, fill the typed DataSet from a database, navigate typed rows and relations, and serialize to JSON.

## 1. Define the .xsd Schema

Create a file `Schemas/NorthwindDS.xsd`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<xs:schema id="NorthwindDS"
           xmlns:xs="http://www.w3.org/2001/XMLSchema"
           xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
           xmlns:codegen="urn:schemas-microsoft-com:xml-msprop">

  <xs:element name="NorthwindDS" msdata:IsDataSet="true">
    <xs:complexType>
      <xs:choice maxOccurs="unbounded">

        <!-- Customers table -->
        <xs:element name="Customer">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CustomerId" type="xs:int"
                          msdata:AutoIncrement="true"
                          msdata:AutoIncrementSeed="1"
                          msdata:AutoIncrementStep="1" />
              <xs:element name="Name" type="xs:string" />
              <xs:element name="Email" type="xs:string" minOccurs="0"
                          codegen:nullValue="_null" />
              <xs:element name="Region" type="xs:string" minOccurs="0"
                          codegen:nullValue="(Unknown)" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>

        <!-- Orders table -->
        <xs:element name="Order">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="OrderId" type="xs:int"
                          msdata:AutoIncrement="true" />
              <xs:element name="CustomerId" type="xs:int" />
              <xs:element name="OrderDate" type="xs:dateTime" />
              <xs:element name="Total" type="xs:decimal" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>

      </xs:choice>
    </xs:complexType>

    <!-- Primary keys -->
    <xs:unique name="PK_Customer" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Customer" />
      <xs:field xpath="CustomerId" />
    </xs:unique>

    <xs:unique name="PK_Order" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Order" />
      <xs:field xpath="OrderId" />
    </xs:unique>

    <!-- Foreign key: Order.CustomerId -> Customer.CustomerId -->
    <xs:keyref name="FK_Customer_Order" refer="PK_Customer"
               codegen:typedParent="Customer"
               codegen:typedChildren="Orders">
      <xs:selector xpath=".//Order" />
      <xs:field xpath="CustomerId" />
    </xs:keyref>

  </xs:element>
</xs:schema>
```

## 2. Configure the .csproj

Add the source generator package and reference the `.xsd` as an `AdditionalFiles` item:

```xml
<ItemGroup>
  <PackageReference Include="AdoNet.Async" />
  <PackageReference Include="AdoNet.Async.DataSet" />
  <PackageReference Include="AdoNet.Async.Adapters" />
  <PackageReference Include="AdoNet.Async.DataSet.Generator" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="Schemas\NorthwindDS.xsd" />
</ItemGroup>
```

The source generator reads the `.xsd` at compile time and produces:

- `AsyncNorthwindDS` -- typed `AsyncDataSet` subclass
- `AsyncCustomerDataTable` / `AsyncCustomerRow` -- typed table and row for `Customer`
- `AsyncOrderDataTable` / `AsyncOrderRow` -- typed table and row for `Order`
- `AddCustomerRowAsync(...)` / `AddOrderRowAsync(...)` -- typed add methods
- `FindByCustomerId(int)` / `FindByOrderId(int)` -- primary key lookup methods
- `GetOrderRows()` / `CustomerRow` -- FK-aware relation navigation
- `IsEmailNull()`, `SetEmailNull()` -- typed null handling
- `Region` returns `"(Unknown)"` when null (from `codegen:nullValue`)

## 3. Create Connection and Adapter

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

var connection = new SqlConnection(connectionString).AsAsync();
await connection.OpenAsync();

var cmd = connection.CreateCommand();
cmd.CommandText = @"
    SELECT CustomerId, Name, Email, Region FROM Customers;
    SELECT OrderId, CustomerId, OrderDate, Total FROM Orders;
";
```

## 4. FillAsync into the Typed DataSet

```csharp
using System.Data.Async.DataSet;

var ds = new AsyncNorthwindDS();
var adapter = new AdapterDbDataAdapter(cmd);

// FillAsync populates tables in order of result sets.
// For typed DataSets, you typically fill each table individually:
cmd.CommandText = "SELECT CustomerId, Name, Email, Region FROM Customers";
await adapter.FillAsync(ds.Customer);

cmd.CommandText = "SELECT OrderId, CustomerId, OrderDate, Total FROM Orders";
await adapter.FillAsync(ds.Order);
```

:::info
When using `FillAsync` with a typed DataSet, fill each typed table individually rather than filling the DataSet as a whole. This ensures data lands in the correct typed table with the right schema.
:::

## 5. Navigate Typed Rows and Relations

```csharp
// Iterate typed rows -- no casts, no string column names
foreach (AsyncCustomerRow customer in ds.Customer.Rows)
{
    Console.WriteLine($"Customer: {customer.Name}");

    // Nullable column with codegen:nullValue="_null"
    if (!customer.IsEmailNull())
        Console.WriteLine($"  Email: {customer.Email}");

    // Nullable column with codegen:nullValue="(Unknown)" -- returns replacement
    Console.WriteLine($"  Region: {customer.Region}");

    // FK-aware relation navigation
    AsyncOrderRow[] orders = customer.GetOrderRows();
    foreach (AsyncOrderRow order in orders)
    {
        Console.WriteLine($"  Order #{order.OrderId}: {order.Total:C} on {order.OrderDate:d}");

        // Navigate back to parent
        AsyncCustomerRow parent = order.CustomerRow;
        Console.WriteLine($"    Ordered by: {parent.Name}");
    }
}

// Primary key lookup
AsyncCustomerRow? alice = ds.Customer.FindByCustomerId(1);
if (alice is not null)
    Console.WriteLine($"Found: {alice.Name}");
```

## 6. Modify Typed Rows

```csharp
// Add a new customer using the typed add method
AsyncCustomerRow newCustomer = await ds.Customer.AddCustomerRowAsync(
    0, "Bob", "bob@example.com", "West");

// Add an order linked to a parent row (FK-aware)
AsyncOrderRow newOrder = await ds.Order.AddOrderRowAsync(
    newCustomer, DateTime.UtcNow, 149.99m);

// Update a typed property
await newCustomer.SetNameAsync("Robert");
await newCustomer.SetEmailAsync("robert@example.com");

// Set a nullable column to null
await newCustomer.SetEmailNull();
```

## 7. Serialize to JSON

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Text.Json;

var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());

// Typed DataSet serializes as AsyncDataSet (base type)
string json = JsonSerializer.Serialize<AsyncDataSet>(ds, options);
Console.WriteLine(json);
```

:::info
When serializing a typed DataSet, cast or specify the base type `AsyncDataSet` in the serializer call. The JSON will contain all data, column definitions, constraints, and relations. When deserializing, you will get an `AsyncDataSet` (not the typed subclass), but the data is fully preserved.
:::

Or with Newtonsoft.Json:

```csharp
using System.Data.Async.Converters;
using Newtonsoft.Json;

var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
settings.Converters.Add(new AsyncDataTableConverter());
settings.Converters.Add(new AsyncDataSetConverter());

string json = JsonConvert.SerializeObject((AsyncDataSet)ds, settings);
```

## Complete Working Example

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using System.Data.Async.Converters.SystemTextJson;
using System.Text.Json;
using Microsoft.Data.SqlClient;

// 1. Connect
var conn = new SqlConnection("Server=.;Database=Northwind;Trusted_Connection=true").AsAsync();
await conn.OpenAsync();

// 2. Create typed DataSet
var ds = new AsyncNorthwindDS();

// 3. Fill tables
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT CustomerId, Name, Email, Region FROM Customers";
var adapter = new AdapterDbDataAdapter(cmd);
await adapter.FillAsync(ds.Customer);

cmd.CommandText = "SELECT OrderId, CustomerId, OrderDate, Total FROM Orders";
await adapter.FillAsync(ds.Order);

// 4. Navigate
foreach (AsyncCustomerRow customer in ds.Customer.Rows)
{
    var orders = customer.GetOrderRows();
    Console.WriteLine($"{customer.Name}: {orders.Length} orders");
}

// 5. Serialize
var options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());
string json = JsonSerializer.Serialize<AsyncDataSet>(ds, options);
Console.WriteLine(json);

await conn.CloseAsync();
```

## Next Steps

- [Async Validation Events](./async-validation-events.md) -- validate rows with async logic
- [JSON API with Typed DataSets](./json-api-typed-datasets.md) -- serve typed DataSets from ASP.NET Core
- [Newtonsoft.Json Serialization](../serialization/newtonsoft-json.md) -- wire format details

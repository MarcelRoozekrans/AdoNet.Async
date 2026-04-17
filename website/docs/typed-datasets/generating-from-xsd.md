---
sidebar_position: 2
title: Generating from XSD
---

# Generating from XSD

This guide walks through setting up the AdoNet.Async typed DataSet source generator from scratch. By the end, you will have strongly-typed async DataSet classes generated automatically at build time from an XSD schema.

## Prerequisites

- .NET 8.0 or later
- An XSD schema file describing your DataSet

## Step-by-Step Setup

### 1. Add the NuGet packages

You need two packages: the runtime library and the source generator.

```bash
dotnet add package AdoNet.Async.DataSet
dotnet add package AdoNet.Async.DataSet.Generator
```

### 2. Add your .xsd file as AdditionalFiles

The source generator watches for `.xsd` files declared as `AdditionalFiles` in your project. Create a `Schemas` folder (or any location you prefer) and add the following to your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\*.xsd" />
</ItemGroup>
```

You can also reference individual files:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\OrdersDS.xsd" />
</ItemGroup>
```

### 3. Build

Run `dotnet build`. The generator reads every `.xsd` marked as `AdditionalFiles`, parses it, and emits the typed classes into the compilation. No extra build steps, no MSBuild targets -- it just works.

## Full .csproj Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AdoNet.Async.DataSet" Version="1.*" />
    <PackageReference Include="AdoNet.Async.DataSet.Generator" Version="1.*" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="Schemas\*.xsd" />
  </ItemGroup>
</Project>
```

:::info
The generator package is a **development dependency** -- it produces source at compile time and adds no runtime DLLs to your output. Only `AdoNet.Async.DataSet` ships in your published application.
:::

## Example XSD File

Below is a complete schema defining a `Customer`/`Order` DataSet with a foreign key relationship, auto-increment primary keys, nullable columns, and null-value behavior:

```xml
<?xml version="1.0" standalone="yes"?>
<xs:schema id="OrdersDS"
           xmlns:xs="http://www.w3.org/2001/XMLSchema"
           xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
           xmlns:codegen="urn:schemas-microsoft-com:xml-msprop">
  <xs:element name="OrdersDS" msdata:IsDataSet="true" msdata:Locale="en-US">
    <xs:complexType>
      <xs:choice maxOccurs="unbounded">

        <!-- Customer table -->
        <xs:element name="Customer">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CustomerId" type="xs:int" />
              <xs:element name="Name" type="xs:string" />
              <xs:element name="Email" type="xs:string" minOccurs="0" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>

        <!-- Order table -->
        <xs:element name="Order">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="OrderId" type="xs:int"
                          msdata:AutoIncrement="true"
                          msdata:AutoIncrementSeed="1"
                          msdata:AutoIncrementStep="1" />
              <xs:element name="CustomerId" type="xs:int" />
              <xs:element name="OrderDate" type="xs:dateTime" />
              <xs:element name="Total" type="xs:decimal" />
              <xs:element name="Notes" type="xs:string"
                          minOccurs="0"
                          codegen:nullValue="" />
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
    <xs:keyref name="FK_Customer_Order" refer="PK_Customer">
      <xs:selector xpath=".//Order" />
      <xs:field xpath="CustomerId" />
    </xs:keyref>
  </xs:element>
</xs:schema>
```

## What Gets Generated

From the schema above, the generator produces the following source files (visible in `obj/` under the analyzer output):

| Generated File | Class | Description |
|---|---|---|
| `OrdersDS.AsyncDataSet.g.cs` | `AsyncOrdersDS` | Typed DataSet with `Customer` and `Order` table properties, relation accessors, `InitClass`, `Clone` |
| `OrdersDS.Customer.AsyncDataTable.g.cs` | `AsyncOrdersDSCustomerDataTable` | Typed table with `CustomerIdColumn`, `NameColumn`, `EmailColumn`, `FindByCustomerId`, `AddCustomerRowAsync`, `RemoveCustomerRowAsync`, `NewCustomerRow` |
| `OrdersDS.Customer.AsyncDataRow.g.cs` | `AsyncOrdersDSCustomerRow` | Typed row with `CustomerId`, `Name`, `Email` properties, `SetCustomerIdAsync`, `SetNameAsync`, `SetEmailAsync`, `IsEmailNull`, `SetEmailNullAsync`, `GetOrderRows` |
| `OrdersDS.Customer.Events.g.cs` | `AsyncOrdersDSCustomerRowChangeEvent` | Typed event args with `Row` (typed as `AsyncOrdersDSCustomerRow`) and `Action` |
| `OrdersDS.Order.AsyncDataTable.g.cs` | `AsyncOrdersDSOrderDataTable` | Typed table for orders |
| `OrdersDS.Order.AsyncDataRow.g.cs` | `AsyncOrdersDSOrderRow` | Typed row for orders, including `CustomerRow` parent navigation |
| `OrdersDS.Order.Events.g.cs` | `AsyncOrdersDSOrderRowChangeEvent` | Typed event args for orders |

### Naming Conventions

All generated classes include the DataSet name as a prefix to guarantee uniqueness when multiple DataSet schemas share a table name:

- **DataSet**: `Async{DataSetName}` — e.g., `AsyncOrdersDS`
- **DataTable**: `Async{DataSetName}{TableName}DataTable` — e.g., `AsyncOrdersDSCustomerDataTable`
- **DataRow**: `Async{DataSetName}{TableName}Row` — e.g., `AsyncOrdersDSCustomerRow`
- **Event args**: `Async{DataSetName}{TableName}RowChangeEvent` — e.g., `AsyncOrdersDSCustomerRowChangeEvent`

You can override table and row names with the `codegen:typedName` and `codegen:typedPlural` annotations. See [Annotations Reference](./annotations-reference.md).

## Namespace

All generated classes are placed in the `System.Data.Async.DataSet` namespace:

```csharp
using System.Data.Async.DataSet;

var ds = new AsyncOrdersDS();
```

## Partial Classes for Extension

Every generated class is declared `partial`. You can create a companion file to add custom behavior:

```csharp
// CustomerExtensions.cs
namespace System.Data.Async.DataSet;

public partial class AsyncCustomerRow
{
    public string DisplayName => $"{Name} ({Email})";

    public bool HasOrders => GetOrderRows().Length > 0;
}
```

```csharp
// Usage
var customer = ds.Customer.FindByCustomerId(1)!;
Console.WriteLine(customer.DisplayName);    // "Alice (alice@example.com)"
Console.WriteLine(customer.HasOrders);      // true
```

:::tip
Partial class files must use the same namespace (`System.Data.Async.DataSet`) and class name as the generated code.
:::

## xs:attribute Columns

Columns can be declared as `xs:attribute` elements directly in the table's `xs:complexType` rather than as `xs:element` inside an `xs:sequence`. This is common in schemas generated by Visual Studio Dataset Designer. Both styles are fully supported.

| Column style | Nullable indicator |
|---|---|
| `xs:element minOccurs="0"` | nullable |
| `xs:element` | required |
| `xs:attribute` (no `use`) | nullable (default) |
| `xs:attribute use="optional"` | nullable |
| `xs:attribute use="required"` | required |

`xs:field` selectors that reference attribute columns use `xpath="@ColName"`. The generator strips the `@` prefix automatically to match the column name.

See [Annotations Reference — xs:attribute Column Declarations](./annotations-reference.md#xsattribute-column-declarations) for full examples.

## Troubleshooting

### Classes not generated

- Verify that your `.xsd` files are included as `<AdditionalFiles>`, not as `<None>` or `<Content>`.
- The root element must have `msdata:IsDataSet="true"`.
- Check the build output for diagnostic `ADGEN001` (invalid XSD).

### IntelliSense not showing generated types

Some IDEs need a restart or a rebuild after first adding the generator. In Visual Studio, try **Build > Rebuild Solution**. In VS Code with C# DevKit, restart the language server.

### Viewing the generated source

In Visual Studio, expand **Dependencies > Analyzers > AdoNet.Async.DataSet.Generator** in Solution Explorer to see all generated `.g.cs` files. You can also find them on disk under:

```
obj/Debug/net8.0/generated/System.Data.Async.DataSet.Generator/
```

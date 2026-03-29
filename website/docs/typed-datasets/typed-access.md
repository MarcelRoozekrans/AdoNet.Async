---
sidebar_position: 3
title: Typed Access
---

# Typed Access

Once the source generator has produced your typed classes from an [XSD schema](./generating-from-xsd.md), you get a rich, compile-time-safe API for reading, writing, finding, and managing rows. This page covers the full typed access surface.

## Typed Read-Only Properties

Every non-hidden column in your schema becomes a property on the generated row class. The property type is determined by the XSD type mapping (e.g., `xs:int` maps to `int`, `xs:dateTime` maps to `DateTime`, `xs:decimal` maps to `decimal`).

```csharp
using var ds = new AsyncOrdersDS();
var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

// Strongly-typed property access -- no casts, no magic strings
int id = customer.CustomerId;
string name = customer.Name;
```

The properties are read-only. All mutations go through async setters (see below).

### Supported XSD-to-CLR Type Mappings

| XSD Type | CLR Type |
|---|---|
| `xs:string` | `string` |
| `xs:int` | `int` |
| `xs:long`, `xs:integer` | `long` |
| `xs:short` | `short` |
| `xs:boolean` | `bool` |
| `xs:decimal` | `decimal` |
| `xs:double` | `double` |
| `xs:float` | `float` |
| `xs:dateTime`, `xs:date`, `xs:time` | `DateTime` |
| `xs:duration` | `TimeSpan` |
| `xs:base64Binary` | `byte[]` |
| `xs:byte` | `sbyte` |
| `xs:unsignedByte` | `byte` |
| `xs:unsignedShort` | `ushort` |
| `xs:unsignedInt` | `uint` |
| `xs:unsignedLong` | `ulong` |

You can override the CLR type for any column using the `msdata:DataType` annotation (e.g., `msdata:DataType="System.Guid"`). See [Annotations Reference](./annotations-reference.md).

## Typed Async Setters

Every mutable column (not read-only, not an expression, not hidden) gets an async setter method named `Set{ColumnName}Async`:

```csharp
await customer.SetNameAsync("Bob");
await customer.SetEmailAsync("bob@example.com");

// Setters accept a CancellationToken
await customer.SetNameAsync("Charlie", cancellationToken);
```

The async setters call `SetValueAsync` on the base `AsyncDataRow`, which ensures consistent behavior with the rest of the AdoNet.Async library.

:::info
Columns marked with `msdata:ReadOnly="true"` or `msdata:Expression="..."` do not get setter methods. The generator omits them entirely, so attempting to call a non-existent setter is a compile-time error.
:::

## Nullable Columns

Columns with `minOccurs="0"` in the XSD are nullable (they allow `DBNull`). The generator produces two additional methods for each nullable column:

- **`Is{ColumnName}Null()`** -- returns `bool`
- **`Set{ColumnName}NullAsync()`** -- sets the column to `DBNull.Value`

```csharp
var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

// Check and set null
await customer.SetEmailNullAsync();
bool isNull = customer.IsEmailNull();   // true

// Set a value again
await customer.SetEmailAsync("alice@example.com");
customer.IsEmailNull();                  // false
```

### Null-Value Behavior Modes

What happens when you **read** a nullable column that is currently `DBNull`? The generator supports four behaviors, controlled by the `codegen:nullValue` annotation on the column element:

#### 1. Throw (default)

When no `codegen:nullValue` annotation is present, reading a null column throws `StrongTypingException`:

```xml
<xs:element name="Email" type="xs:string" minOccurs="0" />
```

```csharp
// Email is null -- this throws StrongTypingException
string email = customer.Email;  // throws!

// Always check first
if (!customer.IsEmailNull())
{
    string email = customer.Email;  // safe
}
```

#### 2. ReturnNull (`_null`)

The property type becomes nullable and returns `null` when the column is `DBNull`:

```xml
<xs:element name="Description" type="xs:string" minOccurs="0"
            codegen:nullValue="_null" />
```

```csharp
string? description = category.Description;  // null, not an exception
```

#### 3. ReturnEmpty (`_empty` or `""`)

For string columns, returns `""`. For value types, returns `default(T)`:

```xml
<xs:element name="Notes" type="xs:string" minOccurs="0"
            codegen:nullValue="" />
```

```csharp
string notes = order.Notes;  // "" when null, not an exception
```

#### 4. ReplacementValue (literal string)

Returns a specific replacement value when the column is `DBNull`. For strings, the annotation value is used directly. For numeric types, the value is parsed as a literal:

```xml
<xs:element name="Notes" type="xs:string" minOccurs="0"
            codegen:nullValue="N/A" />
```

```csharp
string notes = product.Notes;  // "N/A" when null
```

## Primary Key Lookup

If your schema defines a primary key (via `xs:unique` or `xs:key` with `msdata:PrimaryKey="true"`), the generated table gets a `FindBy` method:

```csharp
using var ds = new AsyncOrdersDS();
await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

// Single-column primary key
AsyncCustomerRow? found = ds.Customer.FindByCustomerId(2);
found!.Name;  // "Bob"

// Returns null when not found
AsyncCustomerRow? missing = ds.Customer.FindByCustomerId(999);
missing.Should().BeNull();
```

The method name is `FindBy{Column1}{Column2}...` -- concatenating all primary key column names.

### Composite Primary Keys

For tables with multi-column primary keys, the `FindBy` method accepts multiple parameters:

```xml
<xs:unique name="PK_SalesOrderLine" msdata:PrimaryKey="true">
  <xs:selector xpath=".//SalesOrderLine" />
  <xs:field xpath="SalesOrderId" />
  <xs:field xpath="SkuId" />
</xs:unique>
```

```csharp
// Composite PK lookup
var line = ds.SalesOrderLine.FindBySalesOrderIdSkuId(1, 1);
line!.Quantity;  // 5
```

## Adding Rows

### AddXxxRowAsync -- Typed Parameters

The generator creates a typed add method that accepts values for all non-hidden, non-auto-increment, non-expression, non-read-only columns. For foreign key columns that have a relation, the method accepts the **parent row** instead of the raw FK value:

```csharp
// CustomerId, Name, Email -- all three columns as parameters
var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

// OrderId is auto-increment, so it is excluded.
// CustomerId is a FK to Customer, so the parameter is the parent row.
// Remaining columns: OrderDate, Total, Notes
var order = await ds.Order.AddOrderRowAsync(
    customer,           // parent row, not raw CustomerId
    DateTime.Now,       // OrderDate
    99.99m,             // Total
    "Rush delivery"     // Notes
);

order.OrderId;  // 1 (auto-assigned)
```

The method creates a new `DataRow`, assigns the values, wraps it as a typed row, adds it via `Rows.AddAsync`, and returns the typed row.

### NewXxxRow -- Manual Construction

For more control, use `NewXxxRow` to create an unattached typed row, set its values, then add it manually:

```csharp
var row = ds.Customer.NewCustomerRow();
await row.SetCustomerIdAsync(1);
await row.SetNameAsync("Alice");
await row.SetEmailAsync("alice@example.com");
await ds.Customer.Rows.AddAsync(row);
```

## Removing Rows

The generated table includes a `RemoveXxxRowAsync` method:

```csharp
var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
ds.Customer.Count;  // 1

await ds.Customer.RemoveCustomerRowAsync(customer);
ds.Customer.Count;  // 0
```

You can also remove by index or via the `Rows` collection directly:

```csharp
await ds.Customer.Rows.RemoveAtAsync(0);
await ds.Customer.Rows.RemoveAsync(customer);
```

## Typed Indexer and Count

The generated table provides a typed indexer and a `Count` property:

```csharp
ds.Customer.Count;            // number of rows
AsyncCustomerRow row = ds.Customer[0];  // typed indexer, no cast
```

## Typed Row Collection

The `Rows` property on each generated table is an `AsyncDataRowCollection<TRow>` that supports:

- **`AddAsync(TRow row, CancellationToken)`** -- add a typed row
- **`RemoveAsync(TRow row, CancellationToken)`** -- remove a specific typed row
- **`RemoveAtAsync(int index, CancellationToken)`** -- remove by index
- **`Contains(object key)`** -- check if a primary key exists
- **`Count`** -- current row count
- **Typed enumeration** -- `foreach (AsyncCustomerRow row in ds.Customer.Rows)`

```csharp
// Typed enumeration
foreach (AsyncCustomerRow row in ds.Customer.Rows)
{
    Console.WriteLine($"{row.CustomerId}: {row.Name}");
}

// Contains by primary key
bool exists = ds.Customer.Rows.Contains(42);
```

## Column Properties

Each generated table exposes its `DataColumn` objects as properties, useful for programmatic access:

```csharp
DataColumn col = ds.Customer.CustomerIdColumn;
DataColumn nameCol = ds.Customer.NameColumn;

// Use with the versioned indexer
object original = row[ds.Customer.NameColumn, DataRowVersion.Original];
```

## Edit Lifecycle

The typed row inherits the full async edit lifecycle from `AsyncDataRow`:

```csharp
var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

// Begin/End edit
await row.BeginEditAsync();
await row.SetNameAsync("Bob");
await row.EndEditAsync();     // commits the change

// Cancel edit
await row.BeginEditAsync();
await row.SetNameAsync("Charlie");
await row.CancelEditAsync();  // reverts to "Bob"

// Row state management
await row.AcceptChangesAsync();   // marks as Unchanged
await row.SetNameAsync("Diana");  // marks as Modified
await row.DeleteAsync();          // marks as Deleted
```

## Next Steps

- [Relations](./relations.md) -- navigate parent/child relationships with typed methods
- [Annotations Reference](./annotations-reference.md) -- control naming, null behavior, auto-increment, and more

---
sidebar_position: 4
title: Relations
---

# Relations

Foreign key relationships defined in your XSD schema are surfaced as typed navigation methods on the generated row classes. This gives you compile-time-safe traversal between parent and child rows without any string-based lookups.

## Defining Relations in XSD

Relations are declared using `xs:keyref` elements that reference a `xs:unique` or `xs:key` constraint. Here is a minimal example with a `Customer` to `Order` one-to-many relationship:

```xml
<!-- Primary key on Customer -->
<xs:unique name="PK_Customer" msdata:PrimaryKey="true">
  <xs:selector xpath=".//Customer" />
  <xs:field xpath="CustomerId" />
</xs:unique>

<!-- Foreign key: Order.CustomerId -> Customer.CustomerId -->
<xs:keyref name="FK_Customer_Order" refer="PK_Customer">
  <xs:selector xpath=".//Order" />
  <xs:field xpath="CustomerId" />
</xs:keyref>
```

This produces a `DataRelation` named `FK_Customer_Order` on the generated `AsyncOrdersDS`, along with navigation methods on both the parent and child row classes.

## Parent-to-Child Navigation

From a parent row, call `Get{ChildTableName}Rows()` to retrieve all related child rows as a typed array:

```csharp
using var ds = new AsyncOrdersDS();
var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Order 1");
await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 49.99m, "Order 2");

// Navigate from parent to children
AsyncOrderRow[] orders = customer.GetOrderRows();
orders.Length;  // 2
```

The method name defaults to `Get{ChildTableName}Rows`. You can customize it with the `codegen:typedChildren` annotation (see [Custom Navigation Names](#custom-navigation-names) below).

## Child-to-Parent Navigation

From a child row, access the parent via a typed property named `{ParentTableName}Row`:

```csharp
var order = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Test");

// Navigate from child to parent
AsyncCustomerRow? parent = order.CustomerRow;
parent!.Name;  // "Alice"
```

The property returns `null` if the foreign key columns are `DBNull` (i.e., no parent is linked).

## Setting the Parent Row

Each child-to-parent relationship also generates an async setter method `Set{ParentTableName}RowAsync`:

```csharp
// Change the parent
var newCustomer = await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");
await order.SetCustomerRowAsync(newCustomer);
order.CustomerRow!.Name;  // "Bob"

// Unlink the parent (sets FK columns to DBNull)
await order.SetCustomerRowAsync(null);
order.CustomerRow;  // null
```

The setter updates the underlying foreign key columns on the child row to match the parent's primary key values, or sets them to `DBNull.Value` when you pass `null`.

## FK-Aware AddRowAsync

When a table has foreign key relations, the generated `Add{TableName}RowAsync` method accepts the **parent row** instead of the raw FK column value. This prevents invalid FK values and makes the relationship explicit:

```csharp
// Instead of: AddOrderRowAsync(customerId: 1, ...)
// You write:  AddOrderRowAsync(customer, ...)
var order = await ds.Order.AddOrderRowAsync(
    customer,           // parent row -- FK is extracted automatically
    DateTime.Now,       // OrderDate
    99.99m,             // Total
    "Rush delivery"     // Notes
);
```

The parent parameter is nullable. When `null` is passed, the FK columns are left unset.

:::tip
Auto-increment columns (like `OrderId` with `msdata:AutoIncrement="true"`) are excluded from the `AddRowAsync` parameters entirely -- the DataTable assigns their values automatically.
:::

## Multiple Relations on a Single Table

A child table can have multiple foreign key relations. For example, a `SalesOrderLine` table might reference both `SalesOrder` and `Sku`:

```xml
<xs:keyref name="FK_SalesOrder_SalesOrderLine" refer="PK_SalesOrder">
  <xs:selector xpath=".//SalesOrderLine" />
  <xs:field xpath="SalesOrderId" />
</xs:keyref>

<xs:keyref name="FK_Sku_SalesOrderLine" refer="PK_Sku">
  <xs:selector xpath=".//SalesOrderLine" />
  <xs:field xpath="SkuId" />
</xs:keyref>
```

This produces:

- On `AsyncSalesOrderRow`: `GetSalesOrderLineRows()` returns child line items
- On `AsyncSkuRow`: `GetSalesOrderLineRows()` returns child line items
- On `AsyncSalesOrderLineRow`: `SalesOrderRow` and `SkuRow` properties for parent navigation
- `AddSalesOrderLineRowAsync` accepts both parent rows:

```csharp
var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);

// Both parent rows as parameters
var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(
    order,      // parent SalesOrder
    sku,        // parent Sku
    5,          // Quantity
    10.00m      // UnitPrice
);

// Navigate in any direction
line.SalesOrderRow!.SalesOrderId;       // 1
line.SkuRow!.Title;                     // "Widget"
order.GetSalesOrderLineRows().Length;   // 1
sku.GetSalesOrderLineRows().Length;     // 1
```

## Relation Accessor on the DataSet

Each relation is also accessible as a property on the generated DataSet class:

```csharp
using var ds = new AsyncOrdersDS();

DataRelation rel = ds.FK_Customer_Order;
rel.RelationName;  // "FK_Customer_Order"
```

This is useful for advanced scenarios like programmatic constraint inspection.

## Custom Navigation Names

By default, navigation methods and properties are named based on the table name:

- Parent-to-child: `Get{ChildTableName}Rows()`
- Child-to-parent: `{ParentTableName}Row`

You can override these names using annotations on the `xs:keyref` element:

### codegen:typedChildren

Overrides the parent-to-child method name:

```xml
<xs:keyref name="FK_Category_Product" refer="PK_Category"
           codegen:typedChildren="GetProducts">
  <xs:selector xpath=".//Product" />
  <xs:field xpath="CategoryId" />
</xs:keyref>
```

```csharp
// Instead of category.GetProductRows()
AsyncProductRow[] products = category.GetProducts();
```

### codegen:typedParent

Overrides the child-to-parent property name (the generator appends `Row`):

```xml
<xs:keyref name="FK_Category_Product" refer="PK_Category"
           codegen:typedParent="Category">
  <xs:selector xpath=".//Product" />
  <xs:field xpath="CategoryId" />
</xs:keyref>
```

```csharp
// Property is named "CategoryRow" (typedParent + "Row")
AsyncCategoryEntryRow? cat = product.CategoryRow;

// Setter is named "SetCategoryRowAsync"
await product.SetCategoryRowAsync(newCategory);
```

:::info
Both `codegen:typedParent` and `codegen:typedChildren` can be used together on the same `xs:keyref`.
:::

## Constraint-Only Relations

Sometimes you want a foreign key constraint (enforcing referential integrity) without creating a `DataRelation` and the associated navigation methods. Use `msdata:ConstraintOnly="true"`:

```xml
<xs:keyref name="FK_Region_Store" refer="PK_Region"
           msdata:ConstraintOnly="true">
  <xs:selector xpath=".//Store" />
  <xs:field xpath="RegionId" />
</xs:keyref>
```

With `ConstraintOnly`, the generator:

- **Does** create a `ForeignKeyConstraint` with the specified update/delete/accept-reject rules
- **Does not** create a `DataRelation`
- **Does not** generate `GetChildRows` or parent row navigation methods
- **Does not** replace the FK column with a parent row parameter in `AddRowAsync`

The FK column appears as a regular typed parameter in the add method instead.

## Nested Relations

The `msdata:IsNested` annotation marks a relation as nested, which affects XML serialization of the DataSet (child elements are serialized inside the parent element):

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:IsNested="true">
  <xs:selector xpath=".//Order" />
  <xs:field xpath="CustomerId" />
</xs:keyref>
```

This sets `DataRelation.Nested = true` on the generated relation. It does not change the navigation API.

## Constraint Rules

Foreign key constraints support `UpdateRule`, `DeleteRule`, and `AcceptRejectRule` via `msdata:` annotations:

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:UpdateRule="Cascade"
           msdata:DeleteRule="SetNull"
           msdata:AcceptRejectRule="None">
  <xs:selector xpath=".//Order" />
  <xs:field xpath="CustomerId" />
</xs:keyref>
```

Valid values match the `System.Data.Rule` enum: `Cascade`, `SetNull`, `SetDefault`, `None`. The `AcceptRejectRule` supports `None` and `Cascade`. Defaults are `Cascade` for update/delete and `None` for accept/reject.

## Next Steps

- [Typed Access](./typed-access.md) -- property getters, async setters, null handling
- [Annotations Reference](./annotations-reference.md) -- full reference of all XSD annotations including relation annotations

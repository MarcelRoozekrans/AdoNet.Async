---
sidebar_position: 5
title: Annotations Reference
---

# Annotations Reference

The AdoNet.Async typed DataSet source generator supports two families of XML annotations on XSD elements: **codegen** annotations (from the `urn:schemas-microsoft-com:xml-msprop` namespace) that control code generation, and **msdata** annotations (from the `urn:schemas-microsoft-com:xml-msdata` namespace) that control DataSet schema behavior.

Declare both namespaces on your root `xs:schema` element:

```xml
<xs:schema id="MyDS"
           xmlns:xs="http://www.w3.org/2001/XMLSchema"
           xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
           xmlns:codegen="urn:schemas-microsoft-com:xml-msprop">
```

---

## codegen: Annotations

These annotations control naming and null-value behavior in the generated C# code.

### codegen:typedName

**Applies to:** table element (`xs:element` inside the DataSet's `xs:choice`)

Overrides the name used for the generated **row** class. Also affects the names of `AddRowAsync`, `RemoveRowAsync`, and `NewRow` methods.

```xml
<xs:element name="Category" codegen:typedName="CategoryEntry">
  ...
</xs:element>
```

**Default:** uses the table element name (e.g., `Category`).

**Effect on generated code:**

| Without annotation | With `codegen:typedName="CategoryEntry"` |
|---|---|
| `AsyncCategoryRow` | `AsyncCategoryEntryRow` |
| `AddCategoryRowAsync(...)` | `AddCategoryEntryRowAsync(...)` |
| `RemoveCategoryRowAsync(...)` | `RemoveCategoryEntryRowAsync(...)` |
| `NewCategoryRow()` | `NewCategoryEntryRow()` |
| `AsyncCategoryRowChangeEvent` | `AsyncCategoryEntryRowChangeEvent` |

---

### codegen:typedPlural

**Applies to:** table element

Overrides the name used for the generated **table** class and the table accessor property on the DataSet.

```xml
<xs:element name="Category" codegen:typedPlural="Categories">
  ...
</xs:element>
```

**Default:** uses the table element name (e.g., `Category`).

**Effect on generated code:**

| Without annotation | With `codegen:typedPlural="Categories"` |
|---|---|
| `AsyncCategoryDataTable` | `AsyncCategoriesDataTable` |
| `ds.Category` | `ds.Categories` |

---

### codegen:typedParent

**Applies to:** `xs:keyref` element

Overrides the name of the child-to-parent **property** on the child row class. The generator appends `Row` to the value.

```xml
<xs:keyref name="FK_Category_Product" refer="PK_Category"
           codegen:typedParent="Category">
  <xs:selector xpath=".//Product" />
  <xs:field xpath="CategoryId" />
</xs:keyref>
```

**Default:** `{ParentTableName}Row` (e.g., `CategoryRow`).

**Effect on generated code:**

| Without annotation | With `codegen:typedParent="Category"` |
|---|---|
| `product.CategoryRow` | `product.CategoryRow` (same in this case) |
| `SetCategoryRowAsync(...)` | `SetCategoryRowAsync(...)` |

This annotation is most useful when a child table has multiple relations to the same parent table, or when the parent table name does not read naturally as a property name.

---

### codegen:typedChildren

**Applies to:** `xs:keyref` element

Overrides the name of the parent-to-child **method** on the parent row class.

```xml
<xs:keyref name="FK_Category_Product" refer="PK_Category"
           codegen:typedChildren="GetProducts">
  <xs:selector xpath=".//Product" />
  <xs:field xpath="CategoryId" />
</xs:keyref>
```

**Default:** `Get{ChildTableName}Rows` (e.g., `GetProductRows`).

**Effect on generated code:**

| Without annotation | With `codegen:typedChildren="GetProducts"` |
|---|---|
| `category.GetProductRows()` | `category.GetProducts()` |

---

### codegen:nullValue

**Applies to:** column element (`xs:element` with `minOccurs="0"` or `xs:attribute` without `use="required"`) that allows `DBNull`

Controls what happens when the property getter is called on a column whose current value is `DBNull`. Has no effect on non-nullable columns.

| Value | Behavior | Generated Property |
|---|---|---|
| *(not set)* | **Throw** -- throws `StrongTypingException` | `public string Email { get { if (IsNull("Email")) throw ...; return ...; } }` |
| `_throw` | Same as not set | Same as above |
| `_null` | **ReturnNull** -- returns `null` | `public string? Email => IsNull("Email") ? null : ...;` |
| `_empty` or `""` | **ReturnEmpty** -- returns `""` for strings, `default(T)` for value types | `public string Email => IsNull("Email") ? "" : ...;` |
| Any other string | **ReplacementValue** -- returns the literal | `public string Email => IsNull("Email") ? "N/A" : ...;` |

**Examples:**

```xml
<!-- Throw on null (default) -->
<xs:element name="Email" type="xs:string" minOccurs="0" />

<!-- Return null -->
<xs:element name="Description" type="xs:string" minOccurs="0"
            codegen:nullValue="_null" />

<!-- Return empty string -->
<xs:element name="Notes" type="xs:string" minOccurs="0"
            codegen:nullValue="" />

<!-- Return replacement value -->
<xs:element name="Notes" type="xs:string" minOccurs="0"
            codegen:nullValue="N/A" />
```

:::warning
The `_null` behavior changes the property return type to nullable (e.g., `string?`). The other behaviors keep the return type non-nullable since they always return a value.
:::

---

## xs:attribute Column Declarations

Columns can be declared either as `xs:element` children inside a `xs:sequence`, or as `xs:attribute` children directly on the table's `xs:complexType`. Both forms generate identical typed DataRow properties and DataTable column fields.

### Nullability

| Declaration | Nullable? |
|---|---|
| `xs:element minOccurs="0"` | Yes |
| `xs:element` (no `minOccurs`) | No |
| `xs:attribute` (no `use`) | Yes (default) |
| `xs:attribute use="optional"` | Yes |
| `xs:attribute use="required"` | No |

### Example — all-attribute table with composite PK

```xml
<xs:element name="Entry">
  <xs:complexType>
    <xs:attribute name="RegionId"    type="xs:int"    use="required" />
    <xs:attribute name="Code"        type="xs:string" use="required" />
    <xs:attribute name="Description" type="xs:string" />
    <xs:attribute name="TrackingId"  msdata:DataType="System.Guid" />
    <xs:attribute name="Tag"         type="xs:string" codegen:nullValue="N/A" />
  </xs:complexType>
</xs:element>
```

`xs:field` selectors that reference attribute columns use the `@ColName` XPath syntax:

```xml
<xs:key name="PK_Entry" msdata:PrimaryKey="true">
  <xs:selector xpath=".//Entry" />
  <xs:field xpath="@RegionId" />
  <xs:field xpath="@Code" />
</xs:key>
```

### Example — mixed xs:element + xs:attribute

A table may freely mix both column declaration forms:

```xml
<xs:element name="Item">
  <xs:complexType>
    <xs:sequence>
      <xs:element name="Id"   type="xs:int"    />
      <xs:element name="Name" type="xs:string" />
    </xs:sequence>
    <xs:attribute name="Source"   type="xs:string" use="required" />
    <xs:attribute name="Priority" type="xs:int" />
  </xs:complexType>
</xs:element>
```

All `codegen:` and `msdata:` column annotations (`nullValue`, `DataType`, `DefaultValue`, `ReadOnly`, etc.) work the same way on `xs:attribute` columns as they do on `xs:element` columns.

### Namespace-prefixed XPath

When the XSD has a `targetNamespace`, XPath selectors use a namespace prefix. The generator strips the prefix automatically from element references in selectors (`mstns:TableName` → `TableName`). Attribute column `xs:field` references use the `@ColName` form without a namespace prefix — the `@` is stripped to match the column name:

```xml
<xs:schema targetNamespace="http://tempuri.org/MyDS.xsd"
           xmlns:mstns="http://tempuri.org/MyDS.xsd" ...>
  ...
  <xs:key name="PK_Entry" msdata:PrimaryKey="true">
    <xs:selector xpath=".//mstns:Entry" />  <!-- mstns: prefix stripped on selector -->
    <xs:field xpath="@RegionId" />          <!-- @ prefix stripped; no mstns: on field -->
  </xs:key>
</xs:schema>
```

---

## msdata: Annotations

These annotations control the schema structure of the underlying `DataSet`, `DataTable`, and `DataColumn` objects.

### msdata:IsDataSet

**Applies to:** root `xs:element`

**Required.** Marks the element as the DataSet root. The generator only processes elements with `msdata:IsDataSet="true"`.

```xml
<xs:element name="OrdersDS" msdata:IsDataSet="true">
  ...
</xs:element>
```

**Effect:** The element's name becomes the DataSet name (e.g., `AsyncOrdersDS`). Its child elements become tables.

---

### msdata:PrimaryKey

**Applies to:** `xs:unique` or `xs:key` element

Marks the constraint as a primary key. This sets `DataTable.PrimaryKey` and enables the `FindBy` method on the generated table class.

```xml
<xs:unique name="PK_Customer" msdata:PrimaryKey="true">
  <xs:selector xpath=".//Customer" />
  <xs:field xpath="CustomerId" />
</xs:unique>
```

**Effect on generated code:**

```csharp
// FindByCustomerId is generated because PK_Customer is a primary key
AsyncCustomerRow? row = ds.Customer.FindByCustomerId(42);
```

Without `msdata:PrimaryKey="true"`, the constraint is treated as a unique constraint only, and no `FindBy` method is generated.

---

### msdata:AutoIncrement

**Applies to:** column element

Marks the column as auto-incrementing. Auto-increment columns are excluded from the `AddRowAsync` parameter list.

```xml
<xs:element name="OrderId" type="xs:int"
            msdata:AutoIncrement="true"
            msdata:AutoIncrementSeed="1"
            msdata:AutoIncrementStep="1" />
```

**Effect:**

- Sets `DataColumn.AutoIncrement = true`
- Sets `DataColumn.AutoIncrementSeed` and `DataColumn.AutoIncrementStep`
- The column is **excluded** from `AddOrderRowAsync` parameters
- A getter property and async setter are still generated

---

### msdata:AutoIncrementSeed

**Applies to:** column element

The starting value for auto-increment. Defaults to `0`.

```xml
<xs:element name="OrderId" type="xs:int"
            msdata:AutoIncrement="true"
            msdata:AutoIncrementSeed="1000" />
```

---

### msdata:AutoIncrementStep

**Applies to:** column element

The increment step value. Defaults to `1`.

```xml
<xs:element name="OrderId" type="xs:int"
            msdata:AutoIncrement="true"
            msdata:AutoIncrementStep="10" />
```

---

### msdata:ReadOnly

**Applies to:** column element

Marks the column as read-only. No async setter method is generated, and the column is excluded from `AddRowAsync` parameters.

```xml
<xs:element name="Discount" type="xs:decimal"
            msdata:ReadOnly="true"
            msdata:DefaultValue="0" />
```

**Effect on generated code:**

- `line.Discount` -- property getter is generated (read-only)
- `SetDiscountAsync` -- **not generated**
- `AddSalesOrderLineRowAsync` -- `Discount` is **excluded** from parameters
- `DataColumn.ReadOnly = true`

---

### msdata:Expression

**Applies to:** column element

Defines a computed column expression. The column becomes read-only: no setter is generated, and the column is excluded from `AddRowAsync` parameters.

```xml
<xs:element name="LineTotal" type="xs:decimal"
            msdata:ReadOnly="true"
            msdata:Expression="Quantity * UnitPrice" />
```

**Effect on generated code:**

```csharp
// Computed automatically -- no setter exists
decimal total = line.LineTotal;   // returns Quantity * UnitPrice

// Expression is set on the DataColumn
ds.SalesOrderLine.LineTotalColumn.Expression;  // "Quantity * UnitPrice"
ds.SalesOrderLine.LineTotalColumn.ReadOnly;    // true
```

:::info
Expression columns should typically also have `msdata:ReadOnly="true"`. While the generator omits setters for expression columns regardless of the ReadOnly flag, setting ReadOnly prevents accidental writes through the untyped DataRow API.
:::

---

### msdata:DefaultValue

**Applies to:** column element

Sets the default value for the column. When a row is added without explicitly setting this column, the default value is used.

```xml
<xs:element name="Price" type="xs:decimal"
            msdata:DefaultValue="0" />
```

**Effect:**

- Sets `DataColumn.DefaultValue`
- The column can still be set explicitly via the async setter

```csharp
var row = ds.Product.NewProductRow();
// Price not set -- defaults to 0
await ds.Product.Rows.AddAsync(row);
row.Price;  // 0m
```

---

### msdata:DataType

**Applies to:** column element

Overrides the CLR type for the column, bypassing the standard XSD-to-CLR type mapping. Useful for types not directly representable in XSD, such as `System.Guid`.

```xml
<xs:element name="Sku" msdata:DataType="System.Guid" minOccurs="0" />
```

**Effect on generated code:**

```csharp
// Property type is Guid, not string
Guid sku = product.Sku;
await product.SetSkuAsync(Guid.NewGuid());
```

The value must be a fully-qualified CLR type name (e.g., `System.Guid`, `System.TimeSpan`, `System.Byte[]`).

---

### msdata:Ordinal

**Applies to:** column element

Sets the column's ordinal position within the DataTable. Parsed but does not directly affect code generation -- the column order in the XSD sequence determines property order.

```xml
<xs:element name="Name" type="xs:string" msdata:Ordinal="1" />
```

---

### msdata:Caption

**Applies to:** column element

Sets `DataColumn.Caption`, which is a user-friendly label for UI binding.

```xml
<xs:element name="CustomerId" type="xs:int" msdata:Caption="Customer ID" />
```

**Effect:**

```csharp
ds.Customer.CustomerIdColumn.Caption;  // "Customer ID"
```

---

### msdata:MaxLength

**Applies to:** column element (typically `xs:string` columns)

Sets `DataColumn.MaxLength`. Note that this is declared using the standard XSD `maxLength` attribute, not the `msdata:` namespace.

```xml
<xs:element name="Name" type="xs:string" maxLength="100" />
```

**Effect:**

```csharp
ds.Customer.NameColumn.MaxLength;  // 100
```

---

### msdata:ConstraintOnly

**Applies to:** `xs:keyref` element

When `true`, creates a `ForeignKeyConstraint` but does **not** create a `DataRelation`. No parent/child navigation methods are generated.

```xml
<xs:keyref name="FK_Region_Store" refer="PK_Region"
           msdata:ConstraintOnly="true">
  <xs:selector xpath=".//Store" />
  <xs:field xpath="RegionId" />
</xs:keyref>
```

**Effect:**

- FK constraint is added to the child table with the specified rules
- No `DataRelation` is created
- No `Get{ChildTable}Rows()` method on the parent row
- No `{ParentTable}Row` property on the child row
- FK column appears as a raw value parameter in `AddRowAsync` (not a parent row parameter)

See [Relations -- Constraint-Only Relations](./relations.md#constraint-only-relations) for details.

---

### msdata:IsNested

**Applies to:** `xs:keyref` element

Sets `DataRelation.Nested = true`. This affects how the DataSet is serialized to XML: child rows are nested inside parent elements instead of being flat.

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:IsNested="true">
  <xs:selector xpath=".//Order" />
  <xs:field xpath="CustomerId" />
</xs:keyref>
```

**Effect:** Only affects XML serialization behavior. Navigation methods are generated identically.

---

### msdata:UpdateRule

**Applies to:** `xs:keyref` element

Sets the `ForeignKeyConstraint.UpdateRule`. Controls what happens to child rows when the parent's key column is updated.

| Value | Behavior |
|---|---|
| `Cascade` (default) | Child FK values are updated to match |
| `SetNull` | Child FK values are set to `DBNull` |
| `SetDefault` | Child FK values are set to their default |
| `None` | No action taken |

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:UpdateRule="Cascade">
  ...
</xs:keyref>
```

---

### msdata:DeleteRule

**Applies to:** `xs:keyref` element

Sets the `ForeignKeyConstraint.DeleteRule`. Controls what happens to child rows when the parent row is deleted.

| Value | Behavior |
|---|---|
| `Cascade` (default) | Child rows are deleted |
| `SetNull` | Child FK values are set to `DBNull` |
| `SetDefault` | Child FK values are set to their default |
| `None` | No action taken (may throw if children exist) |

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:DeleteRule="SetNull">
  ...
</xs:keyref>
```

---

### msdata:AcceptRejectRule

**Applies to:** `xs:keyref` element

Sets the `ForeignKeyConstraint.AcceptRejectRule`. Controls what happens to child rows when `AcceptChanges` or `RejectChanges` is called on the parent.

| Value | Behavior |
|---|---|
| `None` (default) | No action taken |
| `Cascade` | AcceptChanges/RejectChanges cascades to children |

```xml
<xs:keyref name="FK_Customer_Order" refer="PK_Customer"
           msdata:AcceptRejectRule="Cascade">
  ...
</xs:keyref>
```

---

### msdata:Locale

**Applies to:** DataSet root element

Sets the DataSet's `Locale` property for culture-sensitive comparisons.

```xml
<xs:element name="OrdersDS" msdata:IsDataSet="true"
            msdata:Locale="en-US">
  ...
</xs:element>
```

**Effect:**

```csharp
var inner = (DataSet)ds;
inner.Locale.Name;  // "en-US"
```

---

### msdata:CaseSensitive

**Applies to:** DataSet root element

Sets `DataSet.CaseSensitive`. When `true`, string comparisons and sorts within the DataSet are case-sensitive.

```xml
<xs:element name="AdvancedDS" msdata:IsDataSet="true"
            msdata:CaseSensitive="true">
  ...
</xs:element>
```

**Effect:**

```csharp
var inner = (DataSet)ds;
inner.CaseSensitive;  // true
```

---

### msdata:EnforceConstraints

**Applies to:** DataSet root element

Sets `DataSet.EnforceConstraints`. When `true` (the default), the DataSet validates constraints (unique, foreign key) on every change.

```xml
<xs:element name="AdvancedDS" msdata:IsDataSet="true"
            msdata:EnforceConstraints="true">
  ...
</xs:element>
```

---

## Quick Reference Table

| Annotation | Applies To | Purpose |
|---|---|---|
| `codegen:typedName` | Table element | Override row class name |
| `codegen:typedPlural` | Table element | Override table class name and accessor |
| `codegen:typedParent` | `xs:keyref` | Override parent row property name |
| `codegen:typedChildren` | `xs:keyref` | Override child rows method name |
| `codegen:nullValue` | Column element | Null-value read behavior |
| `msdata:IsDataSet` | Root element | **Required.** Mark as DataSet |
| `msdata:PrimaryKey` | `xs:unique`/`xs:key` | Mark as primary key, enable `FindBy` |
| `msdata:AutoIncrement` | Column element | Auto-increment column |
| `msdata:AutoIncrementSeed` | Column element | Auto-increment starting value |
| `msdata:AutoIncrementStep` | Column element | Auto-increment step value |
| `msdata:ReadOnly` | Column element | No setter generated |
| `msdata:Expression` | Column element | Computed column, no setter generated |
| `msdata:DefaultValue` | Column element | Column default value |
| `msdata:DataType` | Column element | CLR type override |
| `msdata:Ordinal` | Column element | Column ordinal position |
| `msdata:Caption` | Column element | Column display caption |
| `maxLength` | Column element | Column max length (standard XSD attribute) |
| `msdata:ConstraintOnly` | `xs:keyref` | FK constraint without DataRelation |
| `msdata:IsNested` | `xs:keyref` | Nested relation for XML serialization |
| `msdata:UpdateRule` | `xs:keyref` | FK update rule |
| `msdata:DeleteRule` | `xs:keyref` | FK delete rule |
| `msdata:AcceptRejectRule` | `xs:keyref` | FK accept/reject rule |
| `msdata:Locale` | DataSet root | DataSet locale |
| `msdata:CaseSensitive` | DataSet root | Case-sensitive comparisons |
| `msdata:EnforceConstraints` | DataSet root | Constraint enforcement |

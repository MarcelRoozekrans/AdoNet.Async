# Typed DataSet Source Generator Design

## Problem

Users with 50+ Visual Studio designer-generated typed DataSets (`.xsd` files) cannot use them with the async library. Wrapping in `AsyncDataTable`/`AsyncDataRow` loses all typed properties, making the library unusable for typed DataSet workflows.

## Solution

A Roslyn incremental source generator that reads `.xsd` files and produces fully typed async DataSet classes — replacing the VS designer entirely.

## Package

**`AdoNet.Async.DataSet.Generator`** — new NuGet package containing the source generator. Depends on `AdoNet.Async.DataSet` for base types.

### Usage

```xml
<ItemGroup>
  <PackageReference Include="AdoNet.Async.DataSet.Generator" />
  <AdditionalFiles Include="Schemas\Orders.xsd" />
</ItemGroup>
```

## Generated Type Hierarchy

| VS Designer Generated | Async Equivalent |
|---|---|
| `OrdersDataSet : DataSet` | `AsyncOrdersDataSet : AsyncDataSet` |
| `OrderDataTable : TypedTableBase<OrderRow>` | `AsyncOrderDataTable : AsyncDataTable<AsyncOrderRow>` |
| `OrderRow : DataRow` | `AsyncOrderRow : AsyncDataRow` |
| `OrderRowChangeEvent : EventArgs` | `AsyncOrderRowChangeEvent` |
| `OrderRowChangeEventHandler` delegate | `AsyncEventHandler<AsyncOrderRowChangeEvent>` |

## Generic Base Classes (additions to AdoNet.Async.DataSet)

### `AsyncDataTable<TRow>` where `TRow : AsyncDataRow`

```csharp
public class AsyncDataTable<TRow> : AsyncDataTable where TRow : AsyncDataRow
{
    public new AsyncDataRowCollection<TRow> Rows { get; }
    public new TRow this[int index] => Rows[index];
    public new TRow NewRow();
    protected virtual TRow WrapRow(DataRow innerRow);
}
```

### `AsyncDataRowCollection<TRow>` where `TRow : AsyncDataRow`

```csharp
public class AsyncDataRowCollection<TRow> : AsyncDataRowCollection, IEnumerable<TRow>
{
    public new TRow this[int index] { get; }
    public ValueTask AddAsync(TRow row, CancellationToken ct = default);
    public ValueTask RemoveAsync(TRow row, CancellationToken ct = default);
    public new IEnumerator<TRow> GetEnumerator();
}
```

Key points:
- Non-breaking: existing untyped classes unchanged
- `new` keyword shadows base untyped versions (same pattern as `TypedTableBase<T>`)
- `WrapRow` pattern: generator overrides to create concrete typed rows without reflection
- Casting to `AsyncDataTable` gives untyped access

## Generated AsyncDataSet Subclass

```csharp
public partial class AsyncOrdersDataSet : AsyncDataSet
{
    // Typed table accessors
    public AsyncOrderDataTable Order { get; }
    public AsyncOrderDetailDataTable OrderDetail { get; }

    // Relation accessors
    public DataRelation Order_OrderDetail { get; }

    // Initialization
    private void InitClass();       // Creates tables, relations, constraints
    internal void InitVars();       // Resolves cached fields
    private void InitExpressions(); // Sets computed column expressions

    // Clone
    public override AsyncDataSet Clone();
}
```

## Generated AsyncDataTable Subclass (per table)

```csharp
public partial class AsyncOrderDataTable : AsyncDataTable<AsyncOrderRow>
{
    // Column properties
    public DataColumn OrderIdColumn { get; }
    public DataColumn OrderDateColumn { get; }
    public DataColumn CustomerColumn { get; }

    // Typed row factory
    public new AsyncOrderRow NewRow();

    // Typed Add with parameters
    public ValueTask<AsyncOrderRow> AddOrderRowAsync(
        DateTime orderDate, string customer,
        CancellationToken ct = default);

    // FK-aware Add (parent row instead of FK value)
    public ValueTask<AsyncOrderRow> AddOrderRowAsync(
        AsyncCustomerRow parentCustomerRow,
        DateTime orderDate,
        CancellationToken ct = default);

    // Remove
    public ValueTask RemoveOrderRowAsync(AsyncOrderRow row, CancellationToken ct = default);

    // FindBy (primary key)
    public AsyncOrderRow? FindByOrderId(int orderId);

    // Typed async events
    public event AsyncEventHandler<AsyncOrderRowChangeEvent> OrderRowChangingAsync;
    public event AsyncEventHandler<AsyncOrderRowChangeEvent> OrderRowChangedAsync;
    public event AsyncEventHandler<AsyncOrderRowChangeEvent> OrderRowDeletingAsync;
    public event AsyncEventHandler<AsyncOrderRowChangeEvent> OrderRowDeletedAsync;

    // Initialization
    internal void InitVars();
    private void InitClass();

    // Protected overrides
    protected override AsyncOrderRow WrapRow(DataRow innerRow);
}
```

## Generated AsyncDataRow Subclass (per table)

```csharp
public partial class AsyncOrderRow : AsyncDataRow
{
    // Typed read-only properties
    public int OrderId => (int)this["OrderId"];
    public DateTime OrderDate => (DateTime)this["OrderDate"];
    public string Customer => (string)this["Customer"];

    // Typed async setters
    public ValueTask SetOrderDateAsync(DateTime value, CancellationToken ct = default)
        => SetValueAsync("OrderDate", value, ct);

    // Nullable column support
    public bool IsShipDateNull() => IsNull("ShipDate");
    public ValueTask SetShipDateNullAsync(CancellationToken ct = default)
        => SetValueAsync("ShipDate", DBNull.Value, ct);

    // Relation accessors — child rows
    public AsyncOrderDetailRow[] GetOrderDetailRows();

    // Relation accessors — parent row
    public AsyncCustomerRow? CustomerRow { get; }
    public ValueTask SetCustomerRowAsync(AsyncCustomerRow? parent, CancellationToken ct = default);
}
```

### Null Value Behavior (controlled by `codegen:nullValue`)

| Annotation | Getter Behavior |
|---|---|
| `_throw` (default) | Throws `StrongTypingException` when column is `DBNull` |
| `_null` | Returns `null` (reference types only) |
| `_empty` | Returns `String.Empty` or `default(T)` |
| `<literal>` | Returns the specified replacement value |

## Generated Event Args (per table)

```csharp
public class AsyncOrderRowChangeEvent
{
    public AsyncOrderRow Row { get; }
    public DataRowAction Action { get; }
}
```

## XSD Feature Coverage

| Feature | Generated Code |
|---|---|
| Simple columns | Read-only typed property + `SetXxxAsync()` method |
| Nullable columns (`minOccurs="0"`) | `IsXxxNull()` + `SetXxxNullAsync()`, getter behavior per `codegen:nullValue` |
| Computed columns (`msdata:Expression`) | Read-only typed property only, no setter |
| Read-only columns (`msdata:ReadOnly`) | Read-only typed property only, no setter |
| AutoIncrement columns | Column configured in `InitClass`, excluded from `AddXxxRowAsync` params |
| Primary key (`msdata:PrimaryKey="true"`) | `FindByXxx()` method on table |
| Composite primary key | `FindByXxxYyy(type1 xxx, type2 yyy)` |
| Unique constraints | `UniqueConstraint` in `InitClass` |
| Foreign keys (`xs:keyref`) | `ForeignKeyConstraint` + `DataRelation`; `GetChildRows()` on parent, parent accessor on child |
| `msdata:ConstraintOnly="true"` | `ForeignKeyConstraint` only, no relation, no accessors |
| Nested relations | `Nested = true`, auto-generated hidden FK column |
| `msdata:UpdateRule/DeleteRule/AcceptRejectRule` | Passed to `ForeignKeyConstraint` |
| Default values (`msdata:DefaultValue`) | `DataColumn.DefaultValue` in `InitClass` |
| `msdata:DataType` override | Uses specified CLR type |
| `msdata:Ordinal` | `SetOrdinal()` in `InitClass` |
| `msdata:Caption` | `DataColumn.Caption` set |
| `codegen:typedName/typedPlural` | Overrides class and method names |
| `codegen:typedParent/typedChildren` | Overrides relation accessor names |
| DataSet properties (`Locale`, `CaseSensitive`, `EnforceConstraints`) | Set on `AsyncDataSet` in `InitClass` |

### XSD Type to CLR Type Mapping

| XSD Type | CLR Type |
|---|---|
| `xs:string` | `System.String` |
| `xs:int` | `System.Int32` |
| `xs:integer` | `System.Int64` |
| `xs:boolean` | `System.Boolean` |
| `xs:dateTime` | `System.DateTime` |
| `xs:decimal` | `System.Decimal` |
| `xs:double` | `System.Double` |
| `xs:float` | `System.Single` |
| `xs:long` | `System.Int64` |
| `xs:short` | `System.Int16` |
| `xs:byte` | `System.SByte` |
| `xs:unsignedByte` | `System.Byte` |
| `xs:base64Binary` | `System.Byte[]` |

`msdata:DataType` attribute overrides any of these.

## Generator Implementation

### Roslyn Incremental Source Generator

Implemented as `IIncrementalGenerator` — only re-runs when `.xsd` files change.

### Pipeline

```
.xsd AdditionalFile
  -> Parse XML schema
  -> Build intermediate model (tables, columns, relations, constraints)
  -> Resolve codegen annotations (typedName, nullValue, etc.)
  -> Emit C# source for each type
```

### Intermediate Model

```
DataSetModel
  +-- Name, Namespace, Locale, CaseSensitive, EnforceConstraints
  +-- Tables[]
  |     +-- Name, TypedName, TypedPlural
  |     +-- Columns[]
  |     |     +-- Name, ClrType, AllowDBNull, ReadOnly, Expression
  |     |     +-- AutoIncrement, Seed, Step
  |     |     +-- DefaultValue, Caption, Ordinal, MaxLength
  |     |     +-- NullValueBehavior (Throw|Null|Empty|Replacement)
  |     |     +-- IsHidden (auto-generated FK columns)
  |     +-- PrimaryKey[] (column references)
  |     +-- UniqueConstraints[]
  +-- Relations[]
  |     +-- Name, ParentTable, ChildTable
  |     +-- ParentColumns[], ChildColumns[]
  |     +-- Nested, ConstraintOnly
  |     +-- UpdateRule, DeleteRule, AcceptRejectRule
  |     +-- TypedParent, TypedChildren (codegen overrides)
  +-- ForeignKeyConstraints[]
```

### Emitted Files (per .xsd)

| File | Contents |
|---|---|
| `{DataSetName}.AsyncDataSet.g.cs` | The `AsyncXxxDataSet` class |
| `{DataSetName}.{TableName}.AsyncDataTable.g.cs` | One per table |
| `{DataSetName}.{TableName}.AsyncDataRow.g.cs` | One per table |
| `{DataSetName}.{TableName}.Events.g.cs` | One per table — event args |

All emitted as `partial class` with `[GeneratedCode]` attribute and `#nullable enable`.

### Error Handling

Diagnostics (not exceptions) for:
- Malformed XSD
- Unsupported type mappings
- Missing key references in `keyref`
- Circular nested relations

## Testing Strategy

### Unit Tests — `AdoNet.Async.DataSet.Generator.Tests`

| Category | What's Tested |
|---|---|
| XSD Parsing | Each feature in isolation |
| Type Mapping | All XSD types -> CLR types, `msdata:DataType` overrides |
| Code Emission | Generated source compiles and matches expected shape (via `GeneratorDriver`) |
| Snapshot Tests | Golden-file comparison of `.g.cs` against expected output |
| Diagnostics | Malformed XSD produces correct error codes |

### Integration Tests

Test `.xsd` files:
1. **Simple** — single table, basic types, PK, nullable columns
2. **Relations** — parent-child with `keyref`, `FindBy`, `GetChildRows`, parent accessor
3. **Full-featured** — nested relations, expressions, auto-increment, defaults, read-only, all `codegen:` annotations, composite PKs, constraint-only FKs
4. **Multi-table** — 5+ tables exercising complete feature matrix

Runtime integration tests:
- Create typed `AsyncDataSet`, verify table/relation properties
- `AddXxxRowAsync` with typed parameters, verify via typed properties
- `FindByXxx` on primary keys
- Null handling: `IsXxxNull()`, `SetXxxNullAsync()`, `StrongTypingException`
- Relation navigation: `GetChildRows()`, parent accessor, `SetParentRowAsync()`
- Typed async events fire with correct typed row and action
- `FillAsync` via adapter populates typed tables with typed rows
- Serialization round-trip (both JSON serializers) with typed DataSets
- `Clone()`, `AcceptChangesAsync()`, `RejectChangesAsync()` on typed instances

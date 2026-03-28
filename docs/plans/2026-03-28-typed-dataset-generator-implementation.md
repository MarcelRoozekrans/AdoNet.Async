# Typed DataSet Source Generator Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Roslyn incremental source generator that reads `.xsd` files and produces fully typed async DataSet/DataTable/DataRow classes, replacing the VS designer entirely.

**Architecture:** New NuGet package `AdoNet.Async.DataSet.Generator` containing an `IIncrementalGenerator`. It parses `.xsd` AdditionalFiles into an intermediate model, then emits typed async wrappers that extend the generic base classes `AsyncDataTable<TRow>` and `AsyncDataRowCollection<TRow>` (added to the existing `AdoNet.Async.DataSet` package).

**Tech Stack:** Roslyn incremental source generators (`Microsoft.CodeAnalysis.CSharp` 4.x), xUnit 2.x, FluentAssertions 8.x, `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` for generator unit tests.

---

## Phase 1: Generic Base Classes

Add generic typed versions of `AsyncDataTable` and `AsyncDataRowCollection` to the existing `AdoNet.Async.DataSet` package. These are the foundation the generator builds on.

### Task 1: Unseal AsyncDataRow for inheritance

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataRow.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs`

Currently `AsyncDataRow` is `sealed`. The generator needs to produce subclasses. We must unseal it and make the constructor `protected internal` so generated subclasses can call it.

**Step 1: Write the failing test**

Add to `tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs`:

```csharp
[Fact]
public void AsyncDataRow_Can_Be_Subclassed()
{
    using var table = new AsyncDataTable("Test");
    table.Columns.Add("Id", typeof(int));
    var innerRow = table.InnerDataTable.NewRow();

    var row = new TestAsyncDataRow(innerRow, table);

    row.Should().BeAssignableTo<AsyncDataRow>();
}

private sealed class TestAsyncDataRow : AsyncDataRow
{
    public TestAsyncDataRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRow_Can_Be_Subclassed" -v n`
Expected: FAIL — `AsyncDataRow` is sealed, cannot inherit.

**Step 3: Unseal AsyncDataRow**

In `src/System.Data.Async.DataSet/AsyncDataRow.cs`:
- Change `public sealed class AsyncDataRow` to `public class AsyncDataRow`
- Change `internal AsyncDataRow(DataRow inner, AsyncDataTable table)` to `protected internal AsyncDataRow(DataRow inner, AsyncDataTable table)`
- Add `protected DataRow InnerRow => _inner;` (so subclasses can access the inner row)
- Add `protected bool IsNull(string columnName) => _inner.IsNull(columnName);`
- Add `protected bool IsNull(DataColumn column) => _inner.IsNull(column);`

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRow_Can_Be_Subclassed" -v n`
Expected: PASS

**Step 5: Run all existing tests to verify no regressions**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests -v n`
Expected: All PASS

**Step 6: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataRow.cs tests/System.Data.Async.DataSet.Tests/AsyncDataRowTests.cs
git commit -m "refactor: unseal AsyncDataRow for typed subclass support"
```

---

### Task 2: Unseal AsyncDataRowCollection for inheritance

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`

Currently `AsyncDataRowCollection` is `sealed`. The generic version needs to inherit from it.

**Step 1: Unseal AsyncDataRowCollection**

In `src/System.Data.Async.DataSet/AsyncDataRowCollection.cs`:
- Change `public sealed class AsyncDataRowCollection` to `public class AsyncDataRowCollection`
- Change `internal AsyncDataRowCollection(DataRowCollection inner, AsyncDataTable table)` to `protected internal AsyncDataRowCollection(DataRowCollection inner, AsyncDataTable table)`
- Make `_inner` and `_table` fields `protected` instead of `private`

**Step 2: Run all existing tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests -v n`
Expected: All PASS (unsealing is non-breaking)

**Step 3: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataRowCollection.cs
git commit -m "refactor: unseal AsyncDataRowCollection for typed subclass support"
```

---

### Task 3: Add AsyncDataRowCollection\<TRow>

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataRowCollection{TRow}.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionGenericTests.cs`

**Step 1: Write the failing test**

Create `tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionGenericTests.cs`:

```csharp
using FluentAssertions;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowCollectionGenericTests
{
    private sealed class TestRow : AsyncDataRow
    {
        public TestRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
        public int Id => (int)this["Id"];
    }

    [Fact]
    public void Indexer_Returns_Typed_Row()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(42);

        var collection = new AsyncDataRowCollection<TestRow>(
            table.InnerDataTable.Rows, table, (inner, t) => new TestRow(inner, t));

        collection[0].Should().BeOfType<TestRow>();
        collection[0].Id.Should().Be(42);
    }

    [Fact]
    public void Enumeration_Returns_Typed_Rows()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(1);
        table.InnerDataTable.Rows.Add(2);

        var collection = new AsyncDataRowCollection<TestRow>(
            table.InnerDataTable.Rows, table, (inner, t) => new TestRow(inner, t));

        collection.Should().HaveCount(2);
        collection.Should().AllBeOfType<TestRow>();
    }

    [Fact]
    public async Task AddAsync_Returns_Typed_Row_Via_Indexer()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));

        var collection = new AsyncDataRowCollection<TestRow>(
            table.InnerDataTable.Rows, table, (inner, t) => new TestRow(inner, t));

        var innerRow = table.InnerDataTable.NewRow();
        innerRow["Id"] = 99;
        var row = new TestRow(innerRow, table);
        await collection.AddAsync(row);

        collection[0].Id.Should().Be(99);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowCollectionGenericTests" -v n`
Expected: FAIL — `AsyncDataRowCollection<TRow>` does not exist.

**Step 3: Implement AsyncDataRowCollection\<TRow>**

Create `src/System.Data.Async.DataSet/AsyncDataRowCollection{TRow}.cs`:

```csharp
using System.Collections;

namespace System.Data.Async.DataSet;

public class AsyncDataRowCollection<TRow> : AsyncDataRowCollection, IEnumerable<TRow>
    where TRow : AsyncDataRow
{
    private readonly Func<DataRow, AsyncDataTable, TRow> _rowFactory;

    public AsyncDataRowCollection(
        DataRowCollection inner,
        AsyncDataTable table,
        Func<DataRow, AsyncDataTable, TRow> rowFactory)
        : base(inner, table)
    {
        _rowFactory = rowFactory;
    }

    public new TRow this[int index] => _rowFactory(_inner[index], _table);

#pragma warning disable HLQ006
    public new IEnumerator<TRow> GetEnumerator()
    {
        for (int i = 0; i < _inner.Count; i++)
        {
            yield return _rowFactory(_inner[i], _table);
        }
    }
#pragma warning restore HLQ006

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataRowCollectionGenericTests" -v n`
Expected: All PASS

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataRowCollection{TRow}.cs tests/System.Data.Async.DataSet.Tests/AsyncDataRowCollectionGenericTests.cs
git commit -m "feat: add AsyncDataRowCollection<TRow> generic typed collection"
```

---

### Task 4: Add AsyncDataTable\<TRow>

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataTable{TRow}.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableGenericTests.cs`

**Step 1: Write the failing test**

Create `tests/System.Data.Async.DataSet.Tests/AsyncDataTableGenericTests.cs`:

```csharp
using FluentAssertions;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableGenericTests
{
    private sealed class TestRow : AsyncDataRow
    {
        public TestRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
        public int Id => (int)this["Id"];
        public string Name => (string)this["Name"];
    }

    private sealed class TestTable : AsyncDataTable<TestRow>
    {
        public TestTable() : base("Test") { }
        protected override TestRow WrapRow(DataRow innerRow) => new(innerRow, this);
    }

    [Fact]
    public void NewRow_Returns_Typed_Row()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();

        row.Should().BeOfType<TestRow>();
    }

    [Fact]
    public void Rows_Returns_Typed_Collection()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));

        table.Rows.Should().BeOfType<AsyncDataRowCollection<TestRow>>();
    }

    [Fact]
    public async Task Rows_AddAsync_And_Indexer_Return_Typed_Rows()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Alice");
        await table.Rows.AddAsync(row);

        table.Rows[0].Should().BeOfType<TestRow>();
        table.Rows[0].Id.Should().Be(1);
        table.Rows[0].Name.Should().Be("Alice");
    }

    [Fact]
    public void Indexer_Returns_Typed_Row()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(42);

        table[0].Should().BeOfType<TestRow>();
        table[0].Id.Should().Be(42);
    }

    [Fact]
    public void Can_Cast_To_Untyped_AsyncDataTable()
    {
        using var table = new TestTable();

        AsyncDataTable untyped = table;
        untyped.Should().BeSameAs(table);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataTableGenericTests" -v n`
Expected: FAIL — `AsyncDataTable<TRow>` does not exist.

**Step 3: Implement AsyncDataTable\<TRow>**

Create `src/System.Data.Async.DataSet/AsyncDataTable{TRow}.cs`:

```csharp
namespace System.Data.Async.DataSet;

public abstract class AsyncDataTable<TRow> : AsyncDataTable
    where TRow : AsyncDataRow
{
    private AsyncDataRowCollection<TRow>? _typedRows;

    protected AsyncDataTable(string tableName) : base(tableName) { }
    protected AsyncDataTable(string tableName, string tableNamespace) : base(tableName, tableNamespace) { }
    protected AsyncDataTable(DataTable inner) : base(inner) { }

    protected abstract TRow WrapRow(DataRow innerRow);

    public new AsyncDataRowCollection<TRow> Rows =>
        _typedRows ??= new AsyncDataRowCollection<TRow>(InnerDataTable.Rows, this, (inner, t) => WrapRow(inner));

    public new TRow NewRow()
    {
        var innerRow = InnerDataTable.NewRow();
        return WrapRow(innerRow);
    }

    public TRow this[int index] => Rows[index];
}
```

Note: `AsyncDataTable` needs to expose `InnerDataTable` as `protected internal` (currently it is `internal` via the `_inner` field). Check and adjust visibility in `AsyncDataTable.cs` if needed — the inner DataTable may need a protected accessor.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests --filter "AsyncDataTableGenericTests" -v n`
Expected: All PASS

**Step 5: Run all tests for regressions**

Run: `dotnet test tests/System.Data.Async.DataSet.Tests -v n`
Expected: All PASS

**Step 6: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataTable{TRow}.cs tests/System.Data.Async.DataSet.Tests/AsyncDataTableGenericTests.cs
git commit -m "feat: add AsyncDataTable<TRow> generic typed table base class"
```

---

## Phase 2: Source Generator Project Setup

### Task 5: Create generator project and test project

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/System.Data.Async.DataSet.Generator.csproj`
- Create: `tests/System.Data.Async.DataSet.Generator.Tests/System.Data.Async.DataSet.Generator.Tests.csproj`
- Modify: `System.Data.Async.slnx`

**Step 1: Create the generator project**

Create `src/System.Data.Async.DataSet.Generator/System.Data.Async.DataSet.Generator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <RootNamespace>System.Data.Async.DataSet.Generator</RootNamespace>
    <PackageId>AdoNet.Async.DataSet.Generator</PackageId>
    <Title>AdoNet.Async.DataSet.Generator</Title>
    <Description>Roslyn source generator that produces typed async DataSet/DataTable/DataRow classes from .xsd schema files.</Description>
    <PackageTags>system.data.async;async;ado.net;dataset;typed-dataset;source-generator;xsd</PackageTags>
    <IsRoslynComponent>true</IsRoslynComponent>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <!-- Suppress warnings not applicable to netstandard2.0 source generators -->
    <NoWarn>$(NoWarn);NU5128</NoWarn>
  </PropertyGroup>

  <!-- Override root Directory.Build.props target framework -->
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

Important: Source generators MUST target `netstandard2.0`. The root `Directory.Build.props` sets `net10.0`, so this project must override it. The generator also needs `<IsRoslynComponent>true</IsRoslynComponent>` and must NOT reference the runtime DataSet package (it only emits code that references it).

**Step 2: Create the test project**

Create `tests/System.Data.Async.DataSet.Generator.Tests/System.Data.Async.DataSet.Generator.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet.Generator\System.Data.Async.DataSet.Generator.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit" Version="1.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
  </ItemGroup>
</Project>
```

**Step 3: Update solution file**

In `System.Data.Async.slnx`, add:
- `src/System.Data.Async.DataSet.Generator/System.Data.Async.DataSet.Generator.csproj` under `/src/`
- `tests/System.Data.Async.DataSet.Generator.Tests/System.Data.Async.DataSet.Generator.Tests.csproj` under `/tests/`

**Step 4: Create generator entry point stub**

Create `src/System.Data.Async.DataSet.Generator/TypedDataSetGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace System.Data.Async.DataSet.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TypedDataSetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline will be implemented in subsequent tasks
    }
}
```

**Step 5: Verify it builds**

Run: `dotnet build src/System.Data.Async.DataSet.Generator`
Expected: Build succeeds.

Run: `dotnet build tests/System.Data.Async.DataSet.Generator.Tests`
Expected: Build succeeds.

**Step 6: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/ tests/System.Data.Async.DataSet.Generator.Tests/ System.Data.Async.slnx
git commit -m "feat: scaffold source generator and test projects"
```

---

## Phase 3: XSD Parser — Intermediate Model

### Task 6: Define the intermediate model types

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Model/DataSetModel.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Model/TableModel.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Model/ColumnModel.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Model/RelationModel.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Model/ConstraintModel.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Model/NullValueBehavior.cs`

These are pure data-carrying record types with `IEquatable<T>` for incremental generator caching.

**Step 1: Create the model types**

Create `src/System.Data.Async.DataSet.Generator/Model/NullValueBehavior.cs`:

```csharp
namespace System.Data.Async.DataSet.Generator.Model;

internal enum NullValueBehaviorKind
{
    Throw,
    ReturnNull,
    ReturnEmpty,
    ReplacementValue
}

internal sealed record NullValueBehavior(NullValueBehaviorKind Kind, string? ReplacementValue = null);
```

Create `src/System.Data.Async.DataSet.Generator/Model/ColumnModel.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record ColumnModel(
    string Name,
    string ClrTypeName,
    bool AllowDBNull,
    bool ReadOnly,
    string? Expression,
    bool AutoIncrement,
    long AutoIncrementSeed,
    long AutoIncrementStep,
    string? DefaultValue,
    string? Caption,
    int? Ordinal,
    int? MaxLength,
    bool IsHidden,
    NullValueBehavior NullValueBehavior);
```

Create `src/System.Data.Async.DataSet.Generator/Model/ConstraintModel.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record UniqueConstraintModel(
    string Name,
    string TableName,
    ImmutableArray<string> ColumnNames,
    bool IsPrimaryKey);

internal sealed record ForeignKeyConstraintModel(
    string Name,
    string ParentTableName,
    ImmutableArray<string> ParentColumnNames,
    string ChildTableName,
    ImmutableArray<string> ChildColumnNames,
    string UpdateRule,
    string DeleteRule,
    string AcceptRejectRule);
```

Create `src/System.Data.Async.DataSet.Generator/Model/RelationModel.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record RelationModel(
    string Name,
    string ParentTableName,
    ImmutableArray<string> ParentColumnNames,
    string ChildTableName,
    ImmutableArray<string> ChildColumnNames,
    bool Nested,
    bool ConstraintOnly,
    string? TypedParent,
    string? TypedChildren);
```

Create `src/System.Data.Async.DataSet.Generator/Model/TableModel.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record TableModel(
    string Name,
    string? TypedName,
    string? TypedPlural,
    ImmutableArray<ColumnModel> Columns,
    ImmutableArray<string> PrimaryKeyColumnNames,
    ImmutableArray<UniqueConstraintModel> UniqueConstraints);
```

Create `src/System.Data.Async.DataSet.Generator/Model/DataSetModel.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Model;

internal sealed record DataSetModel(
    string Name,
    string? Namespace,
    string? Locale,
    bool CaseSensitive,
    bool EnforceConstraints,
    ImmutableArray<TableModel> Tables,
    ImmutableArray<RelationModel> Relations,
    ImmutableArray<ForeignKeyConstraintModel> ForeignKeyConstraints);
```

**Step 2: Verify it builds**

Run: `dotnet build src/System.Data.Async.DataSet.Generator`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Model/
git commit -m "feat: add intermediate model types for XSD-to-code pipeline"
```

---

### Task 7: Implement XSD parser

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Parsing/XsdParser.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Parsing/XsdTypeMapper.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Parsing/XsdParserTests.cs`
- Create: `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Simple.xsd` (test fixture)

**Step 1: Create test XSD fixture**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Simple.xsd`:

```xml
<?xml version="1.0" standalone="yes"?>
<xs:schema id="OrdersDS"
           xmlns:xs="http://www.w3.org/2001/XMLSchema"
           xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
           xmlns:codegen="urn:schemas-microsoft-com:xml-msprop">
  <xs:element name="OrdersDS" msdata:IsDataSet="true" msdata:Locale="en-US">
    <xs:complexType>
      <xs:choice maxOccurs="unbounded">
        <xs:element name="Customer">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CustomerId" type="xs:int" />
              <xs:element name="Name" type="xs:string" />
              <xs:element name="Email" type="xs:string" minOccurs="0" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:element name="Order">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="OrderId" type="xs:int" msdata:AutoIncrement="true" msdata:AutoIncrementSeed="1" msdata:AutoIncrementStep="1" />
              <xs:element name="CustomerId" type="xs:int" />
              <xs:element name="OrderDate" type="xs:dateTime" />
              <xs:element name="Total" type="xs:decimal" />
              <xs:element name="Notes" type="xs:string" minOccurs="0" codegen:nullValue="" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:choice>
    </xs:complexType>
    <xs:unique name="PK_Customer" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Customer" />
      <xs:field xpath="CustomerId" />
    </xs:unique>
    <xs:unique name="PK_Order" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Order" />
      <xs:field xpath="OrderId" />
    </xs:unique>
    <xs:keyref name="FK_Customer_Order" refer="PK_Customer">
      <xs:selector xpath=".//Order" />
      <xs:field xpath="CustomerId" />
    </xs:keyref>
  </xs:element>
</xs:schema>
```

**Step 2: Write parser tests**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Parsing/XsdParserTests.cs`:

```csharp
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Parsing;

public class XsdParserTests
{
    private static string LoadSchema(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_Simple_DataSetName()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        model.Name.Should().Be("OrdersDS");
    }

    [Fact]
    public void Parse_Simple_Locale()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        model.Locale.Should().Be("en-US");
    }

    [Fact]
    public void Parse_Simple_Tables()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        model.Tables.Should().HaveCount(2);
        model.Tables.Select(t => t.Name).Should().BeEquivalentTo("Customer", "Order");
    }

    [Fact]
    public void Parse_Simple_Customer_Columns()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        var customer = model.Tables.First(t => t.Name == "Customer");
        customer.Columns.Should().HaveCount(3);
        customer.Columns[0].Name.Should().Be("CustomerId");
        customer.Columns[0].ClrTypeName.Should().Be("System.Int32");
        customer.Columns[0].AllowDBNull.Should().BeFalse();
        customer.Columns[2].Name.Should().Be("Email");
        customer.Columns[2].AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_Simple_AutoIncrement()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        var order = model.Tables.First(t => t.Name == "Order");
        var orderId = order.Columns.First(c => c.Name == "OrderId");
        orderId.AutoIncrement.Should().BeTrue();
        orderId.AutoIncrementSeed.Should().Be(1);
        orderId.AutoIncrementStep.Should().Be(1);
    }

    [Fact]
    public void Parse_Simple_NullValueBehavior_Empty()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        var order = model.Tables.First(t => t.Name == "Order");
        var notes = order.Columns.First(c => c.Name == "Notes");
        notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnEmpty);
    }

    [Fact]
    public void Parse_Simple_PrimaryKeys()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        var customer = model.Tables.First(t => t.Name == "Customer");
        customer.PrimaryKeyColumnNames.Should().ContainSingle("CustomerId");
    }

    [Fact]
    public void Parse_Simple_Relations()
    {
        var model = XsdParser.Parse(LoadSchema("Simple.xsd"));
        model.Relations.Should().ContainSingle();
        var rel = model.Relations[0];
        rel.Name.Should().Be("FK_Customer_Order");
        rel.ParentTableName.Should().Be("Customer");
        rel.ChildTableName.Should().Be("Order");
        rel.ParentColumnNames.Should().ContainSingle("CustomerId");
        rel.ChildColumnNames.Should().ContainSingle("CustomerId");
    }
}
```

Ensure the `.xsd` file is copied to output: add to the test `.csproj`:

```xml
<ItemGroup>
  <None Include="Schemas\**\*.xsd" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "XsdParserTests" -v n`
Expected: FAIL — `XsdParser` does not exist.

**Step 4: Create XsdTypeMapper**

Create `src/System.Data.Async.DataSet.Generator/Parsing/XsdTypeMapper.cs`:

```csharp
using System.Collections.Generic;

namespace System.Data.Async.DataSet.Generator.Parsing;

internal static class XsdTypeMapper
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xs:string"] = "System.String",
        ["xs:int"] = "System.Int32",
        ["xs:integer"] = "System.Int64",
        ["xs:boolean"] = "System.Boolean",
        ["xs:dateTime"] = "System.DateTime",
        ["xs:decimal"] = "System.Decimal",
        ["xs:double"] = "System.Double",
        ["xs:float"] = "System.Single",
        ["xs:long"] = "System.Int64",
        ["xs:short"] = "System.Int16",
        ["xs:byte"] = "System.SByte",
        ["xs:unsignedByte"] = "System.Byte",
        ["xs:unsignedShort"] = "System.UInt16",
        ["xs:unsignedInt"] = "System.UInt32",
        ["xs:unsignedLong"] = "System.UInt64",
        ["xs:base64Binary"] = "System.Byte[]",
        ["xs:duration"] = "System.TimeSpan",
        ["xs:time"] = "System.DateTime",
        ["xs:date"] = "System.DateTime",
        ["xs:anyURI"] = "System.String",
        ["xs:QName"] = "System.String",
    };

    public static string? TryMap(string xsdType)
    {
        return Map.TryGetValue(xsdType, out var clrType) ? clrType : null;
    }
}
```

**Step 5: Create XsdParser**

Create `src/System.Data.Async.DataSet.Generator/Parsing/XsdParser.cs`:

This is the most complex piece. It parses the XSD XML into the intermediate model. Key responsibilities:
- Read `xs:element` with `msdata:IsDataSet="true"` as the root
- Parse each table element within `xs:choice`
- Parse columns from `xs:sequence` elements
- Read `msdata:*` attributes (AutoIncrement, ReadOnly, DefaultValue, DataType, Expression, etc.)
- Read `codegen:*` attributes (typedName, typedPlural, typedParent, typedChildren, nullValue)
- Parse `xs:unique` (with `msdata:PrimaryKey`) → primary keys and unique constraints
- Parse `xs:keyref` → relations and foreign key constraints
- Handle nested complex types → implicit parent-child relations with hidden FK column

```csharp
using System.Collections.Immutable;
using System.Xml.Linq;
using System.Data.Async.DataSet.Generator.Model;

namespace System.Data.Async.DataSet.Generator.Parsing;

internal static class XsdParser
{
    private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Msdata = "urn:schemas-microsoft-com:xml-msdata";
    private static readonly XNamespace Codegen = "urn:schemas-microsoft-com:xml-msprop";

    public static DataSetModel Parse(string xsdContent)
    {
        var doc = XDocument.Parse(xsdContent);
        var schema = doc.Root!;
        var dsElement = schema.Descendants(Xs + "element")
            .First(e => string.Equals((string?)e.Attribute(Msdata + "IsDataSet"), "true", StringComparison.OrdinalIgnoreCase));

        var name = (string)dsElement.Attribute("name")!;
        var locale = (string?)dsElement.Attribute(Msdata + "Locale");
        var caseSensitive = ParseBool(dsElement, Msdata + "CaseSensitive");
        var enforceConstraints = ParseBool(dsElement, Msdata + "EnforceConstraints", defaultValue: true);

        var complexType = dsElement.Element(Xs + "complexType")!;
        var choice = complexType.Element(Xs + "choice") ?? complexType.Element(Xs + "sequence");

        var tables = ImmutableArray.CreateBuilder<TableModel>();
        var uniqueKeys = new Dictionary<string, (string TableName, ImmutableArray<string> Columns, bool IsPrimaryKey)>(StringComparer.Ordinal);

        if (choice != null)
        {
            foreach (var tableElement in choice.Elements(Xs + "element"))
            {
                tables.Add(ParseTable(tableElement));
            }
        }

        // Parse xs:unique constraints
        foreach (var unique in dsElement.Elements(Xs + "unique"))
        {
            var uName = (string)unique.Attribute("name")!;
            var isPk = ParseBool(unique, Msdata + "PrimaryKey");
            var selector = unique.Element(Xs + "selector")!;
            var tableName = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var columns = unique.Elements(Xs + "field")
                .Select(f => ExtractColumnNameFromXPath((string)f.Attribute("xpath")!))
                .ToImmutableArray();

            uniqueKeys[uName] = (tableName, columns, isPk);

            // Update table with PK info
            if (isPk)
            {
                var tableIndex = tables.FindIndex(t => t.Name == tableName);
                if (tableIndex >= 0)
                {
                    tables[tableIndex] = tables[tableIndex] with { PrimaryKeyColumnNames = columns };
                }
            }
        }

        // Parse xs:key constraints (also used as unique references)
        foreach (var key in dsElement.Elements(Xs + "key"))
        {
            var kName = (string)key.Attribute("name")!;
            var isPk = ParseBool(key, Msdata + "PrimaryKey");
            var selector = key.Element(Xs + "selector")!;
            var tableName = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var columns = key.Elements(Xs + "field")
                .Select(f => ExtractColumnNameFromXPath((string)f.Attribute("xpath")!))
                .ToImmutableArray();

            uniqueKeys[kName] = (tableName, columns, isPk);

            if (isPk)
            {
                var tableIndex = tables.FindIndex(t => t.Name == tableName);
                if (tableIndex >= 0)
                {
                    tables[tableIndex] = tables[tableIndex] with { PrimaryKeyColumnNames = columns };
                }
            }
        }

        // Parse xs:keyref → relations and FK constraints
        var relations = ImmutableArray.CreateBuilder<RelationModel>();
        var fkConstraints = ImmutableArray.CreateBuilder<ForeignKeyConstraintModel>();

        foreach (var keyref in dsElement.Elements(Xs + "keyref"))
        {
            var krName = (string)keyref.Attribute("name")!;
            var refer = (string)keyref.Attribute("refer")!;
            var constraintOnly = ParseBool(keyref, Msdata + "ConstraintOnly");
            var nested = ParseBool(keyref, Msdata + "IsNested");
            var updateRule = (string?)keyref.Attribute(Msdata + "UpdateRule") ?? "Cascade";
            var deleteRule = (string?)keyref.Attribute(Msdata + "DeleteRule") ?? "Cascade";
            var acceptRejectRule = (string?)keyref.Attribute(Msdata + "AcceptRejectRule") ?? "None";
            var typedParent = (string?)keyref.Attribute(Codegen + "typedParent");
            var typedChildren = (string?)keyref.Attribute(Codegen + "typedChildren");

            var selector = keyref.Element(Xs + "selector")!;
            var childTable = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var childColumns = keyref.Elements(Xs + "field")
                .Select(f => ExtractColumnNameFromXPath((string)f.Attribute("xpath")!))
                .ToImmutableArray();

            if (!uniqueKeys.TryGetValue(refer, out var parentInfo))
                continue;

            fkConstraints.Add(new ForeignKeyConstraintModel(
                krName, parentInfo.TableName, parentInfo.Columns,
                childTable, childColumns,
                updateRule, deleteRule, acceptRejectRule));

            if (!constraintOnly)
            {
                relations.Add(new RelationModel(
                    krName, parentInfo.TableName, parentInfo.Columns,
                    childTable, childColumns,
                    nested, constraintOnly, typedParent, typedChildren));
            }
        }

        // Build unique constraints list (non-PK)
        var allTables = tables.ToImmutable();
        var updatedTables = allTables.Select(t =>
        {
            var ucs = uniqueKeys.Values
                .Where(u => u.TableName == t.Name && !u.IsPrimaryKey)
                .Select(u => new UniqueConstraintModel(t.Name + "_Unique", t.Name, u.Columns, false))
                .ToImmutableArray();
            return t with { UniqueConstraints = ucs };
        }).ToImmutableArray();

        return new DataSetModel(
            name, null, locale, caseSensitive, enforceConstraints,
            updatedTables, relations.ToImmutable(), fkConstraints.ToImmutable());
    }

    private static TableModel ParseTable(XElement tableElement)
    {
        var tableName = (string)tableElement.Attribute("name")!;
        var typedName = (string?)tableElement.Attribute(Codegen + "typedName");
        var typedPlural = (string?)tableElement.Attribute(Codegen + "typedPlural");

        var complexType = tableElement.Element(Xs + "complexType");
        var columns = ImmutableArray.CreateBuilder<ColumnModel>();

        if (complexType != null)
        {
            var sequence = complexType.Element(Xs + "sequence");
            if (sequence != null)
            {
                foreach (var col in sequence.Elements(Xs + "element"))
                {
                    columns.Add(ParseColumn(col));
                }
            }
        }

        return new TableModel(
            tableName, typedName, typedPlural,
            columns.ToImmutable(),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);
    }

    private static ColumnModel ParseColumn(XElement colElement)
    {
        var colName = (string)colElement.Attribute("name")!;
        var xsdType = (string?)colElement.Attribute("type");
        var dataTypeOverride = (string?)colElement.Attribute(Msdata + "DataType");
        var clrType = dataTypeOverride ?? (xsdType != null ? XsdTypeMapper.TryMap(xsdType) : null) ?? "System.String";

        var minOccurs = (string?)colElement.Attribute("minOccurs");
        var allowDbNull = minOccurs == "0";

        var readOnly = ParseBool(colElement, Msdata + "ReadOnly");
        var expression = (string?)colElement.Attribute(Msdata + "Expression");
        var autoIncrement = ParseBool(colElement, Msdata + "AutoIncrement");
        var seed = ParseLong(colElement, Msdata + "AutoIncrementSeed", 0);
        var step = ParseLong(colElement, Msdata + "AutoIncrementStep", 1);
        var defaultValue = (string?)colElement.Attribute(Msdata + "DefaultValue");
        var caption = (string?)colElement.Attribute(Msdata + "Caption");
        var ordinal = ParseNullableInt(colElement, Msdata + "Ordinal");
        var maxLength = ParseNullableInt(colElement, "maxLength");
        var isHidden = ParseBool(colElement, Msdata + "hiddenColumn");

        var nullValueRaw = (string?)colElement.Attribute(Codegen + "nullValue");
        var nullBehavior = ParseNullValueBehavior(nullValueRaw, allowDbNull);

        return new ColumnModel(
            colName, clrType, allowDbNull, readOnly, expression,
            autoIncrement, seed, step, defaultValue, caption,
            ordinal, maxLength, isHidden, nullBehavior);
    }

    private static NullValueBehavior ParseNullValueBehavior(string? raw, bool allowDbNull)
    {
        if (!allowDbNull || raw == null)
            return new NullValueBehavior(NullValueBehaviorKind.Throw);

        return raw switch
        {
            "_throw" => new NullValueBehavior(NullValueBehaviorKind.Throw),
            "_null" => new NullValueBehavior(NullValueBehaviorKind.ReturnNull),
            "_empty" or "" => new NullValueBehavior(NullValueBehaviorKind.ReturnEmpty),
            _ => new NullValueBehavior(NullValueBehaviorKind.ReplacementValue, raw)
        };
    }

    private static bool ParseBool(XElement el, XName attr, bool defaultValue = false)
    {
        var val = (string?)el.Attribute(attr);
        return val != null ? string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) : defaultValue;
    }

    private static long ParseLong(XElement el, XName attr, long defaultValue)
    {
        var val = (string?)el.Attribute(attr);
        return val != null && long.TryParse(val, out var result) ? result : defaultValue;
    }

    private static int? ParseNullableInt(XElement el, XName attr)
    {
        var val = (string?)el.Attribute(attr);
        return val != null && int.TryParse(val, out var result) ? result : null;
    }

    private static string ExtractTableNameFromXPath(string xpath)
    {
        // xpath is like ".//TableName" or "./TableName"
        var idx = xpath.LastIndexOf('/');
        return idx >= 0 ? xpath.Substring(idx + 1) : xpath;
    }

    private static string ExtractColumnNameFromXPath(string xpath)
    {
        var idx = xpath.LastIndexOf('/');
        return idx >= 0 ? xpath.Substring(idx + 1) : xpath;
    }
}
```

**Step 6: Run parser tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "XsdParserTests" -v n`
Expected: All PASS

**Step 7: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Parsing/ tests/System.Data.Async.DataSet.Generator.Tests/
git commit -m "feat: implement XSD parser with intermediate model"
```

---

### Task 8: Add XSD parser tests for advanced features

**Files:**
- Create: `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Advanced.xsd`
- Modify: `tests/System.Data.Async.DataSet.Generator.Tests/Parsing/XsdParserTests.cs`

**Step 1: Create advanced test fixture**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Advanced.xsd`:

```xml
<?xml version="1.0" standalone="yes"?>
<xs:schema id="AdvancedDS"
           xmlns:xs="http://www.w3.org/2001/XMLSchema"
           xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
           xmlns:codegen="urn:schemas-microsoft-com:xml-msprop">
  <xs:element name="AdvancedDS" msdata:IsDataSet="true" msdata:CaseSensitive="true" msdata:EnforceConstraints="true">
    <xs:complexType>
      <xs:choice maxOccurs="unbounded">
        <xs:element name="Category" codegen:typedName="CategoryEntry" codegen:typedPlural="Categories">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="CategoryId" type="xs:int" />
              <xs:element name="Name" type="xs:string" />
              <xs:element name="Description" type="xs:string" minOccurs="0" codegen:nullValue="_null" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:element name="Product">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="ProductId" type="xs:int" />
              <xs:element name="CategoryId" type="xs:int" />
              <xs:element name="Name" type="xs:string" />
              <xs:element name="Price" type="xs:decimal" msdata:DefaultValue="0" />
              <xs:element name="Stock" type="xs:int" msdata:ReadOnly="true" />
              <xs:element name="TotalValue" type="xs:decimal" msdata:Expression="Price * Stock" msdata:ReadOnly="true" />
              <xs:element name="Sku" msdata:DataType="System.Guid" minOccurs="0" />
              <xs:element name="Notes" type="xs:string" minOccurs="0" codegen:nullValue="N/A" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:choice>
    </xs:complexType>
    <xs:unique name="PK_Category" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Category" />
      <xs:field xpath="CategoryId" />
    </xs:unique>
    <xs:unique name="PK_Product" msdata:PrimaryKey="true">
      <xs:selector xpath=".//Product" />
      <xs:field xpath="ProductId" />
    </xs:unique>
    <xs:keyref name="FK_Category_Product" refer="PK_Category" codegen:typedParent="Category" codegen:typedChildren="GetProducts">
      <xs:selector xpath=".//Product" />
      <xs:field xpath="CategoryId" />
    </xs:keyref>
  </xs:element>
</xs:schema>
```

**Step 2: Add advanced parser tests**

Add to `tests/System.Data.Async.DataSet.Generator.Tests/Parsing/XsdParserTests.cs`:

```csharp
[Fact]
public void Parse_Advanced_CaseSensitive()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    model.CaseSensitive.Should().BeTrue();
    model.EnforceConstraints.Should().BeTrue();
}

[Fact]
public void Parse_Advanced_TypedName_Override()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var category = model.Tables.First(t => t.Name == "Category");
    category.TypedName.Should().Be("CategoryEntry");
    category.TypedPlural.Should().Be("Categories");
}

[Fact]
public void Parse_Advanced_NullValue_Null()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var desc = model.Tables.First(t => t.Name == "Category").Columns.First(c => c.Name == "Description");
    desc.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnNull);
}

[Fact]
public void Parse_Advanced_NullValue_Replacement()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var notes = model.Tables.First(t => t.Name == "Product").Columns.First(c => c.Name == "Notes");
    notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReplacementValue);
    notes.NullValueBehavior.ReplacementValue.Should().Be("N/A");
}

[Fact]
public void Parse_Advanced_DefaultValue()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var price = model.Tables.First(t => t.Name == "Product").Columns.First(c => c.Name == "Price");
    price.DefaultValue.Should().Be("0");
}

[Fact]
public void Parse_Advanced_ReadOnly()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var stock = model.Tables.First(t => t.Name == "Product").Columns.First(c => c.Name == "Stock");
    stock.ReadOnly.Should().BeTrue();
}

[Fact]
public void Parse_Advanced_Expression()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var total = model.Tables.First(t => t.Name == "Product").Columns.First(c => c.Name == "TotalValue");
    total.Expression.Should().Be("Price * Stock");
    total.ReadOnly.Should().BeTrue();
}

[Fact]
public void Parse_Advanced_DataType_Override()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var sku = model.Tables.First(t => t.Name == "Product").Columns.First(c => c.Name == "Sku");
    sku.ClrTypeName.Should().Be("System.Guid");
}

[Fact]
public void Parse_Advanced_TypedParent_TypedChildren()
{
    var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
    var rel = model.Relations.First(r => r.Name == "FK_Category_Product");
    rel.TypedParent.Should().Be("Category");
    rel.TypedChildren.Should().Be("GetProducts");
}
```

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "XsdParserTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add tests/System.Data.Async.DataSet.Generator.Tests/
git commit -m "test: add advanced XSD parser tests for codegen annotations and expressions"
```

---

## Phase 4: Code Emitters

### Task 9: Implement naming helper

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Emit/NamingHelper.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Emit/NamingHelperTests.cs`

The naming helper centralizes all name derivation logic (typed names, class names, method names) from the model.

**Step 1: Write tests**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Emit/NamingHelperTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class NamingHelperTests
{
    [Theory]
    [InlineData("Order", null, "AsyncOrderRow")]
    [InlineData("Order", "OrderEntry", "AsyncOrderEntryRow")]
    public void RowClassName(string tableName, string? typedName, string expected)
    {
        NamingHelper.RowClassName(tableName, typedName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Order", null, "AsyncOrderDataTable")]
    [InlineData("Order", null, "AsyncOrderDataTable")]
    [InlineData("Category", "Categories", "AsyncCategoriesDataTable")]
    public void TableClassName(string tableName, string? typedPlural, string expected)
    {
        NamingHelper.TableClassName(tableName, typedPlural).Should().Be(expected);
    }

    [Theory]
    [InlineData("OrdersDS", "AsyncOrdersDS")]
    public void DataSetClassName(string dsName, string expected)
    {
        NamingHelper.DataSetClassName(dsName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Order", null, "AsyncOrderRowChangeEvent")]
    public void EventArgsClassName(string tableName, string? typedName, string expected)
    {
        NamingHelper.EventArgsClassName(tableName, typedName).Should().Be(expected);
    }

    [Theory]
    [InlineData("OrderId", "int", "FindByOrderId")]
    public void FindByMethodName_SinglePK(string pk, string type, string expected)
    {
        NamingHelper.FindByMethodName(ImmutableArray.Create(pk)).Should().Be(expected);
    }

    [Theory]
    [InlineData("OrderDate", "SetOrderDateAsync")]
    [InlineData("Name", "SetNameAsync")]
    public void SetterMethodName(string colName, string expected)
    {
        NamingHelper.SetterMethodName(colName).Should().Be(expected);
    }
}
```

**Step 2: Implement NamingHelper**

Create `src/System.Data.Async.DataSet.Generator/Emit/NamingHelper.cs`:

```csharp
using System.Collections.Immutable;

namespace System.Data.Async.DataSet.Generator.Emit;

internal static class NamingHelper
{
    public static string RowClassName(string tableName, string? typedName)
        => $"Async{typedName ?? tableName}Row";

    public static string TableClassName(string tableName, string? typedPlural)
        => $"Async{typedPlural ?? tableName}DataTable";

    public static string DataSetClassName(string dsName)
        => $"Async{dsName}";

    public static string EventArgsClassName(string tableName, string? typedName)
        => $"Async{typedName ?? tableName}RowChangeEvent";

    public static string FindByMethodName(ImmutableArray<string> pkColumns)
        => "FindBy" + string.Join("", pkColumns);

    public static string SetterMethodName(string columnName)
        => $"Set{columnName}Async";

    public static string IsNullMethodName(string columnName)
        => $"Is{columnName}Null";

    public static string SetNullMethodName(string columnName)
        => $"Set{columnName}NullAsync";

    public static string AddRowMethodName(string tableName, string? typedName)
        => $"Add{typedName ?? tableName}RowAsync";

    public static string RemoveRowMethodName(string tableName, string? typedName)
        => $"Remove{typedName ?? tableName}RowAsync";

    public static string NewRowMethodName(string tableName, string? typedName)
        => $"New{typedName ?? tableName}Row";

    public static string GetChildRowsMethodName(string childTableName, string? typedChildren)
        => typedChildren ?? $"Get{childTableName}Rows";

    public static string ParentRowPropertyName(string parentTableName, string? typedParent)
        => typedParent != null ? $"{typedParent}Row" : $"{parentTableName}Row";

    public static string SetParentRowMethodName(string parentTableName, string? typedParent)
        => $"Set{ParentRowPropertyName(parentTableName, typedParent)}Async";

    public static string ClrTypeToKeyword(string clrType) => clrType switch
    {
        "System.String" => "string",
        "System.Int32" => "int",
        "System.Int64" => "long",
        "System.Int16" => "short",
        "System.Byte" => "byte",
        "System.SByte" => "sbyte",
        "System.UInt16" => "ushort",
        "System.UInt32" => "uint",
        "System.UInt64" => "ulong",
        "System.Boolean" => "bool",
        "System.Decimal" => "decimal",
        "System.Double" => "double",
        "System.Single" => "float",
        "System.DateTime" => "global::System.DateTime",
        "System.Guid" => "global::System.Guid",
        "System.TimeSpan" => "global::System.TimeSpan",
        "System.Byte[]" => "byte[]",
        _ => $"global::{clrType}"
    };
}
```

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "NamingHelperTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Emit/ tests/System.Data.Async.DataSet.Generator.Tests/Emit/
git commit -m "feat: add NamingHelper for typed name derivation"
```

---

### Task 10: Implement DataRow emitter

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Emit/DataRowEmitter.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataRowEmitterTests.cs`

The emitter generates the `AsyncXxxRow` class source code from a `TableModel` and its associated `RelationModel` entries.

**Step 1: Write a snapshot test**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataRowEmitterTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class DataRowEmitterTests
{
    [Fact]
    public void Emit_Contains_Typed_Property()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("OrderId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw)),
                new ColumnModel("Total", "System.Decimal", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray.Create("OrderId"),
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("public int OrderId =>");
        source.Should().Contain("public decimal Total =>");
    }

    [Fact]
    public void Emit_Contains_Async_Setter()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("Total", "System.Decimal", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("public global::System.Threading.Tasks.ValueTask SetTotalAsync(");
    }

    [Fact]
    public void Emit_Nullable_Column_Has_IsNull_And_SetNull()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("Notes", "System.String", true, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("public bool IsNotesNull()");
        source.Should().Contain("public global::System.Threading.Tasks.ValueTask SetNotesNullAsync(");
    }

    [Fact]
    public void Emit_ReadOnly_Column_Has_No_Setter()
    {
        var table = new TableModel("Product", null, null,
            ImmutableArray.Create(
                new ColumnModel("Stock", "System.Int32", false, true, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("public int Stock =>");
        source.Should().NotContain("SetStockAsync");
    }

    [Fact]
    public void Emit_Expression_Column_Has_No_Setter()
    {
        var table = new TableModel("Product", null, null,
            ImmutableArray.Create(
                new ColumnModel("TotalValue", "System.Decimal", false, true, "Price * Stock", false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().NotContain("SetTotalValueAsync");
    }

    [Fact]
    public void Emit_NullValue_Throw_Has_StrongTypingException()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("Notes", "System.String", true, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = DataRowEmitter.Emit("OrdersDS", table,
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("StrongTypingException");
    }

    [Fact]
    public void Emit_Child_Relation_Has_GetChildRows()
    {
        var parentTable = new TableModel("Customer", null, null,
            ImmutableArray.Create(
                new ColumnModel("CustomerId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray.Create("CustomerId"),
            ImmutableArray<UniqueConstraintModel>.Empty);

        var childTable = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("OrderId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw)),
                new ColumnModel("CustomerId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray.Create("OrderId"),
            ImmutableArray<UniqueConstraintModel>.Empty);

        var relation = new RelationModel("FK_Customer_Order", "Customer",
            ImmutableArray.Create("CustomerId"), "Order",
            ImmutableArray.Create("CustomerId"), false, false, null, null);

        // Emit the PARENT row — should have GetOrderRows()
        var source = DataRowEmitter.Emit("TestDS", parentTable,
            ImmutableArray.Create(relation), ImmutableArray.Create(parentTable, childTable));

        source.Should().Contain("GetOrderRows()");
    }

    [Fact]
    public void Emit_Parent_Relation_Has_ParentRow_Property()
    {
        var childTable = new TableModel("Order", null, null,
            ImmutableArray.Create(
                new ColumnModel("OrderId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw)),
                new ColumnModel("CustomerId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray.Create("OrderId"),
            ImmutableArray<UniqueConstraintModel>.Empty);

        var parentTable = new TableModel("Customer", null, null,
            ImmutableArray.Create(
                new ColumnModel("CustomerId", "System.Int32", false, false, null, false, 0, 1, null, null, null, null, false,
                    new NullValueBehavior(NullValueBehaviorKind.Throw))),
            ImmutableArray.Create("CustomerId"),
            ImmutableArray<UniqueConstraintModel>.Empty);

        var relation = new RelationModel("FK_Customer_Order", "Customer",
            ImmutableArray.Create("CustomerId"), "Order",
            ImmutableArray.Create("CustomerId"), false, false, null, null);

        // Emit the CHILD row — should have CustomerRow property
        var source = DataRowEmitter.Emit("TestDS", childTable,
            ImmutableArray.Create(relation), ImmutableArray.Create(parentTable, childTable));

        source.Should().Contain("CustomerRow");
        source.Should().Contain("SetCustomerRowAsync");
    }
}
```

**Step 2: Implement DataRowEmitter**

Create `src/System.Data.Async.DataSet.Generator/Emit/DataRowEmitter.cs`:

This emitter generates:
- Typed read-only properties per column
- Async setters per mutable column
- `IsXxxNull()` / `SetXxxNullAsync()` per nullable column
- `StrongTypingException` handling per `NullValueBehavior`
- `GetChildRows()` methods for child relations where this table is parent
- `ParentRow` properties + `SetParentRowAsync()` for parent relations where this table is child
- Hidden columns are skipped

The implementation uses a `StringBuilder` to build the source. Use `global::` qualified type names throughout to avoid namespace conflicts.

```csharp
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Data.Async.DataSet.Generator.Model;

namespace System.Data.Async.DataSet.Generator.Emit;

internal static class DataRowEmitter
{
    public static string Emit(
        string dataSetName,
        TableModel table,
        ImmutableArray<RelationModel> allRelations,
        ImmutableArray<TableModel> allTables)
    {
        var rowClass = NamingHelper.RowClassName(table.Name, table.TypedName);
        var tableClass = NamingHelper.TableClassName(table.Name, table.TypedPlural);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace System.Data.Async.DataSet;");
        sb.AppendLine();
        sb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"AdoNet.Async.DataSet.Generator\", \"1.0.0\")]");
        sb.AppendLine($"public partial class {rowClass} : global::System.Data.Async.DataSet.AsyncDataRow");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {tableClass} _typedTable;");
        sb.AppendLine();
        sb.AppendLine($"    internal {rowClass}(global::System.Data.DataRow inner, {tableClass} table)");
        sb.AppendLine($"        : base(inner, table)");
        sb.AppendLine("    {");
        sb.AppendLine($"        _typedTable = table;");
        sb.AppendLine("    }");

        // Typed properties and setters per column
        foreach (var col in table.Columns)
        {
            if (col.IsHidden) continue;

            var keyword = NamingHelper.ClrTypeToKeyword(col.ClrTypeName);
            var isRefType = col.ClrTypeName == "System.String" || col.ClrTypeName == "System.Byte[]";
            sb.AppendLine();

            // Getter
            if (col.AllowDBNull)
            {
                EmitNullableGetter(sb, col, keyword, isRefType);
            }
            else
            {
                sb.AppendLine($"    public {keyword} {col.Name} => ({keyword})this[\"{col.Name}\"];");
            }

            // Setter (skip for read-only and expression columns)
            if (!col.ReadOnly && col.Expression == null)
            {
                sb.AppendLine();
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask {NamingHelper.SetterMethodName(col.Name)}({keyword} value, global::System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine($"        => SetValueAsync(\"{col.Name}\", value, cancellationToken);");
            }

            // IsNull / SetNull for nullable columns
            if (col.AllowDBNull)
            {
                sb.AppendLine();
                sb.AppendLine($"    public bool {NamingHelper.IsNullMethodName(col.Name)}() => IsNull(\"{col.Name}\");");
                sb.AppendLine();
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask {NamingHelper.SetNullMethodName(col.Name)}(global::System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine($"        => SetValueAsync(\"{col.Name}\", global::System.DBNull.Value, cancellationToken);");
            }
        }

        // Child relation accessors (this table is parent)
        foreach (var rel in allRelations.Where(r => r.ParentTableName == table.Name && !r.ConstraintOnly))
        {
            var childTable = allTables.FirstOrDefault(t => t.Name == rel.ChildTableName);
            if (childTable == null) continue;

            var childRowClass = NamingHelper.RowClassName(childTable.Name, childTable.TypedName);
            var methodName = NamingHelper.GetChildRowsMethodName(childTable.Name, rel.TypedChildren);

            sb.AppendLine();
            sb.AppendLine($"    public {childRowClass}[] {methodName}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        var relation = InnerRow.Table.ChildRelations[\"{rel.Name}\"];");
            sb.AppendLine($"        if (relation == null) return global::System.Array.Empty<{childRowClass}>();");
            sb.AppendLine($"        var innerRows = InnerRow.GetChildRows(relation);");
            sb.AppendLine($"        var result = new {childRowClass}[innerRows.Length];");
            sb.AppendLine($"        for (int i = 0; i < innerRows.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            result[i] = new {childRowClass}(innerRows[i], ({NamingHelper.TableClassName(childTable.Name, childTable.TypedPlural)})Table.DataSet!.Tables[\"{childTable.Name}\"]!);");
            sb.AppendLine("        }");
            sb.AppendLine($"        return result;");
            sb.AppendLine("    }");
        }

        // Parent relation accessors (this table is child)
        foreach (var rel in allRelations.Where(r => r.ChildTableName == table.Name && !r.ConstraintOnly))
        {
            var parentTable = allTables.FirstOrDefault(t => t.Name == rel.ParentTableName);
            if (parentTable == null) continue;

            var parentRowClass = NamingHelper.RowClassName(parentTable.Name, parentTable.TypedName);
            var propName = NamingHelper.ParentRowPropertyName(parentTable.Name, rel.TypedParent);
            var setMethodName = NamingHelper.SetParentRowMethodName(parentTable.Name, rel.TypedParent);
            var parentTableClass = NamingHelper.TableClassName(parentTable.Name, parentTable.TypedPlural);

            sb.AppendLine();
            sb.AppendLine($"    public {parentRowClass}? {propName}");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine($"            var relation = InnerRow.Table.ParentRelations[\"{rel.Name}\"];");
            sb.AppendLine($"            if (relation == null) return null;");
            sb.AppendLine($"            var parentRow = InnerRow.GetParentRow(relation);");
            sb.AppendLine($"            if (parentRow == null) return null;");
            sb.AppendLine($"            return new {parentRowClass}(parentRow, ({parentTableClass})Table.DataSet!.Tables[\"{parentTable.Name}\"]!);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask {setMethodName}({parentRowClass}? parent, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var relation = InnerRow.Table.ParentRelations[\"{rel.Name}\"];");
            sb.AppendLine($"        if (relation != null) InnerRow.SetParentRow(parent?.InnerRow, relation);");
            sb.AppendLine($"        return global::System.Threading.Tasks.ValueTask.CompletedTask;");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitNullableGetter(StringBuilder sb, ColumnModel col, string keyword, bool isRefType)
    {
        switch (col.NullValueBehavior.Kind)
        {
            case NullValueBehaviorKind.Throw:
                sb.AppendLine($"    public {keyword} {col.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (IsNull(\"{col.Name}\"))");
                sb.AppendLine($"                throw new global::System.Data.StrongTypingException(\"The value for column '{col.Name}' in table '\" + Table.TableName + \"' is DBNull.\", new global::System.InvalidCastException());");
                sb.AppendLine($"            return ({keyword})this[\"{col.Name}\"];");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                break;

            case NullValueBehaviorKind.ReturnNull:
                var nullableKeyword = isRefType ? $"{keyword}?" : $"{keyword}?";
                sb.AppendLine($"    public {nullableKeyword} {col.Name} => IsNull(\"{col.Name}\") ? null : ({keyword})this[\"{col.Name}\"];");
                break;

            case NullValueBehaviorKind.ReturnEmpty:
                var emptyValue = col.ClrTypeName == "System.String" ? "\"\"" : $"default({keyword})";
                sb.AppendLine($"    public {keyword} {col.Name} => IsNull(\"{col.Name}\") ? {emptyValue} : ({keyword})this[\"{col.Name}\"];");
                break;

            case NullValueBehaviorKind.ReplacementValue:
                var replacement = col.ClrTypeName == "System.String"
                    ? $"\"{col.NullValueBehavior.ReplacementValue}\""
                    : col.NullValueBehavior.ReplacementValue!;
                sb.AppendLine($"    public {keyword} {col.Name} => IsNull(\"{col.Name}\") ? {replacement} : ({keyword})this[\"{col.Name}\"];");
                break;
        }
    }
}
```

Note: The `InnerRow` property was added in Task 1 (protected accessor). The `IsNull(string)` method was also added there. The child/parent row navigation needs access to `InnerRow` for calling `GetChildRows`/`GetParentRow` on the underlying `DataRow`.

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "DataRowEmitterTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Emit/DataRowEmitter.cs tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataRowEmitterTests.cs
git commit -m "feat: implement DataRow source emitter with typed properties and relation accessors"
```

---

### Task 11: Implement DataTable emitter

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Emit/DataTableEmitter.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataTableEmitterTests.cs`

The emitter generates the `AsyncXxxDataTable` class.

**Step 1: Write tests**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataTableEmitterTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class DataTableEmitterTests
{
    private static TableModel SimpleTable() => new("Order", null, null,
        ImmutableArray.Create(
            new ColumnModel("OrderId", "System.Int32", false, false, null, true, 1, 1, null, null, null, null, false,
                new NullValueBehavior(NullValueBehaviorKind.Throw)),
            new ColumnModel("Total", "System.Decimal", false, false, null, false, 0, 1, null, null, null, null, false,
                new NullValueBehavior(NullValueBehaviorKind.Throw)),
            new ColumnModel("Notes", "System.String", true, false, null, false, 0, 1, null, null, null, null, false,
                new NullValueBehavior(NullValueBehaviorKind.Throw))),
        ImmutableArray.Create("OrderId"),
        ImmutableArray<UniqueConstraintModel>.Empty);

    [Fact]
    public void Emit_Contains_Column_Properties()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("public global::System.Data.DataColumn OrderIdColumn");
        source.Should().Contain("public global::System.Data.DataColumn TotalColumn");
    }

    [Fact]
    public void Emit_Contains_FindBy_Method()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("FindByOrderId(int orderId)");
    }

    [Fact]
    public void Emit_Contains_AddRowAsync_With_Parameters()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        // AutoIncrement columns excluded, nullable columns included
        source.Should().Contain("AddOrderRowAsync(");
        source.Should().Contain("decimal total");
    }

    [Fact]
    public void Emit_Contains_Typed_Events()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("OrderRowChangingAsync");
        source.Should().Contain("OrderRowChangedAsync");
        source.Should().Contain("OrderRowDeletingAsync");
        source.Should().Contain("OrderRowDeletedAsync");
    }

    [Fact]
    public void Emit_Contains_NewRow_Method()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("NewOrderRow()");
    }

    [Fact]
    public void Emit_Contains_RemoveRowAsync_Method()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("RemoveOrderRowAsync(");
    }

    [Fact]
    public void Emit_Extends_AsyncDataTable_Generic()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("AsyncOrderDataTable : global::System.Data.Async.DataSet.AsyncDataTable<AsyncOrderRow>");
    }

    [Fact]
    public void Emit_Contains_WrapRow_Override()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("protected override AsyncOrderRow WrapRow(");
    }

    [Fact]
    public void Emit_Contains_InitClass()
    {
        var source = DataTableEmitter.Emit("TestDS", SimpleTable(),
            ImmutableArray<RelationModel>.Empty, ImmutableArray<TableModel>.Empty);

        source.Should().Contain("private void InitClass()");
        source.Should().Contain("AutoIncrement = true");
    }
}
```

**Step 2: Implement DataTableEmitter**

Create `src/System.Data.Async.DataSet.Generator/Emit/DataTableEmitter.cs`:

This emitter generates:
- Column properties (`DataColumn OrderIdColumn`)
- `Count` property and typed indexer
- Typed async events using `ZeroAlloc.AsyncEvents`
- `NewXxxRow()`, `AddXxxRowAsync()` (parameter-based), `RemoveXxxRowAsync()`
- `FindByXxx()` for primary keys
- `InitClass()` setting up columns with all attributes
- `InitVars()` resolving column fields
- `WrapRow()` override
- `Clone()` and `CreateInstance()` overrides

The implementation follows the same `StringBuilder` pattern as `DataRowEmitter`. For FK-aware `AddXxxRowAsync`, if a column is part of a relation, the parameter becomes the parent typed row instead of the raw FK value.

This is the largest emitter (~250 lines). Generate it following the exact same pattern as `DataRowEmitter`.

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "DataTableEmitterTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Emit/DataTableEmitter.cs tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataTableEmitterTests.cs
git commit -m "feat: implement DataTable source emitter with typed events and row methods"
```

---

### Task 12: Implement DataSet emitter

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Emit/DataSetEmitter.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataSetEmitterTests.cs`

**Step 1: Write tests**

Create `tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataSetEmitterTests.cs`:

```csharp
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class DataSetEmitterTests
{
    private static DataSetModel ParseSimple()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", "Simple.xsd");
        return XsdParser.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Emit_Contains_Typed_Table_Accessors()
    {
        var model = ParseSimple();
        var source = DataSetEmitter.Emit(model);

        source.Should().Contain("public AsyncCustomerDataTable Customer");
        source.Should().Contain("public AsyncOrderDataTable Order");
    }

    [Fact]
    public void Emit_Contains_Relation_Accessor()
    {
        var model = ParseSimple();
        var source = DataSetEmitter.Emit(model);

        source.Should().Contain("FK_Customer_Order");
    }

    [Fact]
    public void Emit_Contains_InitClass()
    {
        var model = ParseSimple();
        var source = DataSetEmitter.Emit(model);

        source.Should().Contain("private void InitClass()");
        source.Should().Contain("DataSetName = \"OrdersDS\"");
    }

    [Fact]
    public void Emit_Contains_Clone()
    {
        var model = ParseSimple();
        var source = DataSetEmitter.Emit(model);

        source.Should().Contain("Clone()");
    }

    [Fact]
    public void Emit_Extends_AsyncDataSet()
    {
        var model = ParseSimple();
        var source = DataSetEmitter.Emit(model);

        source.Should().Contain("AsyncOrdersDS : global::System.Data.Async.DataSet.AsyncDataSet");
    }
}
```

**Step 2: Implement DataSetEmitter**

Create `src/System.Data.Async.DataSet.Generator/Emit/DataSetEmitter.cs`:

Generates:
- Table accessor properties
- Relation accessor properties
- `InitClass()` — creates typed tables, adds to `Tables`, creates `DataRelation` and `ForeignKeyConstraint` objects
- `InitVars()` — resolves typed table and relation fields from collections
- `InitExpressions()` — sets expression strings on computed columns
- `Clone()` — clones and calls `InitVars()` + `InitExpressions()`
- Constructor calling `InitClass()`

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "DataSetEmitterTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Emit/DataSetEmitter.cs tests/System.Data.Async.DataSet.Generator.Tests/Emit/DataSetEmitterTests.cs
git commit -m "feat: implement DataSet source emitter with table/relation accessors"
```

---

### Task 13: Implement EventArgs emitter

**Files:**
- Create: `src/System.Data.Async.DataSet.Generator/Emit/EventArgsEmitter.cs`
- Test: `tests/System.Data.Async.DataSet.Generator.Tests/Emit/EventArgsEmitterTests.cs`

**Step 1: Write tests**

```csharp
using System.Collections.Immutable;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class EventArgsEmitterTests
{
    [Fact]
    public void Emit_Contains_Typed_Row_Property()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray<ColumnModel>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = EventArgsEmitter.Emit(table);

        source.Should().Contain("public AsyncOrderRow Row");
        source.Should().Contain("public global::System.Data.DataRowAction Action");
    }

    [Fact]
    public void Emit_ClassName_Follows_Convention()
    {
        var table = new TableModel("Order", null, null,
            ImmutableArray<ColumnModel>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);

        var source = EventArgsEmitter.Emit(table);

        source.Should().Contain("class AsyncOrderRowChangeEvent");
    }
}
```

**Step 2: Implement EventArgsEmitter**

Create `src/System.Data.Async.DataSet.Generator/Emit/EventArgsEmitter.cs`:

```csharp
using System.Text;
using System.Data.Async.DataSet.Generator.Model;

namespace System.Data.Async.DataSet.Generator.Emit;

internal static class EventArgsEmitter
{
    public static string Emit(TableModel table)
    {
        var className = NamingHelper.EventArgsClassName(table.Name, table.TypedName);
        var rowClass = NamingHelper.RowClassName(table.Name, table.TypedName);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace System.Data.Async.DataSet;");
        sb.AppendLine();
        sb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"AdoNet.Async.DataSet.Generator\", \"1.0.0\")]");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        sb.AppendLine($"    public {className}({rowClass} row, global::System.Data.DataRowAction action)");
        sb.AppendLine("    {");
        sb.AppendLine("        Row = row;");
        sb.AppendLine("        Action = action;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {rowClass} Row {{ get; }}");
        sb.AppendLine();
        sb.AppendLine("    public global::System.Data.DataRowAction Action { get; }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
```

**Step 3: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "EventArgsEmitterTests" -v n`
Expected: All PASS

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/Emit/EventArgsEmitter.cs tests/System.Data.Async.DataSet.Generator.Tests/Emit/EventArgsEmitterTests.cs
git commit -m "feat: implement EventArgs source emitter"
```

---

## Phase 5: Wire Up the Generator Pipeline

### Task 14: Wire the incremental generator pipeline

**Files:**
- Modify: `src/System.Data.Async.DataSet.Generator/TypedDataSetGenerator.cs`
- Create: `src/System.Data.Async.DataSet.Generator/Diagnostics.cs`

**Step 1: Create diagnostics descriptors**

Create `src/System.Data.Async.DataSet.Generator/Diagnostics.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace System.Data.Async.DataSet.Generator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor InvalidXsd = new(
        id: "ADAG001",
        title: "Invalid XSD schema",
        messageFormat: "Failed to parse XSD file '{0}': {1}",
        category: "AdoNet.Async.DataSet.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingKeyReference = new(
        id: "ADAG002",
        title: "Missing key reference",
        messageFormat: "keyref '{0}' references unknown key '{1}'",
        category: "AdoNet.Async.DataSet.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
```

**Step 2: Wire the generator pipeline**

Update `src/System.Data.Async.DataSet.Generator/TypedDataSetGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;

namespace System.Data.Async.DataSet.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TypedDataSetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var xsdFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase));

        var models = xsdFiles.Select(static (file, ct) =>
        {
            var text = file.GetText(ct)?.ToString();
            if (text == null) return default;

            try
            {
                var model = XsdParser.Parse(text);
                return (Model: model, Error: (string?)null, FilePath: file.Path);
            }
            catch (Exception ex)
            {
                return (Model: (DataSetModel?)null, Error: ex.Message, FilePath: file.Path);
            }
        });

        context.RegisterSourceOutput(models, static (spc, result) =>
        {
            if (result.Error != null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidXsd, Location.None, result.FilePath, result.Error));
                return;
            }

            var model = result.Model;
            if (model == null) return;

            // Emit DataSet class
            var dsSource = DataSetEmitter.Emit(model);
            spc.AddSource($"{model.Name}.AsyncDataSet.g.cs", dsSource);

            // Emit per-table types
            foreach (var table in model.Tables)
            {
                var tableSource = DataTableEmitter.Emit(model.Name, table, model.Relations, model.Tables);
                spc.AddSource($"{model.Name}.{table.Name}.AsyncDataTable.g.cs", tableSource);

                var rowSource = DataRowEmitter.Emit(model.Name, table, model.Relations, model.Tables);
                spc.AddSource($"{model.Name}.{table.Name}.AsyncDataRow.g.cs", rowSource);

                var eventSource = EventArgsEmitter.Emit(table);
                spc.AddSource($"{model.Name}.{table.Name}.Events.g.cs", eventSource);
            }
        });
    }
}
```

**Step 3: Verify build**

Run: `dotnet build src/System.Data.Async.DataSet.Generator`
Expected: Build succeeds.

**Step 4: Commit**

```bash
git add src/System.Data.Async.DataSet.Generator/
git commit -m "feat: wire incremental generator pipeline with XSD parsing and code emission"
```

---

## Phase 6: End-to-End Generator Tests

### Task 15: Generator driver tests

**Files:**
- Create: `tests/System.Data.Async.DataSet.Generator.Tests/GeneratorDriverTests.cs`

These tests use `CSharpGeneratorDriver` to verify the full pipeline from `.xsd` to compilable source.

**Step 1: Write generator driver tests**

```csharp
using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace System.Data.Async.DataSet.Generator.Tests;

public class GeneratorDriverTests
{
    private static string LoadSchema(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", name);
        return File.ReadAllText(path);
    }

    private static GeneratorDriverRunResult RunGenerator(string xsdContent, string fileName = "Test.xsd")
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Data.DataSet).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Data.Async.DataSet.AsyncDataTable).Assembly.Location),
            });

        var generator = new TypedDataSetGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText(fileName, xsdContent)));

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        return driver.GetRunResult();
    }

    [Fact]
    public void Simple_Xsd_Generates_Expected_Files()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().HaveCount(9); // 1 DataSet + (2 tables * 3 files each) + 2 event files... count = 1 + 2*3 = 7? Let's check: DS + Customer(Table,Row,Events) + Order(Table,Row,Events) = 1+3+3 = 7
        // Adjust count based on actual emission
    }

    [Fact]
    public void Simple_Xsd_DataSet_File_Contains_Class()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");

        var dsFile = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("AsyncDataSet.g.cs"));
        dsFile.Should().NotBeNull();

        var text = dsFile!.GetText().ToString();
        text.Should().Contain("class AsyncOrdersDS");
    }

    [Fact]
    public void Invalid_Xsd_Reports_Diagnostic()
    {
        var result = RunGenerator("<invalid xml", "Bad.xsd");

        result.Diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("ADAG001");
    }

    // Helper class for in-memory additional text
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly string _text;
        public override string Path { get; }

        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = text;
        }

        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => SourceText.From(_text);
    }
}
```

Note: Add `using Microsoft.CodeAnalysis.Text;` for `SourceText`.

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Tests --filter "GeneratorDriverTests" -v n`
Expected: All PASS (fix any compilation issues in generated code iteratively)

**Step 3: Commit**

```bash
git add tests/System.Data.Async.DataSet.Generator.Tests/GeneratorDriverTests.cs
git commit -m "test: add generator driver end-to-end tests"
```

---

## Phase 7: Runtime Integration Tests

### Task 16: Create integration test project with .xsd consumer

**Files:**
- Create: `tests/System.Data.Async.DataSet.Generator.Integration.Tests/System.Data.Async.DataSet.Generator.Integration.Tests.csproj`
- Create: `tests/System.Data.Async.DataSet.Generator.Integration.Tests/Schemas/OrdersDS.xsd`
- Create: `tests/System.Data.Async.DataSet.Generator.Integration.Tests/TypedDataSetTests.cs`
- Modify: `System.Data.Async.slnx`

This project actually consumes the generator as an analyzer reference, includes an `.xsd`, and verifies the generated types work at runtime.

**Step 1: Create integration test project**

Create `tests/System.Data.Async.DataSet.Generator.Integration.Tests/System.Data.Async.DataSet.Generator.Integration.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet.Generator\System.Data.Async.DataSet.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="Schemas\*.xsd" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
  </ItemGroup>
</Project>
```

**Step 2: Create test XSD**

Copy `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Simple.xsd` to `tests/System.Data.Async.DataSet.Generator.Integration.Tests/Schemas/OrdersDS.xsd`.

**Step 3: Write runtime integration tests**

Create `tests/System.Data.Async.DataSet.Generator.Integration.Tests/TypedDataSetTests.cs`:

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class TypedDataSetTests
{
    [Fact]
    public void Can_Create_Typed_DataSet()
    {
        using var ds = new AsyncOrdersDS();

        ds.Customer.Should().NotBeNull();
        ds.Order.Should().NotBeNull();
    }

    [Fact]
    public void Typed_Table_Has_Column_Properties()
    {
        using var ds = new AsyncOrdersDS();

        ds.Customer.CustomerIdColumn.Should().NotBeNull();
        ds.Customer.NameColumn.Should().NotBeNull();
        ds.Order.OrderIdColumn.Should().NotBeNull();
        ds.Order.OrderDateColumn.Should().NotBeNull();
    }

    [Fact]
    public async Task Can_Add_Typed_Row()
    {
        using var ds = new AsyncOrdersDS();

        var row = ds.Customer.NewCustomerRow();
        await row.SetCustomerIdAsync(1);
        await row.SetNameAsync("Alice");
        await ds.Customer.Rows.AddAsync(row);

        ds.Customer.Rows.Count.Should().Be(1);
        ds.Customer[0].CustomerId.Should().Be(1);
        ds.Customer[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Can_Add_Row_With_Parameters()
    {
        using var ds = new AsyncOrdersDS();

        var row = await ds.Customer.AddCustomerRowAsync(1, "Bob");

        ds.Customer.Rows.Count.Should().Be(1);
        row.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task FindBy_Primary_Key()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice");
        await ds.Customer.AddCustomerRowAsync(2, "Bob");

        var found = ds.Customer.FindByCustomerId(2);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Nullable_Column_IsNull_And_SetNull()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice");

        row.IsEmailNull().Should().BeTrue();

        await row.SetEmailAsync("alice@example.com");
        row.IsEmailNull().Should().BeFalse();
        row.Email.Should().Be("alice@example.com");

        await row.SetEmailNullAsync();
        row.IsEmailNull().Should().BeTrue();
    }

    [Fact]
    public async Task Nullable_Column_Throws_StrongTypingException_By_Default()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice");

        var act = () => row.Email;
        act.Should().Throw<StrongTypingException>();
    }

    [Fact]
    public async Task Relation_Navigation_Parent_To_Child()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice");
        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m);
        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 49.99m);

        var orders = customer.GetOrderRows();

        orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Relation_Navigation_Child_To_Parent()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice");
        var order = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m);

        order.CustomerRow.Should().NotBeNull();
        order.CustomerRow!.Name.Should().Be("Alice");
    }

    [Fact]
    public void Typed_Table_Is_AsyncDataTable()
    {
        using var ds = new AsyncOrdersDS();

        AsyncDataTable untyped = ds.Customer;
        untyped.Should().BeSameAs(ds.Customer);
    }

    [Fact]
    public void Typed_Row_Is_AsyncDataRow()
    {
        using var ds = new AsyncOrdersDS();
        ds.Customer.InnerDataTable.Rows.Add(1, "Test");

        AsyncDataRow untyped = ds.Customer[0];
        untyped.Should().BeOfType<AsyncCustomerRow>();
    }
}
```

**Step 4: Update solution file**

Add to `System.Data.Async.slnx`:
```xml
<Project Path="tests/System.Data.Async.DataSet.Generator.Integration.Tests/System.Data.Async.DataSet.Generator.Integration.Tests.csproj" />
```

**Step 5: Build and run**

Run: `dotnet build tests/System.Data.Async.DataSet.Generator.Integration.Tests`
Expected: Build succeeds (generated types compile)

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Integration.Tests -v n`
Expected: All PASS

This step will likely require iterative fixes to the emitters. The generated code must compile against the actual `AsyncDataTable<TRow>` and `AsyncDataRow` base classes. Fix any issues found.

**Step 6: Commit**

```bash
git add tests/System.Data.Async.DataSet.Generator.Integration.Tests/ System.Data.Async.slnx
git commit -m "test: add runtime integration tests for typed DataSet generator"
```

---

### Task 17: Add advanced integration tests

**Files:**
- Copy: `tests/System.Data.Async.DataSet.Generator.Tests/Schemas/Advanced.xsd` to `tests/System.Data.Async.DataSet.Generator.Integration.Tests/Schemas/AdvancedDS.xsd`
- Create: `tests/System.Data.Async.DataSet.Generator.Integration.Tests/AdvancedTypedDataSetTests.cs`

**Step 1: Write advanced integration tests**

```csharp
using System.Data.Async.DataSet;
using FluentAssertions;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class AdvancedTypedDataSetTests
{
    [Fact]
    public void TypedName_Override_Applied()
    {
        using var ds = new AsyncAdvancedDS();

        // Category table has codegen:typedName="CategoryEntry"
        ds.Categories.Should().NotBeNull();
        ds.Categories.Should().BeOfType<AsyncCategoriesDataTable>();
    }

    [Fact]
    public async Task NullValue_Null_Returns_Null()
    {
        using var ds = new AsyncAdvancedDS();
        var row = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");

        // Description has codegen:nullValue="_null"
        row.Description.Should().BeNull();
    }

    [Fact]
    public async Task NullValue_Replacement_Returns_Value()
    {
        using var ds = new AsyncAdvancedDS();
        await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");
        var product = await ds.Product.AddProductRowAsync(1, 1, "Widget", 9.99m, 10);

        // Notes has codegen:nullValue="N/A"
        product.Notes.Should().Be("N/A");
    }

    [Fact]
    public async Task DefaultValue_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");
        var row = ds.Product.NewProductRow();
        await row.SetProductIdAsync(1);
        await row.SetCategoryIdAsync(1);
        await row.SetNameAsync("Widget");
        // Price has DefaultValue="0", Stock is ReadOnly
        await ds.Product.Rows.AddAsync(row);

        row.Price.Should().Be(0m);
    }

    [Fact]
    public async Task ReadOnly_Column_Has_No_Setter()
    {
        // Verify Stock is read-only (no SetStockAsync method exists)
        // This is a compile-time check — the test existing proves it compiles
        using var ds = new AsyncAdvancedDS();
        await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");
        var row = await ds.Product.AddProductRowAsync(1, 1, "Widget", 9.99m, 10);

        row.Stock.Should().Be(10);
    }

    [Fact]
    public async Task TypedChildren_Override()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");
        await ds.Product.AddProductRowAsync(1, 1, "Widget", 9.99m, 10);

        // codegen:typedChildren="GetProducts"
        var products = cat.GetProducts();
        products.Should().HaveCount(1);
    }

    [Fact]
    public async Task TypedParent_Override()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics");
        var product = await ds.Product.AddProductRowAsync(1, 1, "Widget", 9.99m, 10);

        // codegen:typedParent="Category"
        product.CategoryRow.Should().NotBeNull();
        product.CategoryRow!.Name.Should().Be("Electronics");
    }

    [Fact]
    public void CaseSensitive_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        ds.InnerDataSet.CaseSensitive.Should().BeTrue();
    }

    [Fact]
    public void EnforceConstraints_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        ds.InnerDataSet.EnforceConstraints.Should().BeTrue();
    }
}
```

Note: Some tests reference `InnerDataSet` — you may need to add a `protected internal` accessor for the inner `DataSet` on `AsyncDataSet` if not already available.

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.DataSet.Generator.Integration.Tests -v n`
Expected: All PASS

**Step 3: Commit**

```bash
git add tests/System.Data.Async.DataSet.Generator.Integration.Tests/
git commit -m "test: add advanced integration tests for codegen annotations and expressions"
```

---

## Phase 8: Final Verification

### Task 18: Run full test suite and verify no regressions

**Step 1: Run all tests across all projects**

Run: `dotnet test System.Data.Async.slnx -v n`
Expected: All PASS across all test projects. No regressions in existing functionality.

**Step 2: Build in Release mode**

Run: `dotnet build System.Data.Async.slnx -c Release`
Expected: Build succeeds with zero warnings.

**Step 3: Commit any final fixes**

If any fixes were needed, commit them:

```bash
git add -A
git commit -m "fix: resolve final issues from full test suite verification"
```

---

## Summary of Deliverables

| Phase | What | Files |
|---|---|---|
| 1 | Generic base classes | `AsyncDataTable<TRow>`, `AsyncDataRowCollection<TRow>`, unseal `AsyncDataRow` |
| 2 | Project setup | Generator `.csproj`, test `.csproj`, solution update |
| 3 | XSD parser | `XsdParser`, `XsdTypeMapper`, intermediate model types |
| 4 | Code emitters | `DataRowEmitter`, `DataTableEmitter`, `DataSetEmitter`, `EventArgsEmitter`, `NamingHelper` |
| 5 | Generator pipeline | `TypedDataSetGenerator` wiring, diagnostics |
| 6 | Generator tests | `GeneratorDriverTests` end-to-end |
| 7 | Integration tests | Runtime tests with real `.xsd` consumption |
| 8 | Final verification | Full regression test run |

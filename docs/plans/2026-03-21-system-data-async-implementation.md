# System.Data.Async Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a full async-first drop-in replacement for System.Data targeting .NET 10, with JSON deserialization compatibility via Json.Net.DataSetConverters format.

**Architecture:** Three NuGet packages — core abstractions (zero deps), async DataSet/DataTable with JSON converters (Newtonsoft.Json dep), and adapters wrapping existing ADO.NET providers. Mirror & Wrap approach: async interfaces mirror System.Data, adapters delegate to existing DbConnection/DbCommand/DbDataReader.

**Tech Stack:** .NET 10, C# preview, Newtonsoft.Json, xUnit, NSubstitute, Meziantou.Analyzer, Roslynator

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `System.Data.Async.slnx`
- Create: `Directory.Build.props`
- Create: `Directory.Build.targets`
- Create: `.editorconfig`
- Create: `src/System.Data.Async/System.Data.Async.csproj`
- Create: `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj`
- Create: `src/System.Data.Async.Adapters/System.Data.Async.Adapters.csproj`
- Create: `tests/System.Data.Async.Tests/System.Data.Async.Tests.csproj`
- Create: `tests/System.Data.Async.DataSet.Tests/System.Data.Async.DataSet.Tests.csproj`
- Create: `tests/System.Data.Async.Adapters.Tests/System.Data.Async.Adapters.Tests.csproj`

**Step 1: Create directory structure**

```bash
mkdir -p src/System.Data.Async
mkdir -p src/System.Data.Async.DataSet
mkdir -p src/System.Data.Async.Adapters
mkdir -p tests/System.Data.Async.Tests
mkdir -p tests/System.Data.Async.DataSet.Tests
mkdir -p tests/System.Data.Async.Adapters.Tests
```

**Step 2: Create Directory.Build.props**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>

  <PropertyGroup>
    <Authors>Marcel Roozekrans</Authors>
    <Company>Marcel Roozekrans</Company>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Meziantou.Analyzer" Version="2.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Roslynator.Analyzers" Version="4.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Step 3: Create .editorconfig**

Standard .NET .editorconfig with severity overrides for Meziantou rules as needed.

**Step 4: Create core project — `src/System.Data.Async/System.Data.Async.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async</RootNamespace>
    <PackageId>System.Data.Async</PackageId>
    <Description>Async-first interfaces and base classes for System.Data. Drop-in replacement with modern async support.</Description>
    <PackageTags>data;async;ado.net;database</PackageTags>
  </PropertyGroup>
</Project>
```

**Step 5: Create DataSet project — `src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.DataSet</RootNamespace>
    <PackageId>System.Data.Async.DataSet</PackageId>
    <Description>Async DataSet and DataTable with full Json.Net.DataSetConverters JSON compatibility.</Description>
    <PackageTags>data;async;dataset;datatable;json</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async\System.Data.Async.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.*" />
  </ItemGroup>
</Project>
```

**Step 6: Create Adapters project — `src/System.Data.Async.Adapters/System.Data.Async.Adapters.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>System.Data.Async.Adapters</RootNamespace>
    <PackageId>System.Data.Async.Adapters</PackageId>
    <Description>Adapters wrapping existing ADO.NET providers (DbConnection, DbCommand, DbDataReader) into System.Data.Async interfaces.</Description>
    <PackageTags>data;async;adapter;ado.net</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\System.Data.Async\System.Data.Async.csproj" />
    <ProjectReference Include="..\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.*" />
  </ItemGroup>
</Project>
```

**Step 7: Create test projects**

`tests/System.Data.Async.Tests/System.Data.Async.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async\System.Data.Async.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
  </ItemGroup>
</Project>
```

Same pattern for `System.Data.Async.DataSet.Tests` and `System.Data.Async.Adapters.Tests`, each referencing its corresponding src project plus `System.Data.Async.Tests` for shared test utilities.

**Step 8: Create .slnx solution file**

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/System.Data.Async/System.Data.Async.csproj" />
    <Project Path="src/System.Data.Async.DataSet/System.Data.Async.DataSet.csproj" />
    <Project Path="src/System.Data.Async.Adapters/System.Data.Async.Adapters.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/System.Data.Async.Tests/System.Data.Async.Tests.csproj" />
    <Project Path="tests/System.Data.Async.DataSet.Tests/System.Data.Async.DataSet.Tests.csproj" />
    <Project Path="tests/System.Data.Async.Adapters.Tests/System.Data.Async.Adapters.Tests.csproj" />
  </Folder>
</Solution>
```

**Step 9: Restore and build to verify scaffolding**

Run: `dotnet restore && dotnet build`
Expected: Build succeeded with 0 errors.

**Step 10: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with three packages and test projects"
```

---

## Task 2: Core Interfaces — IAsyncDataRecord and IAsyncDataReader

**Files:**
- Create: `src/System.Data.Async/IAsyncDataRecord.cs`
- Create: `src/System.Data.Async/IAsyncDataReader.cs`
- Test: `tests/System.Data.Async.Tests/IAsyncDataReaderContractTests.cs`

**Step 1: Write contract test for IAsyncDataReader**

```csharp
namespace System.Data.Async.Tests;

using FluentAssertions;
using Xunit;

public class IAsyncDataReaderContractTests
{
    [Fact]
    public void IAsyncDataReader_Should_Extend_IAsyncDataRecord()
    {
        typeof(IAsyncDataReader).GetInterfaces()
            .Should().Contain(typeof(IAsyncDataRecord));
    }

    [Fact]
    public void IAsyncDataReader_Should_Extend_IAsyncEnumerable()
    {
        typeof(IAsyncDataReader).GetInterfaces()
            .Should().Contain(typeof(IAsyncEnumerable<IAsyncDataRecord>));
    }

    [Fact]
    public void IAsyncDataReader_Should_Extend_IAsyncDisposable()
    {
        typeof(IAsyncDataReader).GetInterfaces()
            .Should().Contain(typeof(IAsyncDisposable));
    }

    [Fact]
    public void IAsyncDataReader_Should_Extend_IDisposable()
    {
        typeof(IAsyncDataReader).GetInterfaces()
            .Should().Contain(typeof(IDisposable));
    }

    [Fact]
    public void IAsyncDataReader_Should_Have_ReadAsync_Method()
    {
        var method = typeof(IAsyncDataReader).GetMethod("ReadAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask<bool>));
    }

    [Fact]
    public void IAsyncDataReader_Should_Have_NextResultAsync_Method()
    {
        var method = typeof(IAsyncDataReader).GetMethod("NextResultAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask<bool>));
    }

    [Fact]
    public void IAsyncDataRecord_Should_Have_GetFieldValueAsync_Method()
    {
        var method = typeof(IAsyncDataRecord).GetMethod("GetFieldValueAsync");
        method.Should().NotBeNull();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/System.Data.Async.Tests -v minimal`
Expected: FAIL — types not defined yet.

**Step 3: Implement IAsyncDataRecord**

Create `src/System.Data.Async/IAsyncDataRecord.cs`:

```csharp
namespace System.Data.Async;

public interface IAsyncDataRecord
{
    int FieldCount { get; }
    object this[int i] { get; }
    object this[string name] { get; }

    bool GetBoolean(int i);
    byte GetByte(int i);
    long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length);
    char GetChar(int i);
    long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length);
    Guid GetGuid(int i);
    short GetInt16(int i);
    int GetInt32(int i);
    long GetInt64(int i);
    float GetFloat(int i);
    double GetDouble(int i);
    string GetString(int i);
    decimal GetDecimal(int i);
    DateTime GetDateTime(int i);
    IDataReader GetData(int i);
    string GetDataTypeName(int i);
    Type GetFieldType(int i);
    string GetName(int i);
    int GetOrdinal(string name);
    object GetValue(int i);
    int GetValues(object[] values);
    bool IsDBNull(int i);

    // Async extensions
    ValueTask<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default);
    ValueTask<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default);
}
```

**Step 4: Implement IAsyncDataReader**

Create `src/System.Data.Async/IAsyncDataReader.cs`:

```csharp
namespace System.Data.Async;

public interface IAsyncDataReader : IAsyncDataRecord, IAsyncEnumerable<IAsyncDataRecord>, IAsyncDisposable, IDisposable
{
    int Depth { get; }
    bool IsClosed { get; }
    int RecordsAffected { get; }
    bool HasRows { get; }

    // Sync
    bool Read();
    bool NextResult();
    void Close();
    DataTable GetSchemaTable();

    // Async
    ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
    ValueTask<DataTable> GetSchemaTableAsync(CancellationToken cancellationToken = default);
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/System.Data.Async.Tests -v minimal`
Expected: PASS

**Step 6: Commit**

```bash
git add src/System.Data.Async/IAsyncDataRecord.cs src/System.Data.Async/IAsyncDataReader.cs tests/System.Data.Async.Tests/IAsyncDataReaderContractTests.cs
git commit -m "feat: add IAsyncDataRecord and IAsyncDataReader interfaces"
```

---

## Task 3: Core Interfaces — IAsyncDbTransaction

**Files:**
- Create: `src/System.Data.Async/IAsyncDbTransaction.cs`
- Test: `tests/System.Data.Async.Tests/IAsyncDbTransactionContractTests.cs`

**Step 1: Write contract test**

```csharp
namespace System.Data.Async.Tests;

using FluentAssertions;
using Xunit;

public class IAsyncDbTransactionContractTests
{
    [Fact]
    public void IAsyncDbTransaction_Should_Have_CommitAsync()
    {
        var method = typeof(IAsyncDbTransaction).GetMethod("CommitAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask));
    }

    [Fact]
    public void IAsyncDbTransaction_Should_Have_RollbackAsync()
    {
        var method = typeof(IAsyncDbTransaction).GetMethod("RollbackAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask));
    }

    [Fact]
    public void IAsyncDbTransaction_Should_Extend_IAsyncDisposable_And_IDisposable()
    {
        typeof(IAsyncDbTransaction).GetInterfaces()
            .Should().Contain(typeof(IAsyncDisposable))
            .And.Contain(typeof(IDisposable));
    }
}
```

**Step 2: Run test — expect FAIL**

Run: `dotnet test tests/System.Data.Async.Tests -v minimal`

**Step 3: Implement IAsyncDbTransaction**

```csharp
namespace System.Data.Async;

public interface IAsyncDbTransaction : IAsyncDisposable, IDisposable
{
    IAsyncDbConnection Connection { get; }
    IsolationLevel IsolationLevel { get; }

    void Commit();
    void Rollback();

    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/IAsyncDbTransaction.cs tests/System.Data.Async.Tests/IAsyncDbTransactionContractTests.cs
git commit -m "feat: add IAsyncDbTransaction interface"
```

---

## Task 4: Core Interfaces — IAsyncDbCommand

**Files:**
- Create: `src/System.Data.Async/IAsyncDbCommand.cs`
- Test: `tests/System.Data.Async.Tests/IAsyncDbCommandContractTests.cs`

**Step 1: Write contract test**

Test that IAsyncDbCommand has: ExecuteReaderAsync, ExecuteNonQueryAsync, ExecuteScalarAsync, PrepareAsync, all returning ValueTask variants. Test it extends IAsyncDisposable and IDisposable.

**Step 2: Run test — expect FAIL**

**Step 3: Implement IAsyncDbCommand**

```csharp
namespace System.Data.Async;

public interface IAsyncDbCommand : IAsyncDisposable, IDisposable
{
    string CommandText { get; set; }
    int CommandTimeout { get; set; }
    CommandType CommandType { get; set; }
    IAsyncDbConnection? Connection { get; set; }
    IAsyncDbTransaction? Transaction { get; set; }
    IDataParameterCollection Parameters { get; }
    UpdateRowSource UpdatedRowSource { get; set; }

    // Sync
    IAsyncDataReader ExecuteReader();
    IAsyncDataReader ExecuteReader(CommandBehavior behavior);
    int ExecuteNonQuery();
    object? ExecuteScalar();
    void Prepare();
    void Cancel();
    IDbDataParameter CreateParameter();

    // Async
    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default);
    ValueTask<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
    ValueTask<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);
    ValueTask PrepareAsync(CancellationToken cancellationToken = default);
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/IAsyncDbCommand.cs tests/System.Data.Async.Tests/IAsyncDbCommandContractTests.cs
git commit -m "feat: add IAsyncDbCommand interface"
```

---

## Task 5: Core Interfaces — IAsyncDbConnection

**Files:**
- Create: `src/System.Data.Async/IAsyncDbConnection.cs`
- Test: `tests/System.Data.Async.Tests/IAsyncDbConnectionContractTests.cs`

**Step 1: Write contract test**

Test that IAsyncDbConnection has: OpenAsync, CloseAsync, BeginTransactionAsync, ChangeDatabaseAsync, all returning ValueTask variants. Test CreateCommand returns IAsyncDbCommand.

**Step 2: Run test — expect FAIL**

**Step 3: Implement IAsyncDbConnection**

```csharp
namespace System.Data.Async;

public interface IAsyncDbConnection : IAsyncDisposable, IDisposable
{
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }

    // Sync
    IAsyncDbTransaction BeginTransaction();
    IAsyncDbTransaction BeginTransaction(IsolationLevel il);
    void ChangeDatabase(string databaseName);
    IAsyncDbCommand CreateCommand();
    void Open();
    void Close();

    // Async
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default);
    ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    ValueTask OpenAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/IAsyncDbConnection.cs tests/System.Data.Async.Tests/IAsyncDbConnectionContractTests.cs
git commit -m "feat: add IAsyncDbConnection interface"
```

---

## Task 6: Core Interfaces — IAsyncDbProviderFactory

**Files:**
- Create: `src/System.Data.Async/IAsyncDbProviderFactory.cs`
- Test: `tests/System.Data.Async.Tests/IAsyncDbProviderFactoryContractTests.cs`

**Step 1: Write contract test**

**Step 2: Run test — expect FAIL**

**Step 3: Implement IAsyncDbProviderFactory**

```csharp
namespace System.Data.Async;

public interface IAsyncDbProviderFactory
{
    IAsyncDbConnection CreateConnection();
    IAsyncDbCommand CreateCommand();
    IDbDataParameter CreateParameter();
}
```

Note: `CreateDataAdapter()` returns `AsyncDataAdapter` which lives in the DataSet package. We add this method later via extension or separate interface to avoid circular dependency.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/IAsyncDbProviderFactory.cs tests/System.Data.Async.Tests/IAsyncDbProviderFactoryContractTests.cs
git commit -m "feat: add IAsyncDbProviderFactory interface"
```

---

## Task 7: Abstract Base Class — AsyncDbDataReader

**Files:**
- Create: `src/System.Data.Async/AsyncDbDataReader.cs`
- Test: `tests/System.Data.Async.Tests/AsyncDbDataReaderTests.cs`

**Step 1: Write test with a concrete test implementation**

Create a `TestDbDataReader` that extends `AsyncDbDataReader` with in-memory data. Test:
- `ReadAsync` returns true/false correctly
- `NextResultAsync` advances result sets
- `GetAsyncEnumerator` iterates rows via `await foreach`
- Sync `Read()` works (calls async internally)
- `DisposeAsync` calls `CloseCoreAsync`

**Step 2: Run test — expect FAIL**

**Step 3: Implement AsyncDbDataReader**

Full abstract base class with:
- All `IAsyncDataRecord` Get* methods as abstract
- `protected abstract ValueTask<bool> ReadCoreAsync(CancellationToken)`
- `protected abstract ValueTask<bool> NextResultCoreAsync(CancellationToken)`
- `protected abstract ValueTask CloseCoreAsync()`
- Template method sync → async bridge via `.GetAwaiter().GetResult()`
- `GetAsyncEnumerator` yielding `this` while `ReadCoreAsync` returns true
- `DisposeAsync` calling `CloseCoreAsync`

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/AsyncDbDataReader.cs tests/System.Data.Async.Tests/AsyncDbDataReaderTests.cs
git commit -m "feat: add AsyncDbDataReader abstract base class"
```

---

## Task 8: Abstract Base Class — AsyncDbTransaction

**Files:**
- Create: `src/System.Data.Async/AsyncDbTransaction.cs`
- Test: `tests/System.Data.Async.Tests/AsyncDbTransactionTests.cs`

**Step 1: Write test with concrete test implementation**

**Step 2: Run test — expect FAIL**

**Step 3: Implement AsyncDbTransaction**

```csharp
namespace System.Data.Async;

public abstract class AsyncDbTransaction : IAsyncDbTransaction
{
    public abstract IAsyncDbConnection Connection { get; }
    public abstract IsolationLevel IsolationLevel { get; }

    protected abstract ValueTask CommitCoreAsync(CancellationToken cancellationToken);
    protected abstract ValueTask RollbackCoreAsync(CancellationToken cancellationToken);

    public void Commit() => CommitCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    public void Rollback() => RollbackCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
    public ValueTask CommitAsync(CancellationToken cancellationToken = default) => CommitCoreAsync(cancellationToken);
    public ValueTask RollbackAsync(CancellationToken cancellationToken = default) => RollbackCoreAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await RollbackCoreAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/AsyncDbTransaction.cs tests/System.Data.Async.Tests/AsyncDbTransactionTests.cs
git commit -m "feat: add AsyncDbTransaction abstract base class"
```

---

## Task 9: Abstract Base Class — AsyncDbCommand

**Files:**
- Create: `src/System.Data.Async/AsyncDbCommand.cs`
- Test: `tests/System.Data.Async.Tests/AsyncDbCommandTests.cs`

**Step 1: Write test with concrete test implementation**

Test:
- `ExecuteReaderAsync()` delegates to `ExecuteDbReaderAsync` with `CommandBehavior.Default`
- `ExecuteNonQueryAsync()` delegates to `ExecuteNonQueryCoreAsync`
- `ExecuteScalarAsync()` delegates to `ExecuteScalarCoreAsync`
- Sync methods work via async bridge
- `DisposeAsync` calls `Dispose(true)`

**Step 2: Run test — expect FAIL**

**Step 3: Implement AsyncDbCommand**

Full abstract base class following the template method pattern from the design doc.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/AsyncDbCommand.cs tests/System.Data.Async.Tests/AsyncDbCommandTests.cs
git commit -m "feat: add AsyncDbCommand abstract base class"
```

---

## Task 10: Abstract Base Class — AsyncDbConnection

**Files:**
- Create: `src/System.Data.Async/AsyncDbConnection.cs`
- Test: `tests/System.Data.Async.Tests/AsyncDbConnectionTests.cs`

**Step 1: Write test with concrete test implementation**

Test:
- `OpenAsync` delegates to `OpenCoreAsync`
- `CloseAsync` delegates to `CloseCoreAsync`
- `BeginTransactionAsync` delegates to `BeginDbTransactionAsync`
- Sync methods work via async bridge
- `CreateCommand` delegates to `CreateDbCommand`
- `DisposeAsync` calls `CloseCoreAsync`

**Step 2: Run test — expect FAIL**

**Step 3: Implement AsyncDbConnection**

Full abstract base class following the template method pattern from the design doc.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async/AsyncDbConnection.cs tests/System.Data.Async.Tests/AsyncDbConnectionTests.cs
git commit -m "feat: add AsyncDbConnection abstract base class"
```

---

## Task 11: Adapter — AdapterDbDataReader

**Files:**
- Create: `src/System.Data.Async.Adapters/AdapterDbDataReader.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbDataReaderTests.cs`

**Step 1: Write test using a real SQLite in-memory DbDataReader**

Use `Microsoft.Data.Sqlite` as test dependency. Create a table, insert rows, get a `DbDataReader`, wrap it in `AdapterDbDataReader`, test:
- `await foreach` iterates all rows
- `ReadAsync` returns true/false correctly
- All `Get*` methods return correct values
- `IsDBNullAsync` works
- `GetFieldValueAsync<T>` works
- `FieldCount`, `HasRows`, `RecordsAffected` are correct
- Sync `Read()` uses native sync (not sync-over-async)
- `DisposeAsync` closes the reader
- `InnerReader` returns the wrapped reader

**Step 2: Run test — expect FAIL**

**Step 3: Implement AdapterDbDataReader**

Sealed class extending `AsyncDbDataReader`. Wraps `DbDataReader`. All `Get*` delegate to inner. Async methods delegate to inner's async. Sync overrides with `new` to use inner's native sync. Expose `InnerReader`.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/AdapterDbDataReader.cs tests/System.Data.Async.Adapters.Tests/AdapterDbDataReaderTests.cs
git commit -m "feat: add AdapterDbDataReader wrapping DbDataReader"
```

---

## Task 12: Adapter — AdapterDbTransaction

**Files:**
- Create: `src/System.Data.Async.Adapters/AdapterDbTransaction.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbTransactionTests.cs`

**Step 1: Write test using SQLite in-memory**

Test: CommitAsync commits, RollbackAsync rolls back, IsolationLevel is correct, Connection returns the wrapping AdapterDbConnection.

**Step 2: Run test — expect FAIL**

**Step 3: Implement AdapterDbTransaction**

Sealed class extending `AsyncDbTransaction`. Wraps `DbTransaction`.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/AdapterDbTransaction.cs tests/System.Data.Async.Adapters.Tests/AdapterDbTransactionTests.cs
git commit -m "feat: add AdapterDbTransaction wrapping DbTransaction"
```

---

## Task 13: Adapter — AdapterDbCommand

**Files:**
- Create: `src/System.Data.Async.Adapters/AdapterDbCommand.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbCommandTests.cs`

**Step 1: Write test using SQLite in-memory**

Test: ExecuteReaderAsync returns AdapterDbDataReader, ExecuteNonQueryAsync returns affected rows, ExecuteScalarAsync returns scalar, Parameters work, CreateParameter works, Cancel works.

**Step 2: Run test — expect FAIL**

**Step 3: Implement AdapterDbCommand**

Sealed class extending `AsyncDbCommand`. Wraps `DbCommand`.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/AdapterDbCommand.cs tests/System.Data.Async.Adapters.Tests/AdapterDbCommandTests.cs
git commit -m "feat: add AdapterDbCommand wrapping DbCommand"
```

---

## Task 14: Adapter — AdapterDbConnection

**Files:**
- Create: `src/System.Data.Async.Adapters/AdapterDbConnection.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbConnectionTests.cs`

**Step 1: Write test using SQLite in-memory**

Test: OpenAsync opens, CloseAsync closes, State is correct, CreateCommand returns AdapterDbCommand, BeginTransactionAsync returns AdapterDbTransaction, ConnectionString get/set works, DisposeAsync closes.

**Step 2: Run test — expect FAIL**

**Step 3: Implement AdapterDbConnection**

Sealed class extending `AsyncDbConnection`. Wraps `DbConnection`.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/AdapterDbConnection.cs tests/System.Data.Async.Adapters.Tests/AdapterDbConnectionTests.cs
git commit -m "feat: add AdapterDbConnection wrapping DbConnection"
```

---

## Task 15: Adapter — AdapterDbProviderFactory + Extensions

**Files:**
- Create: `src/System.Data.Async.Adapters/AdapterDbProviderFactory.cs`
- Create: `src/System.Data.Async.Adapters/DbConnectionExtensions.cs`
- Create: `src/System.Data.Async.Adapters/ServiceCollectionExtensions.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbProviderFactoryTests.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/DbConnectionExtensionsTests.cs`

**Step 1: Write tests**

Test `AdapterDbProviderFactory`: CreateConnection, CreateCommand, CreateParameter all return adapter-wrapped types.
Test `AsAsync()`: wraps a DbConnection into AdapterDbConnection.
Test `AddAsyncData`: registers IAsyncDbProviderFactory in service collection.

**Step 2: Run tests — expect FAIL**

**Step 3: Implement**

`AdapterDbProviderFactory`: wraps `DbProviderFactory`.
`DbConnectionExtensions.AsAsync()`: `new AdapterDbConnection(connection)`.
`ServiceCollectionExtensions.AddAsyncData()`: registers `IAsyncDbProviderFactory` as singleton.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/ tests/System.Data.Async.Adapters.Tests/
git commit -m "feat: add AdapterDbProviderFactory, AsAsync extension, and DI registration"
```

---

## Task 16: AsyncDataTable — Core Structure

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableTests.cs`

**Step 1: Write tests**

Test that AsyncDataTable:
- Has same property surface as DataTable (TableName, Namespace, CaseSensitive, Locale, Columns, Rows, Constraints, PrimaryKey, etc.)
- `NewRow()` creates a DataRow
- `Rows.Add()` adds rows
- `AcceptChanges()` / `RejectChanges()` work
- `Clone()` copies schema
- `Copy()` copies schema + data
- `Select()` filters rows
- `GetChanges()` returns changed rows
- `Clear()` removes all rows
- `Merge()` merges tables

**Step 2: Run tests — expect FAIL**

**Step 3: Implement AsyncDataTable**

AsyncDataTable wraps an internal `DataTable` instance. All in-memory operations delegate to the inner DataTable. Public properties expose the inner's collections directly. This ensures identical behavior and JSON serialization shape.

```csharp
namespace System.Data.Async.DataSet;

public class AsyncDataTable
{
    private readonly DataTable _inner;

    public AsyncDataTable() => _inner = new DataTable();
    public AsyncDataTable(string tableName) => _inner = new DataTable(tableName);
    public AsyncDataTable(string tableName, string tableNamespace) => _inner = new DataTable(tableName, tableNamespace);

    // Internal constructor for wrapping existing DataTable (used by JSON deserializer)
    internal AsyncDataTable(DataTable inner) => _inner = inner;

    // Expose inner for JSON converter and adapter
    internal DataTable InnerDataTable => _inner;

    // All properties delegate to _inner
    public string TableName { get => _inner.TableName; set => _inner.TableName = value; }
    public string Namespace { get => _inner.Namespace; set => _inner.Namespace = value; }
    // ... all other properties

    // All collections expose inner's collections directly
    public DataColumnCollection Columns => _inner.Columns;
    public DataRowCollection Rows => _inner.Rows;
    public ConstraintCollection Constraints => _inner.Constraints;
    // ... etc

    // All methods delegate to _inner
    public DataRow NewRow() => _inner.NewRow();
    public void ImportRow(DataRow row) => _inner.ImportRow(row);
    public void AcceptChanges() => _inner.AcceptChanges();
    // ... all other methods

    // Async I/O methods — new additions
    public async ValueTask<int> LoadAsync(IAsyncDataReader reader, CancellationToken cancellationToken = default)
    {
        // Async row-by-row loading from reader
    }

    public async ValueTask ReadXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Async XML reading
    }

    public async ValueTask WriteXmlAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Async XML writing
    }

    // Implicit conversion for backward compat where DataTable is expected
    public static implicit operator DataTable(AsyncDataTable asyncTable) => asyncTable._inner;
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataTable.cs tests/System.Data.Async.DataSet.Tests/AsyncDataTableTests.cs
git commit -m "feat: add AsyncDataTable wrapping DataTable with async I/O"
```

---

## Task 17: AsyncDataSet — Core Structure

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataSet.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataSetTests.cs`

**Step 1: Write tests**

Test same patterns as AsyncDataTable — property surface, Tables collection, Relations, AcceptChanges, Merge, GetChanges, HasChanges, Clone, Copy.

**Step 2: Run tests — expect FAIL**

**Step 3: Implement AsyncDataSet**

Same wrapper pattern: wraps internal `DataSet`. All properties/methods delegate. Uses `AsyncDataTable` wrappers for its Tables.

```csharp
namespace System.Data.Async.DataSet;

public class AsyncDataSet
{
    private readonly System.Data.DataSet _inner;

    public AsyncDataSet() => _inner = new System.Data.DataSet();
    public AsyncDataSet(string dataSetName) => _inner = new System.Data.DataSet(dataSetName);
    internal AsyncDataSet(System.Data.DataSet inner) => _inner = inner;

    internal System.Data.DataSet InnerDataSet => _inner;

    public string DataSetName { get => _inner.DataSetName; set => _inner.DataSetName = value; }
    // ... all properties delegate

    public DataTableCollection Tables => _inner.Tables;
    public DataRelationCollection Relations => _inner.Relations;
    // ... etc

    // Async I/O
    public async ValueTask ReadXmlAsync(Stream stream, CancellationToken cancellationToken = default) { }
    public async ValueTask WriteXmlAsync(Stream stream, CancellationToken cancellationToken = default) { }

    public static implicit operator System.Data.DataSet(AsyncDataSet asyncDataSet) => asyncDataSet._inner;
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataSet.cs tests/System.Data.Async.DataSet.Tests/AsyncDataSetTests.cs
git commit -m "feat: add AsyncDataSet wrapping DataSet with async I/O"
```

---

## Task 18: AsyncDataTable.LoadAsync Implementation

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncDataTableLoadAsyncTests.cs`

**Step 1: Write test**

Use AdapterDbDataReader wrapping a SQLite reader. Create table with columns, insert rows, wrap reader, call `LoadAsync`. Verify rows loaded correctly, column types match, row count is correct.

**Step 2: Run test — expect FAIL**

**Step 3: Implement LoadAsync**

```csharp
public async ValueTask<int> LoadAsync(IAsyncDataReader reader, CancellationToken cancellationToken = default)
{
    return await LoadAsync(reader, LoadOption.OverwriteChanges, cancellationToken).ConfigureAwait(false);
}

public async ValueTask<int> LoadAsync(IAsyncDataReader reader, LoadOption loadOption, CancellationToken cancellationToken = default)
{
    var schemaTable = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);

    // Build columns from schema if table is empty
    if (_inner.Columns.Count == 0)
    {
        foreach (DataRow schemaRow in schemaTable.Rows)
        {
            var columnName = (string)schemaRow["ColumnName"];
            var dataType = (Type)schemaRow["DataType"];
            _inner.Columns.Add(columnName, dataType);
        }
    }

    int count = 0;
    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        _inner.LoadDataRow(values, loadOption);
        count++;
    }

    return count;
}
```

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataTable.cs tests/System.Data.Async.DataSet.Tests/AsyncDataTableLoadAsyncTests.cs
git commit -m "feat: implement AsyncDataTable.LoadAsync with schema inference"
```

---

## Task 19: AsyncDataAdapter

**Files:**
- Create: `src/System.Data.Async.DataSet/AsyncDataAdapter.cs`
- Create: `src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbDataAdapterTests.cs`

**Step 1: Write test**

Use SQLite in-memory. Create table, insert rows. Create AdapterDbDataAdapter with select command. Call `FillAsync(AsyncDataTable)`. Verify rows loaded.

**Step 2: Run test — expect FAIL**

**Step 3: Implement AsyncDataAdapter (abstract)**

```csharp
namespace System.Data.Async.DataSet;

public abstract class AsyncDataAdapter
{
    public IAsyncDbCommand? SelectCommand { get; set; }
    public IAsyncDbCommand? InsertCommand { get; set; }
    public IAsyncDbCommand? UpdateCommand { get; set; }
    public IAsyncDbCommand? DeleteCommand { get; set; }
    public MissingMappingAction MissingMappingAction { get; set; }
    public MissingSchemaAction MissingSchemaAction { get; set; }

    public abstract ValueTask<int> FillAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> FillAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> UpdateAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> UpdateAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default);

    public int Fill(AsyncDataSet dataSet) => FillAsync(dataSet).GetAwaiter().GetResult();
    public int Fill(AsyncDataTable dataTable) => FillAsync(dataTable).GetAwaiter().GetResult();
    public int Update(AsyncDataSet dataSet) => UpdateAsync(dataSet).GetAwaiter().GetResult();
    public int Update(AsyncDataTable dataTable) => UpdateAsync(dataTable).GetAwaiter().GetResult();
}
```

**Step 4: Implement AdapterDbDataAdapter**

```csharp
namespace System.Data.Async.Adapters;

public sealed class AdapterDbDataAdapter : AsyncDataAdapter
{
    public AdapterDbDataAdapter() { }
    public AdapterDbDataAdapter(IAsyncDbCommand selectCommand) => SelectCommand = selectCommand;

    public override async ValueTask<int> FillAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(SelectCommand);
        var connection = SelectCommand.Connection;
        ArgumentNullException.ThrowIfNull(connection);

        bool openedConnection = false;
        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            openedConnection = true;
        }

        try
        {
            await using var reader = await SelectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await dataTable.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (openedConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    public override async ValueTask<int> FillAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default)
    {
        // Similar to FillAsync(AsyncDataTable) but creates/fills tables in the dataset
    }

    public override async ValueTask<int> UpdateAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default)
    {
        // Iterate changed rows, execute Insert/Update/Delete commands
    }

    public override async ValueTask<int> UpdateAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default)
    {
        // Same for single table
    }
}
```

**Step 5: Run tests — expect PASS**

**Step 6: Commit**

```bash
git add src/System.Data.Async.DataSet/AsyncDataAdapter.cs src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs tests/System.Data.Async.Adapters.Tests/AdapterDbDataAdapterTests.cs
git commit -m "feat: add AsyncDataAdapter and AdapterDbDataAdapter with FillAsync"
```

---

## Task 20: JSON Converter — AsyncDataTableConverter

**Files:**
- Create: `src/System.Data.Async.DataSet/Converters/AsyncDataTableConverter.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataTableConverterTests.cs`

**Step 1: Write tests**

Test the exact Json.Net.DataSetConverters format:

```csharp
[Fact]
public void Should_Deserialize_DataTable_From_DataSetConverters_Format()
{
    // JSON produced by original DataTable + Json.Net.DataSetConverters
    var json = """
    {
      "CaseSensitive": false,
      "DisplayExpression": "",
      "Locale": "",
      "MinimumCapacity": 50,
      "Namespace": "",
      "Prefix": "",
      "RemotingFormat": 0,
      "TableName": "Users",
      "Columns": [
        {
          "AllowDBNull": false,
          "AutoIncrement": true,
          "AutoIncrementSeed": 1,
          "AutoIncrementStep": 1,
          "Caption": "Id",
          "ColumnMapping": 1,
          "ColumnName": "Id",
          "DataType": "System.Int32",
          "DateTimeMode": 0,
          "DefaultValue": null,
          "Expression": "",
          "ExtendedProperties": [],
          "MaxLength": -1,
          "Namespace": "",
          "Prefix": "",
          "ReadOnly": false
        },
        {
          "AllowDBNull": true,
          "AutoIncrement": false,
          "AutoIncrementSeed": 0,
          "AutoIncrementStep": 1,
          "Caption": "Name",
          "ColumnMapping": 1,
          "ColumnName": "Name",
          "DataType": "System.String",
          "DateTimeMode": 0,
          "DefaultValue": null,
          "Expression": "",
          "ExtendedProperties": [],
          "MaxLength": -1,
          "Namespace": "",
          "Prefix": "",
          "ReadOnly": false
        }
      ],
      "Constraints": [
        {
          "Columns": ["Id"],
          "ConstraintName": "PK_Users",
          "IsPrimaryKey": true,
          "ExtendedProperties": []
        }
      ],
      "Rows": [
        {
          "OriginalRow": null,
          "Id": 1,
          "Name": "Alice",
          "RowState": 2
        },
        {
          "OriginalRow": { "Id": 2, "Name": "Bob", "RowState": 16 },
          "Id": 2,
          "Name": "Robert",
          "RowState": 16
        }
      ]
    }
    """;

    var settings = new JsonSerializerSettings();
    settings.Converters.Add(new AsyncDataTableConverter());

    var table = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);

    table.Should().NotBeNull();
    table!.TableName.Should().Be("Users");
    table.Columns.Count.Should().Be(2);
    table.Rows.Count.Should().Be(2);

    // Row 1: Unchanged
    table.Rows[0]["Name"].Should().Be("Alice");
    table.Rows[0].RowState.Should().Be(DataRowState.Unchanged);

    // Row 2: Modified — current is "Robert", original is "Bob"
    table.Rows[1]["Name"].Should().Be("Robert");
    table.Rows[1]["Name", DataRowVersion.Original].Should().Be("Bob");
    table.Rows[1].RowState.Should().Be(DataRowState.Modified);
}

[Fact]
public void Should_Serialize_To_DataSetConverters_Compatible_Format()
{
    var table = new AsyncDataTable("Users");
    table.Columns.Add("Id", typeof(int));
    table.Columns.Add("Name", typeof(string));
    table.Rows.Add(1, "Alice");
    table.AcceptChanges();

    var settings = new JsonSerializerSettings();
    settings.Converters.Add(new AsyncDataTableConverter());

    var json = JsonConvert.SerializeObject(table, settings);
    var jObj = JObject.Parse(json);

    jObj["TableName"]!.Value<string>().Should().Be("Users");
    jObj["Columns"]!.Should().HaveCount(2);
    jObj["Rows"]!.Should().HaveCount(1);
    jObj["Rows"]![0]!["RowState"]!.Value<int>().Should().Be(2); // Unchanged
    jObj["Rows"]![0]!["OriginalRow"]!.Type.Should().Be(JTokenType.Null);
}

[Fact]
public void Should_Handle_Deleted_Rows()
{
    // Test deleted row serialization/deserialization
}

[Fact]
public void Should_Handle_Added_Rows()
{
    // Test added row serialization/deserialization
}

[Fact]
public void Should_Handle_DBNull_Values()
{
    // Null values should serialize as JSON null
}

[Fact]
public void Should_Handle_Decimal_As_String()
{
    // Decimal values serialized as "F28" strings
}
```

**Step 2: Run tests — expect FAIL**

**Step 3: Implement AsyncDataTableConverter**

Must follow the exact Json.Net.DataSetConverters format:
- Property order is fixed (CaseSensitive, DisplayExpression, Locale, MinimumCapacity, Namespace, Prefix, RemotingFormat, TableName, Columns, Constraints, Rows)
- Columns as array of column definition objects
- Rows with OriginalRow first, then column values, then RowState
- Row states as integer enum values
- DataType as type name string (try short name first on deser)
- Decimals as "F28" strings
- ExtendedProperties as array of {KeyType, Key, ValueType, Value} objects

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/Converters/ tests/System.Data.Async.DataSet.Tests/Converters/
git commit -m "feat: add AsyncDataTableConverter with Json.Net.DataSetConverters format compatibility"
```

---

## Task 21: JSON Converter — AsyncDataSetConverter

**Files:**
- Create: `src/System.Data.Async.DataSet/Converters/AsyncDataSetConverter.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataSetConverterTests.cs`

**Step 1: Write tests**

Test deserialization of full DataSet JSON (with Tables object, Relations, constraints). Test round-trip. Test cross-compat: serialize with original DataSetConverters, deserialize with AsyncDataSetConverter and vice versa.

**Step 2: Run tests — expect FAIL**

**Step 3: Implement AsyncDataSetConverter**

Delegates to AsyncDataTableConverter for each table. Handles Relations, ExtendedProperties, DataSetName, etc.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/Converters/AsyncDataSetConverter.cs tests/System.Data.Async.DataSet.Tests/Converters/AsyncDataSetConverterTests.cs
git commit -m "feat: add AsyncDataSetConverter with full DataSet JSON round-trip"
```

---

## Task 22: Cross-Compatibility Integration Test

**Files:**
- Test: `tests/System.Data.Async.DataSet.Tests/CrossCompatibilityTests.cs`

**Step 1: Write integration tests**

Add `Json.Net.DataSetConverters` NuGet package as test dependency. Test:
1. Serialize `DataSet` with `Json.Net.DataSetConverters` → Deserialize into `AsyncDataSet` with `AsyncDataSetConverter`
2. Serialize `AsyncDataSet` with `AsyncDataSetConverter` → Deserialize into `DataSet` with `Json.Net.DataSetConverters`
3. Full round-trip with complex data: multiple tables, relations, all row states, null values, decimals, constraints, extended properties

```csharp
[Fact]
public void Original_DataSet_Json_Should_Deserialize_Into_AsyncDataSet()
{
    // Create original DataSet with complex data
    var ds = new System.Data.DataSet("TestDS");
    var orders = ds.Tables.Add("Orders");
    orders.Columns.Add("OrderId", typeof(int));
    orders.Columns.Add("Total", typeof(decimal));
    orders.PrimaryKey = [orders.Columns["OrderId"]!];
    orders.Rows.Add(1, 99.99m);
    orders.Rows.Add(2, 150.00m);
    ds.AcceptChanges();
    orders.Rows[1]["Total"] = 175.50m; // Modified

    // Serialize with original converter
    var originalSettings = new JsonSerializerSettings();
    originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataSetConverter());
    var json = JsonConvert.SerializeObject(ds, originalSettings);

    // Deserialize with our converter
    var asyncSettings = new JsonSerializerSettings();
    asyncSettings.Converters.Add(new AsyncDataSetConverter());
    var asyncDs = JsonConvert.DeserializeObject<AsyncDataSet>(json, asyncSettings);

    asyncDs.Should().NotBeNull();
    asyncDs!.Tables["Orders"]!.Rows.Count.Should().Be(2);
    asyncDs.Tables["Orders"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
    asyncDs.Tables["Orders"]!.Rows[1]["Total"].Should().Be(175.50m);
    asyncDs.Tables["Orders"]!.Rows[1]["Total", DataRowVersion.Original].Should().Be(150.00m);
}
```

**Step 2: Run tests — expect PASS (if converters are correct) or debug failures**

**Step 3: Commit**

```bash
git add tests/System.Data.Async.DataSet.Tests/CrossCompatibilityTests.cs
git commit -m "test: add cross-compatibility integration tests with Json.Net.DataSetConverters"
```

---

## Task 23: End-to-End Integration Test

**Files:**
- Test: `tests/System.Data.Async.Adapters.Tests/EndToEndTests.cs`

**Step 1: Write full end-to-end test**

Using SQLite in-memory, test the complete workflow:

```csharp
[Fact]
public async Task Full_Workflow_Create_Query_Fill_Serialize_Roundtrip()
{
    // 1. Create connection via AsAsync()
    await using var conn = new SqliteConnection("Data Source=:memory:").AsAsync();
    await conn.OpenAsync();

    // 2. Create table
    await using var createCmd = conn.CreateCommand();
    createCmd.CommandText = "CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT, Balance REAL)";
    await createCmd.ExecuteNonQueryAsync();

    // 3. Insert data
    await using var insertCmd = conn.CreateCommand();
    insertCmd.CommandText = "INSERT INTO Users VALUES (1, 'Alice', 100.50), (2, 'Bob', 200.75)";
    await insertCmd.ExecuteNonQueryAsync();

    // 4. Query with IAsyncEnumerable
    await using var queryCmd = conn.CreateCommand();
    queryCmd.CommandText = "SELECT * FROM Users";
    var names = new List<string>();
    await foreach (var record in queryCmd.ExecuteReaderAsync())
    {
        names.Add(record.GetString(1));
    }
    names.Should().BeEquivalentTo(["Alice", "Bob"]);

    // 5. Fill AsyncDataTable via adapter
    var selectCmd = conn.CreateCommand();
    selectCmd.CommandText = "SELECT * FROM Users";
    var adapter = new AdapterDbDataAdapter(selectCmd);
    var table = new AsyncDataTable("Users");
    await adapter.FillAsync(table);
    table.Rows.Count.Should().Be(2);

    // 6. Serialize to JSON and back
    var settings = new JsonSerializerSettings();
    settings.Converters.Add(new AsyncDataTableConverter());
    var json = JsonConvert.SerializeObject(table, settings);
    var deserialized = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);
    deserialized!.Rows.Count.Should().Be(2);
    deserialized.Rows[0]["Name"].Should().Be("Alice");
}
```

**Step 2: Run test — expect PASS**

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Adapters.Tests/EndToEndTests.cs
git commit -m "test: add end-to-end integration test covering full async workflow"
```

---

## Task 24: XML Async I/O Implementation

**Files:**
- Modify: `src/System.Data.Async.DataSet/AsyncDataTable.cs`
- Modify: `src/System.Data.Async.DataSet/AsyncDataSet.cs`
- Test: `tests/System.Data.Async.DataSet.Tests/AsyncXmlTests.cs`

**Step 1: Write tests**

Test ReadXmlAsync/WriteXmlAsync for both AsyncDataTable and AsyncDataSet. Round-trip via MemoryStream. Verify data integrity.

**Step 2: Run tests — expect FAIL**

**Step 3: Implement async XML I/O**

Use `XmlReader.CreateAsync` and `XmlWriter.CreateAsync` with async settings. Delegate to inner DataTable/DataSet for actual XML processing (their ReadXml/WriteXml accept XmlReader/XmlWriter).

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.DataSet/ tests/System.Data.Async.DataSet.Tests/AsyncXmlTests.cs
git commit -m "feat: implement async XML read/write for AsyncDataTable and AsyncDataSet"
```

---

## Task 25: UpdateAsync Implementation

**Files:**
- Modify: `src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs`
- Test: `tests/System.Data.Async.Adapters.Tests/AdapterDbDataAdapterUpdateTests.cs`

**Step 1: Write tests**

Test UpdateAsync with SQLite:
- Insert new rows (Added state → InsertCommand)
- Update modified rows (Modified state → UpdateCommand)
- Delete deleted rows (Deleted state → DeleteCommand)
- Verify database reflects all changes after UpdateAsync

**Step 2: Run tests — expect FAIL**

**Step 3: Implement UpdateAsync**

Iterate DataTable rows by state. For each row state, execute the corresponding command with parameterized values.

**Step 4: Run tests — expect PASS**

**Step 5: Commit**

```bash
git add src/System.Data.Async.Adapters/AdapterDbDataAdapter.cs tests/System.Data.Async.Adapters.Tests/AdapterDbDataAdapterUpdateTests.cs
git commit -m "feat: implement AdapterDbDataAdapter.UpdateAsync with insert/update/delete"
```

---

## Task 26: Final Cleanup and README

**Files:**
- Create: `README.md`
- Verify all tests pass

**Step 1: Run full test suite**

Run: `dotnet test --verbosity normal`
Expected: All tests pass.

**Step 2: Run build with pack**

Run: `dotnet pack -c Release`
Expected: Three .nupkg files produced.

**Step 3: Write README.md**

Include: what it is, installation, quick start (AsAsync, await foreach, FillAsync, JSON compat), package breakdown, API reference overview.

**Step 4: Commit**

```bash
git add README.md
git commit -m "docs: add README with installation and usage examples"
```

---

## Dependency Order

```
Task 1  (scaffolding)
  ├── Task 2  (IAsyncDataRecord + IAsyncDataReader)
  ├── Task 3  (IAsyncDbTransaction)
  ├── Task 4  (IAsyncDbCommand) — depends on Task 2, 3
  ├── Task 5  (IAsyncDbConnection) — depends on Task 3, 4
  ├── Task 6  (IAsyncDbProviderFactory) — depends on Task 4, 5
  │
  ├── Task 7  (AsyncDbDataReader) — depends on Task 2
  ├── Task 8  (AsyncDbTransaction) — depends on Task 3
  ├── Task 9  (AsyncDbCommand) — depends on Task 4, 7
  ├── Task 10 (AsyncDbConnection) — depends on Task 5, 8, 9
  │
  ├── Task 11 (AdapterDbDataReader) — depends on Task 7
  ├── Task 12 (AdapterDbTransaction) — depends on Task 8
  ├── Task 13 (AdapterDbCommand) — depends on Task 9, 11
  ├── Task 14 (AdapterDbConnection) — depends on Task 10, 12, 13
  ├── Task 15 (AdapterDbProviderFactory + Extensions) — depends on Task 14
  │
  ├── Task 16 (AsyncDataTable) — depends on Task 2
  ├── Task 17 (AsyncDataSet) — depends on Task 16
  ├── Task 18 (AsyncDataTable.LoadAsync) — depends on Task 16, 11
  ├── Task 19 (AsyncDataAdapter) — depends on Task 16, 17, 15
  │
  ├── Task 20 (AsyncDataTableConverter) — depends on Task 16
  ├── Task 21 (AsyncDataSetConverter) — depends on Task 17, 20
  ├── Task 22 (Cross-compat tests) — depends on Task 21
  ├── Task 23 (End-to-end tests) — depends on all above
  ├── Task 24 (XML async I/O) — depends on Task 16, 17
  ├── Task 25 (UpdateAsync) — depends on Task 19
  └── Task 26 (Cleanup + README) — depends on all above
```

## Parallelization Opportunities

These tasks can run in parallel within their groups:
- **Group A (interfaces):** Tasks 2, 3 can run in parallel
- **Group B (base classes):** Tasks 7, 8 can run in parallel
- **Group C (adapters):** Tasks 11, 12 can run in parallel
- **Group D (DataSet):** Tasks 16, 20 can start together (16 first, then 20)
- **Group E (tests):** Tasks 22, 23, 24 can run in parallel once dependencies are met

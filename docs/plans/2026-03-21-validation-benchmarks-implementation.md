# Validation & Benchmarks Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Validate that System.Data.Async produces identical behavior to raw ADO.NET, and measure async wrapper overhead via BenchmarkDotNet.

**Architecture:** Two projects — `System.Data.Async.Validation.Tests` (xUnit, inline comparison pattern) and `System.Data.Async.Benchmarks` (BenchmarkDotNet + custom markdown exporter). Both use SQLite via a configurable `ITestDatabaseProvider` interface.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, BenchmarkDotNet, Microsoft.Data.Sqlite

---

## Task 1: Create Validation.Tests Project Scaffold

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/System.Data.Async.Validation.Tests.csproj`
- Modify: `System.Data.Async.slnx`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.Adapters\System.Data.Async.Adapters.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.*" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
  </ItemGroup>
</Project>
```

**Step 2: Add project to solution**

Add to the `/tests/` folder in `System.Data.Async.slnx`:
```xml
<Project Path="tests/System.Data.Async.Validation.Tests/System.Data.Async.Validation.Tests.csproj" />
```

**Step 3: Build to verify**

Run: `dotnet build tests/System.Data.Async.Validation.Tests`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/System.Data.Async.Validation.Tests.csproj System.Data.Async.slnx
git commit -m "chore: scaffold Validation.Tests project"
```

---

## Task 2: Create ITestDatabaseProvider and SQLite Implementation

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/Infrastructure/ITestDatabaseProvider.cs`
- Create: `tests/System.Data.Async.Validation.Tests/Infrastructure/SqliteTestDatabaseProvider.cs`
- Create: `tests/System.Data.Async.Validation.Tests/Infrastructure/ValidationFixture.cs`
- Create: `tests/System.Data.Async.Validation.Tests/Infrastructure/ValidationCollection.cs`

**Step 1: Create ITestDatabaseProvider**

```csharp
using System.Data.Common;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public interface ITestDatabaseProvider
{
    DbConnection CreateRawConnection();
    IAsyncDbConnection CreateAsyncConnection();
    string ProviderName { get; }
}
```

**Step 2: Create SqliteTestDatabaseProvider**

Uses a shared in-memory SQLite database (file-based URI with `cache=shared` so multiple connections share the same data).

```csharp
using System.Data.Async.Adapters;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public sealed class SqliteTestDatabaseProvider : ITestDatabaseProvider
{
    private readonly string _connectionString;

    public SqliteTestDatabaseProvider(string databaseName)
    {
        // file: URI with cache=shared so all connections see the same in-memory DB
        _connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
    }

    public string ProviderName => "Microsoft.Data.Sqlite";

    public DbConnection CreateRawConnection() => new SqliteConnection(_connectionString);

    public IAsyncDbConnection CreateAsyncConnection() => new SqliteConnection(_connectionString).AsAsync();
}
```

**Step 3: Create ValidationFixture**

```csharp
using System.Data.Common;
using System.Globalization;
using Xunit;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public sealed class ValidationFixture : IAsyncLifetime
{
    private DbConnection? _keepAlive; // keeps shared in-memory DB alive

    public ITestDatabaseProvider Provider { get; } = new SqliteTestDatabaseProvider("ValidationTests");

    public async Task InitializeAsync()
    {
        // Open a connection that stays alive for the duration of the test run
        // (shared in-memory SQLite DBs are dropped when last connection closes)
        _keepAlive = Provider.CreateRawConnection();
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();

        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER,
                Balance REAL,
                CreatedAt TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Product TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                Price REAL NOT NULL,
                OrderDate TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed 50 users
        for (int i = 1; i <= 50; i++)
        {
            using var insertCmd = _keepAlive.CreateCommand();
            insertCmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Users (Name, Email, Age, Balance, CreatedAt, IsActive) VALUES ('User{0}', 'user{0}@test.com', {1}, {2}, '{3}', {4})",
                i,
                20 + (i % 40),
                (i * 100.50).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture),
                i % 3 == 0 ? 0 : 1);
            insertCmd.ExecuteNonQuery();
        }

        // Seed 200 orders
        for (int i = 1; i <= 200; i++)
        {
            using var insertCmd = _keepAlive.CreateCommand();
            insertCmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Orders (UserId, Product, Quantity, Price, OrderDate) VALUES ({0}, 'Product{1}', {2}, {3}, '{4}')",
                ((i - 1) % 50) + 1,
                i,
                1 + (i % 10),
                (i * 9.99).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture));
            insertCmd.ExecuteNonQuery();
        }

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            _keepAlive.Close();
            _keepAlive.Dispose();
        }
        await Task.CompletedTask;
    }
}
```

**Step 4: Create ValidationCollection**

```csharp
using Xunit;

namespace System.Data.Async.Validation.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ValidationCollection : ICollectionFixture<ValidationFixture>
{
    public const string Name = "Validation";
}
```

**Step 5: Build and verify**

Run: `dotnet build tests/System.Data.Async.Validation.Tests`
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/Infrastructure/
git commit -m "feat: add ITestDatabaseProvider, SQLite impl, and ValidationFixture"
```

---

## Task 3: ConnectionParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/ConnectionParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class ConnectionParityTests
{
    private readonly ValidationFixture _fixture;

    public ConnectionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Open_Close_State_Transitions_Match()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.State.Should().Be(ConnectionState.Closed);
        raw.Open();
        raw.State.Should().Be(ConnectionState.Open);
        var rawDbName = raw.Database;
        var rawConnString = raw.ConnectionString;
        raw.Close();
        raw.State.Should().Be(ConnectionState.Closed);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        async_.State.Should().Be(ConnectionState.Closed);
        await async_.OpenAsync();
        async_.State.Should().Be(ConnectionState.Open);
        async_.Database.Should().Be(rawDbName);
        async_.ConnectionString.Should().Be(rawConnString);
        await async_.CloseAsync();
        async_.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task Repeated_Open_Close_Cycles_Match()
    {
        var rawStates = new List<ConnectionState>();
        var asyncStates = new List<ConnectionState>();

        // Raw — 3 cycles
        using var raw = _fixture.Provider.CreateRawConnection();
        for (int i = 0; i < 3; i++)
        {
            raw.Open();
            rawStates.Add(raw.State);
            raw.Close();
            rawStates.Add(raw.State);
        }

        // Async — 3 cycles
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        for (int i = 0; i < 3; i++)
        {
            await async_.OpenAsync();
            asyncStates.Add(async_.State);
            await async_.CloseAsync();
            asyncStates.Add(async_.State);
        }

        asyncStates.Should().BeEquivalentTo(rawStates, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ConnectionTimeout_Property_Matches()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        await using var async_ = _fixture.Provider.CreateAsyncConnection();

        async_.ConnectionTimeout.Should().Be(raw.ConnectionTimeout);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~ConnectionParityTests" -v n`
Expected: All 3 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/ConnectionParityTests.cs
git commit -m "test: add ConnectionParityTests"
```

---

## Task 4: CommandExecutionParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/CommandExecutionParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class CommandExecutionParityTests
{
    private readonly ValidationFixture _fixture;

    public CommandExecutionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExecuteScalar_Returns_Same_Value()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var rawResult = rawCmd.ExecuteScalar();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var asyncResult = await asyncCmd.ExecuteScalarAsync();

        Convert.ToInt64(asyncResult, CultureInfo.InvariantCulture)
            .Should().Be(Convert.ToInt64(rawResult, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ExecuteNonQuery_Returns_Same_Affected_Rows()
    {
        // Use a temp table so we don't affect shared data
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var setup1 = raw.CreateCommand();
        setup1.CommandText = "CREATE TABLE IF NOT EXISTS TempNQ (Id INTEGER PRIMARY KEY, Val TEXT)";
        setup1.ExecuteNonQuery();
        using var rawIns = raw.CreateCommand();
        rawIns.CommandText = "INSERT INTO TempNQ VALUES (1, 'a'), (2, 'b')";
        var rawInserted = rawIns.ExecuteNonQuery();
        using var rawUpd = raw.CreateCommand();
        rawUpd.CommandText = "UPDATE TempNQ SET Val = 'x'";
        var rawUpdated = rawUpd.ExecuteNonQuery();
        using var rawDel = raw.CreateCommand();
        rawDel.CommandText = "DELETE FROM TempNQ";
        var rawDeleted = rawDel.ExecuteNonQuery();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var setup2 = async_.CreateCommand();
        setup2.CommandText = "CREATE TABLE IF NOT EXISTS TempNQAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await setup2.ExecuteNonQueryAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.CommandText = "INSERT INTO TempNQAsync VALUES (1, 'a'), (2, 'b')";
        var asyncInserted = await asyncIns.ExecuteNonQueryAsync();
        var asyncUpd = async_.CreateCommand();
        asyncUpd.CommandText = "UPDATE TempNQAsync SET Val = 'x'";
        var asyncUpdated = await asyncUpd.ExecuteNonQueryAsync();
        var asyncDel = async_.CreateCommand();
        asyncDel.CommandText = "DELETE FROM TempNQAsync";
        var asyncDeleted = await asyncDel.ExecuteNonQueryAsync();

        asyncInserted.Should().Be(rawInserted);
        asyncUpdated.Should().Be(rawUpdated);
        asyncDeleted.Should().Be(rawDeleted);
    }

    [Fact]
    public async Task ExecuteReader_Returns_Same_Data()
    {
        var rawRows = new List<(long Id, string Name, string Email)>();
        var asyncRows = new List<(long Id, string Name, string Email)>();

        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read())
        {
            rawRows.Add((rawReader.GetInt64(0), rawReader.GetString(1), rawReader.GetString(2)));
        }

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync())
        {
            asyncRows.Add((asyncReader.GetInt64(0), asyncReader.GetString(1), asyncReader.GetString(2)));
        }

        asyncRows.Should().BeEquivalentTo(rawRows, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Parameterized_Query_Returns_Same_Results()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age AND IsActive = @active ORDER BY Id";
        var p1 = rawCmd.CreateParameter();
        p1.ParameterName = "@age";
        p1.Value = 30;
        rawCmd.Parameters.Add(p1);
        var p2 = rawCmd.CreateParameter();
        p2.ParameterName = "@active";
        p2.Value = 1;
        rawCmd.Parameters.Add(p2);
        var rawNames = new List<string>();
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read()) rawNames.Add(rawReader.GetString(0));

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age AND IsActive = @active ORDER BY Id";
        var ap1 = asyncCmd.CreateParameter();
        ap1.ParameterName = "@age";
        ap1.Value = 30;
        asyncCmd.Parameters.Add(ap1);
        var ap2 = asyncCmd.CreateParameter();
        ap2.ParameterName = "@active";
        ap2.Value = 1;
        asyncCmd.Parameters.Add(ap2);
        var asyncNames = new List<string>();
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync()) asyncNames.Add(asyncReader.GetString(0));

        asyncNames.Should().BeEquivalentTo(rawNames, opts => opts.WithStrictOrdering());
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~CommandExecutionParityTests" -v n`
Expected: All 4 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/CommandExecutionParityTests.cs
git commit -m "test: add CommandExecutionParityTests"
```

---

## Task 5: ReaderParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/ReaderParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class ReaderParityTests
{
    private readonly ValidationFixture _fixture;

    public ReaderParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Field_Access_By_Index_And_Name_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email, Age FROM Users WHERE Id = 1";
        using var rawReader = rawCmd.ExecuteReader();
        rawReader.Read();
        var rawById = (rawReader.GetInt64(0), rawReader.GetString(1), rawReader.GetString(2), rawReader.GetInt64(3));
        var rawByName = (rawReader["Id"], rawReader["Name"], rawReader["Email"], rawReader["Age"]);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email, Age FROM Users WHERE Id = 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        await asyncReader.ReadAsync();
        var asyncById = (asyncReader.GetInt64(0), asyncReader.GetString(1), asyncReader.GetString(2), asyncReader.GetInt64(3));
        var asyncByName = (asyncReader["Id"], asyncReader["Name"], asyncReader["Email"], asyncReader["Age"]);

        asyncById.Should().Be(rawById);
        asyncByName.Should().BeEquivalentTo(rawByName);
    }

    [Fact]
    public async Task FieldCount_And_GetName_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawFieldCount = rawReader.FieldCount;
        var rawNames = Enumerable.Range(0, rawFieldCount).Select(rawReader.GetName).ToList();
        var rawOrdinals = rawNames.Select(rawReader.GetOrdinal).ToList();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncFieldCount = asyncReader.FieldCount;
        var asyncNames = Enumerable.Range(0, asyncFieldCount).Select(asyncReader.GetName).ToList();
        var asyncOrdinals = asyncNames.Select(asyncReader.GetOrdinal).ToList();

        asyncFieldCount.Should().Be(rawFieldCount);
        asyncNames.Should().BeEquivalentTo(rawNames, opts => opts.WithStrictOrdering());
        asyncOrdinals.Should().BeEquivalentTo(rawOrdinals, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task IsDBNull_Matches_For_Null_Values()
    {
        // Insert a row with NULL Email
        using var setup = _fixture.Provider.CreateRawConnection();
        setup.Open();
        using var setupCmd = setup.CreateCommand();
        setupCmd.CommandText = "CREATE TABLE IF NOT EXISTS NullTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        setupCmd.ExecuteNonQuery();
        using var insCmd = setup.CreateCommand();
        insCmd.CommandText = "INSERT OR IGNORE INTO NullTest VALUES (1, NULL), (2, 'hello')";
        insCmd.ExecuteNonQuery();

        var rawNulls = new List<bool>();
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Val FROM NullTest ORDER BY Id";
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read()) rawNulls.Add(rawReader.IsDBNull(0));

        var asyncNulls = new List<bool>();
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Val FROM NullTest ORDER BY Id";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync()) asyncNulls.Add(await asyncReader.IsDBNullAsync(0));

        asyncNulls.Should().BeEquivalentTo(rawNulls, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetSchemaTable_Returns_Equivalent_Schema()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawSchema = rawReader.GetSchemaTable();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncSchema = await asyncReader.GetSchemaTableAsync();

        asyncSchema!.Rows.Count.Should().Be(rawSchema!.Rows.Count);
        for (int i = 0; i < rawSchema.Rows.Count; i++)
        {
            asyncSchema.Rows[i]["ColumnName"].Should().Be(rawSchema.Rows[i]["ColumnName"]);
        }
    }

    [Fact]
    public async Task AwaitForeach_Produces_Same_Data_As_Manual_Loop()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        // Manual loop
        var manualCmd = conn.CreateCommand();
        manualCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var manualRows = new List<(long, string)>();
        await using var reader1 = await manualCmd.ExecuteReaderAsync();
        while (await reader1.ReadAsync())
        {
            manualRows.Add((reader1.GetInt64(0), reader1.GetString(1)));
        }

        // await foreach
        var foreachCmd = conn.CreateCommand();
        foreachCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var foreachRows = new List<(long, string)>();
        await using var reader2 = await foreachCmd.ExecuteReaderAsync();
        await foreach (var record in reader2)
        {
            foreachRows.Add((record.GetInt64(0), record.GetString(1)));
        }

        foreachRows.Should().BeEquivalentTo(manualRows, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task HasRows_And_RecordsAffected_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM Users LIMIT 5";
        using var rawReader = rawCmd.ExecuteReader();
        var rawHasRows = rawReader.HasRows;
        var rawRecordsAffected = rawReader.RecordsAffected;

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM Users LIMIT 5";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncHasRows = asyncReader.HasRows;
        var asyncRecordsAffected = asyncReader.RecordsAffected;

        asyncHasRows.Should().Be(rawHasRows);
        asyncRecordsAffected.Should().Be(rawRecordsAffected);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~ReaderParityTests" -v n`
Expected: All 6 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/ReaderParityTests.cs
git commit -m "test: add ReaderParityTests"
```

---

## Task 6: TransactionParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/TransactionParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class TransactionParityTests
{
    private readonly ValidationFixture _fixture;

    public TransactionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Commit_Persists_Data_Same_As_Raw()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCreate = raw.CreateCommand();
        rawCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxCommitRaw (Id INTEGER PRIMARY KEY, Val TEXT)";
        rawCreate.ExecuteNonQuery();
        using var rawTx = raw.BeginTransaction();
        using var rawIns = raw.CreateCommand();
        rawIns.Transaction = rawTx;
        rawIns.CommandText = "INSERT INTO TxCommitRaw VALUES (1, 'committed')";
        rawIns.ExecuteNonQuery();
        rawTx.Commit();
        using var rawCheck = raw.CreateCommand();
        rawCheck.CommandText = "SELECT COUNT(*) FROM TxCommitRaw";
        var rawCount = Convert.ToInt64(rawCheck.ExecuteScalar(), CultureInfo.InvariantCulture);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCreate = async_.CreateCommand();
        asyncCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxCommitAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await asyncCreate.ExecuteNonQueryAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.Transaction = asyncTx;
        asyncIns.CommandText = "INSERT INTO TxCommitAsync VALUES (1, 'committed')";
        await asyncIns.ExecuteNonQueryAsync();
        await asyncTx.CommitAsync();
        var asyncCheck = async_.CreateCommand();
        asyncCheck.CommandText = "SELECT COUNT(*) FROM TxCommitAsync";
        var asyncCount = Convert.ToInt64(await asyncCheck.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        asyncCount.Should().Be(rawCount);
    }

    [Fact]
    public async Task Rollback_Reverts_Data_Same_As_Raw()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCreate = raw.CreateCommand();
        rawCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxRollbackRaw (Id INTEGER PRIMARY KEY, Val TEXT)";
        rawCreate.ExecuteNonQuery();
        using var rawTx = raw.BeginTransaction();
        using var rawIns = raw.CreateCommand();
        rawIns.Transaction = rawTx;
        rawIns.CommandText = "INSERT INTO TxRollbackRaw VALUES (1, 'rolled-back')";
        rawIns.ExecuteNonQuery();
        rawTx.Rollback();
        using var rawCheck = raw.CreateCommand();
        rawCheck.CommandText = "SELECT COUNT(*) FROM TxRollbackRaw";
        var rawCount = Convert.ToInt64(rawCheck.ExecuteScalar(), CultureInfo.InvariantCulture);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCreate = async_.CreateCommand();
        asyncCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxRollbackAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await asyncCreate.ExecuteNonQueryAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.Transaction = asyncTx;
        asyncIns.CommandText = "INSERT INTO TxRollbackAsync VALUES (1, 'rolled-back')";
        await asyncIns.ExecuteNonQueryAsync();
        await asyncTx.RollbackAsync();
        var asyncCheck = async_.CreateCommand();
        asyncCheck.CommandText = "SELECT COUNT(*) FROM TxRollbackAsync";
        var asyncCount = Convert.ToInt64(await asyncCheck.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        asyncCount.Should().Be(rawCount);
        asyncCount.Should().Be(0);
    }

    [Fact]
    public async Task IsolationLevel_Matches()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawTx = raw.BeginTransaction();
        var rawIso = rawTx.IsolationLevel;
        rawTx.Rollback();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIso = asyncTx.IsolationLevel;
        await asyncTx.RollbackAsync();

        asyncIso.Should().Be(rawIso);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~TransactionParityTests" -v n`
Expected: All 3 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/TransactionParityTests.cs
git commit -m "test: add TransactionParityTests"
```

---

## Task 7: DataAdapterParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/DataAdapterParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Data.Common;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class DataAdapterParityTests
{
    private readonly ValidationFixture _fixture;

    public DataAdapterParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Fill_Produces_Same_RowCount_And_Data()
    {
        // Raw: use DbDataReader to manually load a DataTable
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id";
        var rawTable = new DataTable("Users");
        using var rawReader = rawCmd.ExecuteReader();
        rawTable.Load(rawReader);

        // Async: use AdapterDbDataAdapter.FillAsync
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncSelectCmd = async_.CreateCommand();
        asyncSelectCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id";
        var adapter = new AdapterDbDataAdapter(asyncSelectCmd);
        var asyncTable = new AsyncDataTable("Users");
        await adapter.FillAsync(asyncTable);

        asyncTable.Rows.Count.Should().Be(rawTable.Rows.Count);
        for (int i = 0; i < rawTable.Rows.Count; i++)
        {
            asyncTable.Rows[i]["Id"].Should().Be(rawTable.Rows[i]["Id"]);
            asyncTable.Rows[i]["Name"].Should().Be(rawTable.Rows[i]["Name"]);
            asyncTable.Rows[i]["Email"].Should().Be(rawTable.Rows[i]["Email"]);
        }
    }

    [Fact]
    public async Task Fill_AsyncDataSet_Produces_Same_Data()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var rawDs = new System.Data.DataSet("TestDS");
        var rawTable = new DataTable("Users");
        rawDs.Tables.Add(rawTable);
        using var rawReader = rawCmd.ExecuteReader();
        rawTable.Load(rawReader);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var adapter = new AdapterDbDataAdapter(asyncCmd);
        var asyncDs = new AsyncDataSet("TestDS");
        await adapter.FillAsync(asyncDs);

        asyncDs.InnerDataSet.Tables.Count.Should().Be(rawDs.Tables.Count);
        asyncDs.InnerDataSet.Tables[0].Rows.Count.Should().Be(rawDs.Tables[0].Rows.Count);
    }

    [Fact]
    public async Task Update_Roundtrip_Produces_Same_Affected_Rows()
    {
        // Setup: create table with data for update test
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS AdapterUpdateTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        await createCmd.ExecuteNonQueryAsync();

        var insCmd = conn.CreateCommand();
        insCmd.CommandText = "INSERT OR IGNORE INTO AdapterUpdateTest VALUES (1, 'original'), (2, 'original')";
        await insCmd.ExecuteNonQueryAsync();

        // Fill
        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Val FROM AdapterUpdateTest";

        var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE AdapterUpdateTest SET Val = @Val WHERE Id = @Id";
        var pVal = updateCmd.CreateParameter();
        pVal.ParameterName = "@Val";
        pVal.SourceColumn = "Val";
        updateCmd.Parameters.Add(pVal);
        var pId = updateCmd.CreateParameter();
        pId.ParameterName = "@Id";
        pId.SourceColumn = "Id";
        updateCmd.Parameters.Add(pId);

        var adapter = new AdapterDbDataAdapter(selectCmd) { UpdateCommand = updateCmd };
        var table = new AsyncDataTable("AdapterUpdateTest");
        await adapter.FillAsync(table);

        // Modify
        table.Rows[0]["Val"] = "modified";

        // Update
        var affected = await adapter.UpdateAsync(table);
        affected.Should().BeGreaterThan(0);

        // Verify round-trip
        var verifyTable = new AsyncDataTable("AdapterUpdateTest");
        await adapter.FillAsync(verifyTable);
        ((string)verifyTable.Rows[0]["Val"]).Should().Be("modified");
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~DataAdapterParityTests" -v n`
Expected: All 3 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/DataAdapterParityTests.cs
git commit -m "test: add DataAdapterParityTests"
```

---

## Task 8: SerializationParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/SerializationParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Adapters;
using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class SerializationParityTests
{
    private readonly ValidationFixture _fixture;

    public SerializationParityTests(ValidationFixture fixture) => _fixture = fixture;

    private async Task<AsyncDataTable> LoadUsersTableAsync()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        var adapter = new AdapterDbDataAdapter(cmd);
        var table = new AsyncDataTable("Users");
        await adapter.FillAsync(table);
        return table;
    }

    [Fact]
    public async Task Xml_WriteRead_Roundtrip_Preserves_Data()
    {
        var original = await LoadUsersTableAsync();

        // Write to XML
        using var xmlStream = new MemoryStream();
        await original.WriteXmlAsync(xmlStream);
        xmlStream.Position = 0;

        // Read back
        var restored = new AsyncDataTable("Users");
        await restored.ReadXmlAsync(xmlStream);

        restored.Rows.Count.Should().Be(original.Rows.Count);
        for (int i = 0; i < original.Rows.Count; i++)
        {
            restored.Rows[i]["Name"].Should().Be(original.Rows[i]["Name"]);
            restored.Rows[i]["Email"].Should().Be(original.Rows[i]["Email"]);
        }
    }

    [Fact]
    public async Task Xml_Schema_Roundtrip_Preserves_Columns()
    {
        var original = await LoadUsersTableAsync();

        using var schemaStream = new MemoryStream();
        await original.WriteXmlSchemaAsync(schemaStream);
        schemaStream.Position = 0;

        var restored = new AsyncDataTable("Users");
        await restored.ReadXmlSchemaAsync(schemaStream);

        restored.Columns.Count.Should().Be(original.Columns.Count);
        for (int i = 0; i < original.Columns.Count; i++)
        {
            restored.Columns[i].ColumnName.Should().Be(original.Columns[i].ColumnName);
        }
    }

    [Fact]
    public async Task Json_Roundtrip_Preserves_Data()
    {
        var original = await LoadUsersTableAsync();

        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());

        var json = JsonConvert.SerializeObject(original, settings);
        var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);

        restored!.Rows.Count.Should().Be(original.Rows.Count);
        for (int i = 0; i < original.Rows.Count; i++)
        {
            restored.Rows[i]["Name"].Should().Be(original.Rows[i]["Name"]);
        }
    }

    [Fact]
    public async Task AsyncDataSet_Xml_Roundtrip_Preserves_Data()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 5";
        var adapter = new AdapterDbDataAdapter(cmd);
        var ds = new AsyncDataSet("TestDS");
        await adapter.FillAsync(ds);

        using var xmlStream = new MemoryStream();
        await ds.WriteXmlAsync(xmlStream);
        xmlStream.Position = 0;

        var restored = new AsyncDataSet("TestDS");
        await restored.ReadXmlAsync(xmlStream);

        restored.InnerDataSet.Tables.Count.Should().Be(ds.InnerDataSet.Tables.Count);
        restored.InnerDataSet.Tables[0].Rows.Count.Should().Be(ds.InnerDataSet.Tables[0].Rows.Count);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~SerializationParityTests" -v n`
Expected: All 4 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/SerializationParityTests.cs
git commit -m "test: add SerializationParityTests"
```

---

## Task 9: EventParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/EventParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class EventParityTests
{
    private readonly ValidationFixture _fixture;

    public EventParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public void RowChanged_And_RowChanging_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        // Raw DataTable
        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.Columns.Add("Name", typeof(string));
        rawTable.RowChanging += (_, e) => rawEvents.Add($"Changing:{e.Action}");
        rawTable.RowChanged += (_, e) => rawEvents.Add($"Changed:{e.Action}");

        var rawRow = rawTable.NewRow();
        rawRow["Id"] = 1;
        rawRow["Name"] = "Alice";
        rawTable.Rows.Add(rawRow);
        rawRow["Name"] = "Updated";

        // AsyncDataTable
        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.Columns.Add("Name", typeof(string));
        asyncTable.RowChanging += (_, e) => asyncEvents.Add($"Changing:{e.Action}");
        asyncTable.RowChanged += (_, e) => asyncEvents.Add($"Changed:{e.Action}");

        var asyncRow = asyncTable.NewRow();
        asyncRow["Id"] = 1;
        asyncRow["Name"] = "Alice";
        asyncTable.Rows.Add(asyncRow);
        asyncRow["Name"] = "Updated";

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void RowDeleted_And_RowDeleting_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.RowDeleting += (_, e) => rawEvents.Add($"Deleting:{e.Action}");
        rawTable.RowDeleted += (_, e) => rawEvents.Add($"Deleted:{e.Action}");
        var rawRow = rawTable.NewRow();
        rawRow["Id"] = 1;
        rawTable.Rows.Add(rawRow);
        rawTable.AcceptChanges();
        rawRow.Delete();

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.RowDeleting += (_, e) => asyncEvents.Add($"Deleting:{e.Action}");
        asyncTable.RowDeleted += (_, e) => asyncEvents.Add($"Deleted:{e.Action}");
        var asyncRow = asyncTable.NewRow();
        asyncRow["Id"] = 1;
        asyncTable.Rows.Add(asyncRow);
        asyncTable.AcceptChanges();
        asyncRow.Delete();

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void ColumnChanged_And_ColumnChanging_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Val", typeof(string));
        rawTable.ColumnChanging += (_, e) => rawEvents.Add($"Changing:{e.Column!.ColumnName}");
        rawTable.ColumnChanged += (_, e) => rawEvents.Add($"Changed:{e.Column!.ColumnName}");
        var rawRow = rawTable.NewRow();
        rawRow["Val"] = "original";
        rawTable.Rows.Add(rawRow);
        rawRow["Val"] = "updated";

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Val", typeof(string));
        asyncTable.ColumnChanging += (_, e) => asyncEvents.Add($"Changing:{e.Column!.ColumnName}");
        asyncTable.ColumnChanged += (_, e) => asyncEvents.Add($"Changed:{e.Column!.ColumnName}");
        var asyncRow = asyncTable.NewRow();
        asyncRow["Val"] = "original";
        asyncTable.Rows.Add(asyncRow);
        asyncRow["Val"] = "updated";

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void TableCleared_And_TableClearing_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.Rows.Add(rawTable.NewRow());
        rawTable.TableClearing += (_, _) => rawEvents.Add("Clearing");
        rawTable.TableCleared += (_, _) => rawEvents.Add("Cleared");
        rawTable.Clear();

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.Rows.Add(asyncTable.NewRow());
        asyncTable.TableClearing += (_, _) => asyncEvents.Add("Clearing");
        asyncTable.TableCleared += (_, _) => asyncEvents.Add("Cleared");
        asyncTable.Clear();

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void TableNewRow_Fires_Same_As_Raw()
    {
        var rawFired = false;
        var asyncFired = false;

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.TableNewRow += (_, _) => rawFired = true;
        rawTable.Rows.Add(rawTable.NewRow());

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.TableNewRow += (_, _) => asyncFired = true;
        asyncTable.Rows.Add(asyncTable.NewRow());

        asyncFired.Should().Be(rawFired);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~EventParityTests" -v n`
Expected: All 5 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/EventParityTests.cs
git commit -m "test: add EventParityTests"
```

---

## Task 10: EdgeCaseParityTests

**Files:**
- Create: `tests/System.Data.Async.Validation.Tests/EdgeCaseParityTests.cs`

**Step 1: Write the tests**

```csharp
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class EdgeCaseParityTests
{
    private readonly ValidationFixture _fixture;

    public EdgeCaseParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Empty_ResultSet_Behaves_Same()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM Users WHERE Id = -1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawHasRows = rawReader.HasRows;
        var rawReadResult = rawReader.Read();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM Users WHERE Id = -1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncHasRows = asyncReader.HasRows;
        var asyncReadResult = await asyncReader.ReadAsync();

        asyncHasRows.Should().Be(rawHasRows);
        asyncReadResult.Should().Be(rawReadResult);
    }

    [Fact]
    public async Task Large_ResultSet_Returns_All_Rows()
    {
        // Insert 1000 rows into a temp table
        using var setup = _fixture.Provider.CreateRawConnection();
        setup.Open();
        using var createCmd = setup.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS LargeTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        createCmd.ExecuteNonQuery();
        using var insertCmd = setup.CreateCommand();
        var sb = new System.Text.StringBuilder("INSERT OR IGNORE INTO LargeTest VALUES ");
        for (int i = 1; i <= 1000; i++)
        {
            if (i > 1) sb.Append(", ");
            sb.Append(CultureInfo.InvariantCulture, $"({i}, 'val{i}')");
        }
        insertCmd.CommandText = sb.ToString();
        insertCmd.ExecuteNonQuery();

        // Raw count
        using var rawCmd = setup.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM LargeTest";
        using var rawReader = rawCmd.ExecuteReader();
        int rawCount = 0;
        while (rawReader.Read()) rawCount++;

        // Async count
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM LargeTest";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        int asyncCount = 0;
        while (await asyncReader.ReadAsync()) asyncCount++;

        asyncCount.Should().Be(rawCount);
        asyncCount.Should().Be(1000);
    }

    [Fact]
    public async Task Fill_Empty_Table_Produces_Zero_Rows()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS EmptyFillTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        await createCmd.ExecuteNonQueryAsync();

        var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM EmptyFillTest";
        await deleteCmd.ExecuteNonQueryAsync();

        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM EmptyFillTest";
        var adapter = new AdapterDbDataAdapter(selectCmd);
        var table = new AsyncDataTable("EmptyFillTest");
        var rowCount = await adapter.FillAsync(table);

        rowCount.Should().Be(0);
        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task CancellationToken_Is_Respected()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users";

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        Func<Task> act = async () =>
        {
            await using var reader = await cmd.ExecuteReaderAsync(cts.Token);
            while (await reader.ReadAsync(cts.Token)) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/System.Data.Async.Validation.Tests --filter "FullyQualifiedName~EdgeCaseParityTests" -v n`
Expected: All 4 tests pass.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Validation.Tests/EdgeCaseParityTests.cs
git commit -m "test: add EdgeCaseParityTests"
```

---

## Task 11: Create Benchmarks Project Scaffold

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/System.Data.Async.Benchmarks.csproj`
- Modify: `System.Data.Async.slnx`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\System.Data.Async.Adapters\System.Data.Async.Adapters.csproj" />
    <ProjectReference Include="..\..\src\System.Data.Async.DataSet\System.Data.Async.DataSet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.*" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.*" />
  </ItemGroup>
</Project>
```

**Step 2: Add to solution**

Add to `/tests/` folder in `System.Data.Async.slnx`:
```xml
<Project Path="tests/System.Data.Async.Benchmarks/System.Data.Async.Benchmarks.csproj" />
```

**Step 3: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/System.Data.Async.Benchmarks.csproj System.Data.Async.slnx
git commit -m "chore: scaffold Benchmarks project"
```

---

## Task 12: Benchmark Infrastructure (BenchmarkBase + Program.cs)

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/Infrastructure/BenchmarkBase.cs`
- Create: `tests/System.Data.Async.Benchmarks/Program.cs`

**Step 1: Create BenchmarkBase**

Shared setup/cleanup for all benchmark classes. Creates an in-memory SQLite DB, seeds data, and provides both raw and async connections.

```csharp
using System.Data.Async.Adapters;
using System.Data.Common;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Benchmarks.Infrastructure;

public abstract class BenchmarkBase
{
    private SqliteConnection _keepAlive = null!;
    protected DbConnection RawConnection = null!;
    protected IAsyncDbConnection AsyncConnection = null!;

    private readonly string _dbName;
    private string ConnectionString => $"Data Source={_dbName};Mode=Memory;Cache=Shared";

    protected BenchmarkBase()
    {
        _dbName = GetType().Name;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER,
                Balance REAL,
                CreatedAt TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Product TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                Price REAL NOT NULL,
                OrderDate TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed
        for (int i = 1; i <= 50; i++)
        {
            using var ins = _keepAlive.CreateCommand();
            ins.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT OR IGNORE INTO Users (Id, Name, Email, Age, Balance, CreatedAt, IsActive) VALUES ({0}, 'User{0}', 'user{0}@test.com', {1}, {2}, '{3}', {4})",
                i, 20 + (i % 40),
                (i * 100.50).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture),
                i % 3 == 0 ? 0 : 1);
            ins.ExecuteNonQuery();
        }
        for (int i = 1; i <= 200; i++)
        {
            using var ins = _keepAlive.CreateCommand();
            ins.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT OR IGNORE INTO Orders (Id, UserId, Product, Quantity, Price, OrderDate) VALUES ({0}, {1}, 'Product{0}', {2}, {3}, '{4}')",
                i, ((i - 1) % 50) + 1, 1 + (i % 10),
                (i * 9.99).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture));
            ins.ExecuteNonQuery();
        }

        RawConnection = new SqliteConnection(ConnectionString);
        RawConnection.Open();
        AsyncConnection = new SqliteConnection(ConnectionString).AsAsync();
        AsyncConnection.OpenAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        RawConnection?.Dispose();
        (AsyncConnection as IDisposable)?.Dispose();
        _keepAlive?.Dispose();
    }
}
```

**Step 2: Create Program.cs**

```csharp
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

**Step 3: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/Infrastructure/ tests/System.Data.Async.Benchmarks/Program.cs
git commit -m "feat: add BenchmarkBase infrastructure and Program.cs"
```

---

## Task 13: ConnectionBenchmarks

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/ConnectionBenchmarks.cs`

**Step 1: Write the benchmark**

```csharp
using System.Data.Async.Adapters;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class ConnectionBenchmarks
{
    private const string ConnStr = "Data Source=ConnBench;Mode=Memory;Cache=Shared";
    private SqliteConnection _keepAlive = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keepAlive = new SqliteConnection(ConnStr);
        _keepAlive.Open();
    }

    [GlobalCleanup]
    public void Cleanup() => _keepAlive?.Dispose();

    [Benchmark(Baseline = true)]
    public void Raw_OpenClose()
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        conn.Close();
    }

    [Benchmark]
    public async Task Async_OpenClose()
    {
        await using var conn = new SqliteConnection(ConnStr).AsAsync();
        await conn.OpenAsync();
        await conn.CloseAsync();
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/ConnectionBenchmarks.cs
git commit -m "bench: add ConnectionBenchmarks"
```

---

## Task 14: CommandExecutionBenchmarks

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/CommandExecutionBenchmarks.cs`

**Step 1: Write the benchmark**

```csharp
using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class CommandExecutionBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public object Raw_ExecuteScalar()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users";
        return cmd.ExecuteScalar()!;
    }

    [Benchmark]
    public async Task<object> Async_ExecuteScalar()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users";
        return (await cmd.ExecuteScalarAsync())!;
    }

    [Benchmark]
    public int Raw_ExecuteNonQuery()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        return cmd.ExecuteNonQuery();
    }

    [Benchmark]
    public async Task<int> Async_ExecuteNonQuery()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        return await cmd.ExecuteNonQueryAsync();
    }

    [Benchmark]
    public int Raw_ExecuteReader_Iterate()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";
        using var reader = cmd.ExecuteReader();
        int count = 0;
        while (reader.Read()) count++;
        return count;
    }

    [Benchmark]
    public async Task<int> Async_ExecuteReader_Iterate()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        int count = 0;
        while (await reader.ReadAsync()) count++;
        return count;
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/CommandExecutionBenchmarks.cs
git commit -m "bench: add CommandExecutionBenchmarks"
```

---

## Task 15: ReaderBenchmarks

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/ReaderBenchmarks.cs`

**Step 1: Write the benchmark**

```csharp
using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class ReaderBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public List<string> Raw_ReadAll_Fields()
    {
        var results = new List<string>();
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(1)); // Name
        }
        return results;
    }

    [Benchmark]
    public async Task<List<string>> Async_ReadAll_ManualLoop()
    {
        var results = new List<string>();
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(1));
        }
        return results;
    }

    [Benchmark]
    public async Task<List<string>> Async_ReadAll_AwaitForeach()
    {
        var results = new List<string>();
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        await foreach (var record in reader)
        {
            results.Add(record.GetString(1));
        }
        return results;
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/ReaderBenchmarks.cs
git commit -m "bench: add ReaderBenchmarks"
```

---

## Task 16: TransactionBenchmarks

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/TransactionBenchmarks.cs`

**Step 1: Write the benchmark**

```csharp
using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class TransactionBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public void Raw_BeginCommit()
    {
        using var tx = RawConnection.BeginTransaction();
        using var cmd = RawConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    [Benchmark]
    public async Task Async_BeginCommit()
    {
        await using var tx = await AsyncConnection.BeginTransactionAsync();
        var cmd = AsyncConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    [Benchmark]
    public void Raw_BeginRollback()
    {
        using var tx = RawConnection.BeginTransaction();
        using var cmd = RawConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        cmd.ExecuteNonQuery();
        tx.Rollback();
    }

    [Benchmark]
    public async Task Async_BeginRollback()
    {
        await using var tx = await AsyncConnection.BeginTransactionAsync();
        var cmd = AsyncConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        await cmd.ExecuteNonQueryAsync();
        await tx.RollbackAsync();
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/TransactionBenchmarks.cs
git commit -m "bench: add TransactionBenchmarks"
```

---

## Task 17: DataAdapterBenchmarks

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/DataAdapterBenchmarks.cs`

**Step 1: Write the benchmark**

```csharp
using System.Data.Async.Adapters;
using System.Data.Async.Benchmarks.Infrastructure;
using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class DataAdapterBenchmarks : BenchmarkBase
{
    [Params(10, 100)]
    public int RowLimit { get; set; }

    [Benchmark(Baseline = true)]
    public DataTable Raw_Fill()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM Orders LIMIT {RowLimit}";
        var table = new DataTable("Orders");
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    [Benchmark]
    public async Task<AsyncDataTable> Async_Fill()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM Orders LIMIT {RowLimit}";
        var adapter = new AdapterDbDataAdapter(cmd);
        var table = new AsyncDataTable("Orders");
        await adapter.FillAsync(table);
        return table;
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/DataAdapterBenchmarks.cs
git commit -m "bench: add DataAdapterBenchmarks"
```

---

## Task 18: Custom Markdown Summary Exporter

**Files:**
- Create: `tests/System.Data.Async.Benchmarks/Infrastructure/AsyncParityExporter.cs`

**Step 1: Write the exporter**

```csharp
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace System.Data.Async.Benchmarks.Infrastructure;

public sealed class AsyncParityExporter : IExporter
{
    public string Name => "AsyncParity";

    public void ExportToLog(Summary summary, ILogger logger)
    {
        logger.WriteLine();
        logger.WriteLine("# Async vs Raw Parity Summary");
        logger.WriteLine();
        logger.WriteLine("| Operation | Raw Mean (ns) | Async Mean (ns) | Delta % | Raw Alloc (B) | Async Alloc (B) | Alloc Delta (B) | Status |");
        logger.WriteLine("|-----------|---------------|-----------------|---------|---------------|-----------------|-----------------|--------|");

        // Group benchmarks by base name (strip Raw_/Async_ prefix)
        var groups = summary.BenchmarksCases
            .GroupBy(b =>
            {
                var name = b.Descriptor.WorkloadMethod.Name;
                if (name.StartsWith("Raw_", StringComparison.Ordinal))
                    return name["Raw_".Length..];
                if (name.StartsWith("Async_", StringComparison.Ordinal))
                    return name["Async_".Length..];
                return name;
            })
            .Where(g => g.Count() >= 2);

        foreach (var group in groups)
        {
            var rawCase = group.FirstOrDefault(b => b.Descriptor.WorkloadMethod.Name.StartsWith("Raw_", StringComparison.Ordinal));
            var asyncCase = group.FirstOrDefault(b => b.Descriptor.WorkloadMethod.Name.StartsWith("Async_", StringComparison.Ordinal));

            if (rawCase is null || asyncCase is null) continue;

            var rawReport = summary[rawCase];
            var asyncReport = summary[asyncCase];

            if (rawReport?.ResultStatistics is null || asyncReport?.ResultStatistics is null) continue;

            var rawMean = rawReport.ResultStatistics.Mean;
            var asyncMean = asyncReport.ResultStatistics.Mean;
            var deltaPct = ((asyncMean - rawMean) / rawMean) * 100;

            var rawAlloc = rawReport.GcStats.GetBytesAllocatedPerOperation(rawCase);
            var asyncAlloc = asyncReport.GcStats.GetBytesAllocatedPerOperation(asyncCase);
            var allocDelta = asyncAlloc - rawAlloc;

            var status = deltaPct > 20 ? "WARNING" : "OK";

            var paramSuffix = rawCase.HasParameters ? $" ({rawCase.Parameters.DisplayInfo})" : "";

            logger.WriteLine(
                $"| {group.Key}{paramSuffix} | {rawMean:N0} | {asyncMean:N0} | {deltaPct:+0.0;-0.0}% | {rawAlloc} | {asyncAlloc} | {allocDelta:+0;-0} | {status} |");
        }

        logger.WriteLine();
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, "async-parity-summary.md");
        using var writer = new StreamWriter(filePath);
        var logger = new StreamLogger(writer);
        ExportToLog(summary, logger);
        return [filePath];
    }
}
```

**Step 2: Update Program.cs to use the exporter**

Replace `Program.cs` with:

```csharp
using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = ManualConfig.CreateMinimumViable()
    .AddExporter(new AsyncParityExporter());

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
```

**Step 3: Build to verify**

Run: `dotnet build tests/System.Data.Async.Benchmarks`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add tests/System.Data.Async.Benchmarks/Infrastructure/AsyncParityExporter.cs tests/System.Data.Async.Benchmarks/Program.cs
git commit -m "feat: add AsyncParityExporter custom markdown summary"
```

---

## Task 19: Run All Validation Tests

**Step 1: Run the full validation test suite**

Run: `dotnet test tests/System.Data.Async.Validation.Tests -v n`
Expected: All 28 tests pass.

**Step 2: Fix any failures**

If any tests fail, investigate and fix. The failures reveal real parity issues between the library and raw ADO.NET.

---

## Task 20: Run Benchmarks (Dry Run)

**Step 1: Run a short dry-run to verify benchmarks execute**

Run: `dotnet run --project tests/System.Data.Async.Benchmarks -c Release -- --job short --filter *ConnectionBenchmarks*`
Expected: Benchmark completes, produces results table and `async-parity-summary.md`.

**Step 2: Commit any final fixes**

```bash
git add -A
git commit -m "chore: finalize validation tests and benchmarks"
```

---

## Summary

| Task | What | Files |
|------|------|-------|
| 1 | Validation.Tests scaffold | csproj, slnx |
| 2 | ITestDatabaseProvider + fixture | 4 infra files |
| 3 | ConnectionParityTests | 1 test file (3 tests) |
| 4 | CommandExecutionParityTests | 1 test file (4 tests) |
| 5 | ReaderParityTests | 1 test file (6 tests) |
| 6 | TransactionParityTests | 1 test file (3 tests) |
| 7 | DataAdapterParityTests | 1 test file (3 tests) |
| 8 | SerializationParityTests | 1 test file (4 tests) |
| 9 | EventParityTests | 1 test file (5 tests) |
| 10 | EdgeCaseParityTests | 1 test file (4 tests) |
| 11 | Benchmarks scaffold | csproj, slnx |
| 12 | BenchmarkBase + Program.cs | 2 files |
| 13 | ConnectionBenchmarks | 1 file |
| 14 | CommandExecutionBenchmarks | 1 file |
| 15 | ReaderBenchmarks | 1 file |
| 16 | TransactionBenchmarks | 1 file |
| 17 | DataAdapterBenchmarks | 1 file |
| 18 | AsyncParityExporter | 1 file + Program.cs update |
| 19 | Run all validation tests | verify |
| 20 | Run benchmarks dry run | verify |

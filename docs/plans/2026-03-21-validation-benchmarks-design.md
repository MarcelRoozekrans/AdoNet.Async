# Validation & Benchmarks Design

## Goal

Validate that `System.Data.Async` produces identical behavior to raw ADO.NET (`System.Data`), and measure the performance overhead of the async wrapper layer.

## Project Structure

Two new projects:

```
tests/
  System.Data.Async.Validation.Tests/    (xUnit — behavioral + data + event parity)
  System.Data.Async.Benchmarks/          (BenchmarkDotNet + custom summary exporter)
```

### Validation.Tests Dependencies

- xUnit, FluentAssertions
- Microsoft.Data.Sqlite
- System.Data.Async.Adapters (transitive: Core + DataSet)

### Benchmarks Dependencies

- BenchmarkDotNet
- Microsoft.Data.Sqlite
- System.Data.Async.Adapters

## Approach: Inline Comparison (Approach C)

Each validation test runs the same operation via raw ADO.NET and via the async wrapper within the same test method, then asserts equivalence. This makes each test a self-contained proof of parity — failures show exactly where divergence happens.

## Provider Configurability

A shared `ITestDatabaseProvider` interface abstracts the database provider:

```csharp
public interface ITestDatabaseProvider
{
    DbConnection CreateRawConnection();
    IAsyncDbConnection CreateAsyncConnection();
    string ProviderName { get; }
}
```

SQLite is the default implementation using shared in-memory databases (`cache=shared`). Adding another provider (SQL Server, PostgreSQL) means implementing this interface — zero changes to test logic.

## Shared Test Schema

```sql
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Email TEXT,
    Age INTEGER,
    Balance DECIMAL(10,2),
    CreatedAt TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    Product TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    OrderDate TEXT NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
```

Seed data: 50 users, 200 orders.

Fixture uses `IAsyncLifetime` and `[Collection]` for shared instance across test classes.

## Validation Test Classes (8 files)

### ConnectionParityTests
- Open/Close state transitions
- Repeated open/close cycles
- ConnectionString and Database property parity

### CommandExecutionParityTests
- ExecuteNonQuery (INSERT, UPDATE, DELETE) — affected row count
- ExecuteScalar — single value return
- ExecuteReader — multi-row, multi-column
- Parameterized queries — same results with parameters

### ReaderParityTests
- Read() iteration — same row count
- Field access by index and by name — same values
- NextResult() for multi-resultset queries
- GetSchemaTable() — same schema metadata
- Null/DBNull handling — same IsDBNull results
- `await foreach` vs manual Read() loop equivalence

### TransactionParityTests
- Commit — data persisted
- Rollback — data reverted
- Isolation levels — same behavior
- Error during transaction — auto-rollback behavior

### DataAdapterParityTests
- Fill into DataTable/AsyncDataTable — same row count and data
- Update from DataTable/AsyncDataTable — same affected rows
- Round-trip: Fill → modify → Update → Fill — data integrity

### SerializationParityTests
- XML WriteXml/ReadXml round-trip — same data after deserialization
- XML WriteXmlSchema/ReadXmlSchema round-trip — same schema
- JSON serialization round-trip — same data after deserialization

### EventParityTests
All DataTable/DataSet events fire in the same order with equivalent arguments:
- AsyncDataTable: RowChanged, RowChanging, RowDeleted, RowDeleting, ColumnChanged, ColumnChanging, TableCleared, TableClearing, TableNewRow
- AsyncDataSet: MergeFailed

### EdgeCaseParityTests
- Empty result sets — same empty reader behavior
- Large result sets (1000+ rows) — same data
- CancellationToken — same cancellation behavior
- Disposed object access — same exception types

## Benchmark Classes (5 files)

All classes use `[MemoryDiagnoser]` and `[RankColumn]`. Each has paired methods: `[Benchmark(Baseline = true)]` for raw ADO.NET, `[Benchmark]` for async wrapper.

### ConnectionBenchmarks
- Open/Close throughput and allocation

### CommandExecutionBenchmarks
- ExecuteNonQuery, ExecuteScalar, ExecuteReader

### ReaderBenchmarks
- Row iteration (manual loop + await foreach)
- Field access patterns

### TransactionBenchmarks
- Begin/Commit cycles
- Begin/Rollback cycles

### DataAdapterBenchmarks
- Fill on varying sizes (10, 100, 1000 rows)
- Update on varying sizes (10, 100, 1000 rows)

## Custom Summary Exporter

A `BenchmarkDotNet.Exporters.IExporter` implementation producing a markdown table:

| Operation | Raw Mean | Async Mean | Delta % | Raw Alloc | Async Alloc | Alloc Delta | Status |
|-----------|----------|------------|---------|-----------|-------------|-------------|--------|

- Delta > 20% = warning flag
- Outputs to `BenchmarkDotNet.Artifacts/` alongside standard reports

## Runner

`Program.cs` with `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` for CLI control over which benchmarks to run.

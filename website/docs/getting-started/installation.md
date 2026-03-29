---
sidebar_position: 1
title: Installation
---

# Installation

## Prerequisites

AdoNet.Async targets **.NET 10** (`net10.0`). Make sure you have the .NET 10 SDK installed.

## Packages

Install packages via the `dotnet` CLI:

```bash
# Core interfaces and abstract base classes (zero dependencies)
dotnet add package AdoNet.Async

# Adapter wrappers for existing ADO.NET providers + DI extensions
dotnet add package AdoNet.Async.Adapters

# Async DataTable, DataSet, DataAdapter, and async events
dotnet add package AdoNet.Async.DataSet

# Typed DataSet source generator (generates typed async classes from .xsd)
dotnet add package AdoNet.Async.DataSet.Generator

# Newtonsoft.Json converters (wire-compatible with Json.Net.DataSetConverters)
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson

# System.Text.Json converters (same wire format as above)
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

## Which packages do you need?

Use this decision guide to pick the right combination:

### Wrapping an existing DbConnection

You want to wrap a `SqlConnection`, `NpgsqlConnection`, or any other `DbConnection` and use async interfaces.

```bash
dotnet add package AdoNet.Async
dotnet add package AdoNet.Async.Adapters
```

**AdoNet.Async** gives you the interfaces (`IAsyncDbConnection`, `IAsyncDbCommand`, etc.). **AdoNet.Async.Adapters** gives you the `.AsAsync()` extension method and the `AdapterDbConnection` wrapper that makes any `DbConnection` implement those interfaces.

### Working with DataTable and DataSet

You need `AsyncDataTable`, `AsyncDataRow`, async events, or `FillAsync` via `AdapterDbDataAdapter`.

```bash
dotnet add package AdoNet.Async
dotnet add package AdoNet.Async.Adapters
dotnet add package AdoNet.Async.DataSet
```

**AdoNet.Async.DataSet** includes `AsyncDataTable`, `AsyncDataSet`, `AsyncDataRow`, `AsyncDataRowCollection`, the abstract `AsyncDataAdapter`, and all 9 async events. The `AdapterDbDataAdapter` (in the Adapters package) provides the concrete `FillAsync` and `UpdateAsync` implementations.

### Typed DataSets from .xsd schemas

You have `.xsd` schema files and want the source generator to produce strongly typed async DataTable/DataRow classes.

```bash
dotnet add package AdoNet.Async.DataSet
dotnet add package AdoNet.Async.DataSet.Generator
```

Then add your `.xsd` files as `AdditionalFiles` in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\OrdersDS.xsd" />
</ItemGroup>
```

### JSON serialization

You need to serialize or deserialize `AsyncDataTable`/`AsyncDataSet` to JSON.

Pick **one** (or both, if you need interop -- they produce identical wire formats):

```bash
# If you use Newtonsoft.Json
dotnet add package AdoNet.Async.Serialization.NewtonsoftJson

# If you use System.Text.Json
dotnet add package AdoNet.Async.Serialization.SystemTextJson
```

### Using Dependency Injection

The Adapters package includes a `AddAsyncData()` extension method for `IServiceCollection`:

```bash
dotnet add package AdoNet.Async.Adapters
```

```csharp
services.AddAsyncData(SqlClientFactory.Instance);
```

This registers `IAsyncDbProviderFactory` as a singleton, which you can inject anywhere to create connections and commands.

## Package dependency graph

```
AdoNet.Async                          (no dependencies)
  |
  +-- AdoNet.Async.Adapters          (+ Microsoft.Extensions.DependencyInjection.Abstractions)
  +-- AdoNet.Async.DataSet            (+ ZeroAlloc.AsyncEvents)
        |
        +-- AdoNet.Async.DataSet.Generator     (compile-time only)
        +-- AdoNet.Async.Serialization.NewtonsoftJson  (+ Newtonsoft.Json)
        +-- AdoNet.Async.Serialization.SystemTextJson
```

:::tip
The core **AdoNet.Async** package has zero external dependencies. If you only need the interfaces and abstract base classes (for example, to define a repository abstraction in a shared library), it is the only package you need.
:::

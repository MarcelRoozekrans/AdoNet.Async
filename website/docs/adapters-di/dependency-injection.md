---
sidebar_position: 2
title: Dependency Injection
---

# Dependency Injection

The `AdoNet.Async.Adapters` package includes a DI extension method that registers an `IAsyncDbProviderFactory` in your `IServiceCollection`. This gives you a clean, testable abstraction for database access.

## Registration

```csharp
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Register from any DbProviderFactory
services.AddAsyncData(SqlClientFactory.Instance);
```

The `AddAsyncData` method wraps the `DbProviderFactory` in an `AdapterDbProviderFactory` and registers it as a singleton `IAsyncDbProviderFactory`.

You can also register a custom `IAsyncDbProviderFactory` directly:

```csharp
services.AddAsyncData(myCustomFactory);
```

## Injecting IAsyncDbProviderFactory

Once registered, inject `IAsyncDbProviderFactory` into your services:

```csharp
public interface IAsyncDbProviderFactory
{
    IAsyncDbConnection CreateConnection();
    IAsyncDbCommand CreateCommand();
    IDbDataParameter CreateParameter();
}
```

The factory creates adapter-wrapped instances. Connections created by the factory are `AdapterDbConnection` instances, commands are `AdapterDbCommand` instances, and so on.

## Repository Pattern Example

Here is a complete repository pattern implementation using constructor injection:

```csharp
using System.Data.Async;
using System.Data.Async.DataSet;
using System.Data.Async.Adapters;

public class UserRepository
{
    private readonly IAsyncDbProviderFactory _factory;
    private readonly string _connectionString;

    public UserRepository(IAsyncDbProviderFactory factory, IConfiguration config)
    {
        _factory = factory;
        _connectionString = config.GetConnectionString("Default")!;
    }

    public async Task<AsyncDataTable> GetAllUsersAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";

        var table = new AsyncDataTable("Users");
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(table, ct);

        return table;
    }

    public async Task<string?> GetUserNameAsync(int id, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Users WHERE Id = @id";

        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = id;
        cmd.Parameters.Add(param);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task CreateUserAsync(string name, string email, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Users (Name, Email) VALUES (@name, @email)";

        var nameParam = cmd.CreateParameter();
        nameParam.ParameterName = "@name";
        nameParam.Value = name;
        cmd.Parameters.Add(nameParam);

        var emailParam = cmd.CreateParameter();
        emailParam.ParameterName = "@email";
        emailParam.Value = email;
        cmd.Parameters.Add(emailParam);

        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

Register the repository:

```csharp
builder.Services.AddAsyncData(SqlClientFactory.Instance);
builder.Services.AddScoped<UserRepository>();
```

## Using with Different Providers

The same repository code works with any ADO.NET provider -- just change the factory registration:

```csharp
// SQL Server
using Microsoft.Data.SqlClient;
services.AddAsyncData(SqlClientFactory.Instance);

// PostgreSQL
using Npgsql;
services.AddAsyncData(NpgsqlFactory.Instance);

// MySQL
using MySqlConnector;
services.AddAsyncData(MySqlConnectorFactory.Instance);

// SQLite
using Microsoft.Data.Sqlite;
services.AddAsyncData(SqliteFactory.Instance);
```

Your repository code needs no changes -- it only depends on `IAsyncDbProviderFactory`.

:::tip
This pattern makes it straightforward to swap database providers in tests. Register an in-memory SQLite factory for unit tests and the real provider for production.
:::

## Unit Testing

Because `IAsyncDbProviderFactory` is an interface, it is straightforward to mock in unit tests:

```csharp
// Using a real SQLite in-memory database for integration tests
var services = new ServiceCollection();
services.AddAsyncData(SqliteFactory.Instance);

var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IAsyncDbProviderFactory>();

await using var conn = factory.CreateConnection();
conn.ConnectionString = "Data Source=:memory:";
await conn.OpenAsync();

// Set up test schema and run your repository methods...
```

## Combining with Typed DataSets

For typed DataSet usage, the DI pattern works naturally:

```csharp
public class OrderService
{
    private readonly IAsyncDbProviderFactory _factory;
    private readonly string _connectionString;

    public OrderService(IAsyncDbProviderFactory factory, IConfiguration config)
    {
        _factory = factory;
        _connectionString = config.GetConnectionString("Default")!;
    }

    public async Task<AsyncOrdersDS> GetOrdersAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Orders";

        var ds = new AsyncOrdersDS();
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(ds, ct);

        return ds;
    }
}
```

## Next Steps

- [Wrapping Providers](./wrapping-providers.md) -- details on the adapter classes
- [Migrate Existing Code](../cookbook/migrate-existing-code.md) -- step-by-step migration guide
- [JSON API with Typed DataSets](../cookbook/json-api-typed-datasets.md) -- full ASP.NET Core example

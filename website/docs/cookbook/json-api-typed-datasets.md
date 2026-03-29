---
sidebar_position: 4
title: JSON API Typed DataSets
---

# JSON API with Typed DataSets

This cookbook shows how to build an ASP.NET Core API that loads data into typed DataSets, returns them as JSON, accepts them in POST requests, and handles errors.

## Configure the Serializer

### System.Text.Json (default in ASP.NET Core)

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.Adapters;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Register the async data provider
builder.Services.AddAsyncData(SqlClientFactory.Instance);

// Configure JSON converters
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new AsyncDataTableJsonConverter());
    options.SerializerOptions.Converters.Add(new AsyncDataSetJsonConverter());
});

// For controllers:
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new AsyncDataTableJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AsyncDataSetJsonConverter());
    });
```

### Newtonsoft.Json

If your project uses Newtonsoft.Json (via `Microsoft.AspNetCore.Mvc.NewtonsoftJson`):

```csharp
using System.Data.Async.Converters;

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.Converters.Add(new AsyncDataTableConverter());
        options.SerializerSettings.Converters.Add(new AsyncDataSetConverter());
    });
```

## Minimal API Example

### GET -- Load and Return Data

```csharp
var app = builder.Build();

app.MapGet("/api/orders", async (
    IAsyncDbProviderFactory factory,
    IConfiguration config,
    CancellationToken ct) =>
{
    await using var conn = factory.CreateConnection();
    conn.ConnectionString = config.GetConnectionString("Default")!;
    await conn.OpenAsync(ct);

    var ds = new AsyncOrdersDS();

    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT CustomerId, Name, Email FROM Customers";
    var adapter = new AdapterDbDataAdapter(cmd);
    await adapter.FillAsync(ds.Customer, ct);

    cmd.CommandText = "SELECT OrderId, CustomerId, OrderDate, Total FROM Orders";
    await adapter.FillAsync(ds.Order, ct);

    // Return as AsyncDataSet -- the JSON converter handles serialization
    return Results.Ok<AsyncDataSet>(ds);
});
```

### GET -- Return a Single Table

```csharp
app.MapGet("/api/products", async (
    IAsyncDbProviderFactory factory,
    IConfiguration config,
    CancellationToken ct) =>
{
    await using var conn = factory.CreateConnection();
    conn.ConnectionString = config.GetConnectionString("Default")!;
    await conn.OpenAsync(ct);

    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Price FROM Products";

    var table = new AsyncDataTable("Products");
    var adapter = new AdapterDbDataAdapter(cmd);
    await adapter.FillAsync(table, ct);

    return Results.Ok(table);
});
```

### POST -- Accept and Save Data

```csharp
app.MapPost("/api/products", async (
    AsyncDataTable table,
    IAsyncDbProviderFactory factory,
    IConfiguration config,
    CancellationToken ct) =>
{
    // Validate incoming data
    if (table.Rows.Count == 0)
        return Results.BadRequest("No rows provided");

    await using var conn = factory.CreateConnection();
    conn.ConnectionString = config.GetConnectionString("Default")!;
    await conn.OpenAsync(ct);
    await using var tx = await conn.BeginTransactionAsync(ct);

    foreach (AsyncDataRow row in table.Rows)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Products (Name, Price) VALUES (@name, @price)";

        var nameParam = cmd.CreateParameter();
        nameParam.ParameterName = "@name";
        nameParam.Value = row["Name"];
        cmd.Parameters.Add(nameParam);

        var priceParam = cmd.CreateParameter();
        priceParam.ParameterName = "@price";
        priceParam.Value = row["Price"];
        cmd.Parameters.Add(priceParam);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    await tx.CommitAsync(ct);
    return Results.Created($"/api/products", null);
});
```

## Controller Example

```csharp
using System.Data.Async;
using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IAsyncDbProviderFactory _factory;
    private readonly string _connectionString;

    public OrdersController(IAsyncDbProviderFactory factory, IConfiguration config)
    {
        _factory = factory;
        _connectionString = config.GetConnectionString("Default")!;
    }

    [HttpGet]
    public async Task<ActionResult<AsyncDataSet>> GetOrders(CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var ds = new AsyncOrdersDS();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Customers";
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(ds.Customer, ct);

        cmd.CommandText = "SELECT * FROM Orders";
        await adapter.FillAsync(ds.Order, ct);

        return Ok<AsyncDataSet>(ds);
    }

    [HttpGet("{customerId}/orders")]
    public async Task<ActionResult<AsyncDataTable>> GetCustomerOrders(
        int customerId, CancellationToken ct)
    {
        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT OrderId, CustomerId, OrderDate, Total FROM Orders WHERE CustomerId = @id";

        var param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = customerId;
        cmd.Parameters.Add(param);

        var table = new AsyncDataTable("Orders");
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(table, ct);

        return Ok(table);
    }

    [HttpPost]
    public async Task<IActionResult> SaveOrders(
        [FromBody] AsyncDataTable table, CancellationToken ct)
    {
        if (table.Rows.Count == 0)
            return BadRequest("No rows provided");

        await using var conn = _factory.CreateConnection();
        conn.ConnectionString = _connectionString;
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText =
                "INSERT INTO Orders (CustomerId, OrderDate, Total) VALUES (@cid, @date, @total)";

            var cidParam = insertCmd.CreateParameter();
            cidParam.ParameterName = "@cid";
            cidParam.SourceColumn = "CustomerId";
            insertCmd.Parameters.Add(cidParam);

            var dateParam = insertCmd.CreateParameter();
            dateParam.ParameterName = "@date";
            dateParam.SourceColumn = "OrderDate";
            insertCmd.Parameters.Add(dateParam);

            var totalParam = insertCmd.CreateParameter();
            totalParam.ParameterName = "@total";
            totalParam.SourceColumn = "Total";
            insertCmd.Parameters.Add(totalParam);

            var adapter = new AdapterDbDataAdapter();
            adapter.InsertCommand = insertCmd;
            int affected = await adapter.UpdateAsync(table, ct);

            await tx.CommitAsync(ct);
            return Ok(new { RowsAffected = affected });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
```

## Error Handling

Wrap database operations in try/catch and return appropriate HTTP status codes:

```csharp
app.MapGet("/api/data", async (
    IAsyncDbProviderFactory factory,
    IConfiguration config,
    CancellationToken ct) =>
{
    try
    {
        await using var conn = factory.CreateConnection();
        conn.ConnectionString = config.GetConnectionString("Default")!;
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Products";

        var table = new AsyncDataTable("Products");
        var adapter = new AdapterDbDataAdapter(cmd);
        await adapter.FillAsync(table, ct);

        return Results.Ok(table);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499); // Client closed request
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});
```

:::tip
Use global exception handling middleware in production rather than try/catch in every endpoint. The example above shows the pattern for clarity.
:::

## Client-Side Consumption

The JSON produced by these APIs can be consumed by any HTTP client. Here is an example using `HttpClient`:

```csharp
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using System.Net.Http.Json;
using System.Text.Json;

var options = new JsonSerializerOptions();
options.Converters.Add(new AsyncDataTableJsonConverter());
options.Converters.Add(new AsyncDataSetJsonConverter());

var http = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };

// GET
var table = await http.GetFromJsonAsync<AsyncDataTable>("/api/products", options);
foreach (AsyncDataRow row in table!.Rows)
{
    Console.WriteLine($"{row["Name"]}: {row["Price"]}");
}

// POST
var newProducts = new AsyncDataTable("Products");
newProducts.Columns.Add("Name", typeof(string));
newProducts.Columns.Add("Price", typeof(decimal));
await newProducts.Rows.AddAsync(["New Widget", 14.99m]);

await http.PostAsJsonAsync("/api/products", newProducts, options);
```

## Next Steps

- [System.Text.Json Serialization](../serialization/system-text-json.md) -- converter details and ASP.NET Core integration
- [Newtonsoft.Json Serialization](../serialization/newtonsoft-json.md) -- wire format reference
- [Fill Typed DataSet](./fill-typed-dataset.md) -- end-to-end typed DataSet example
- [Dependency Injection](../adapters-di/dependency-injection.md) -- register `IAsyncDbProviderFactory`

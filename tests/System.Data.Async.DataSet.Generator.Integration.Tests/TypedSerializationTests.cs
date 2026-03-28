using System.Data.Async.Converters;
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using FluentAssertions;
using Newtonsoft.Json;
using STJ = System.Text.Json;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class TypedSerializationTests
{
    private static JsonSerializerSettings NewtonsoftSettings()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());
        settings.Converters.Add(new AsyncDataSetConverter());
        return settings;
    }

    private static STJ.JsonSerializerOptions StjOptions()
    {
        var options = new STJ.JsonSerializerOptions();
        options.Converters.Add(new AsyncDataTableJsonConverter());
        options.Converters.Add(new AsyncDataSetJsonConverter());
        return options;
    }

    // --- Newtonsoft ---

    [Fact]
    public async Task Newtonsoft_Serialize_Typed_DataSet_Roundtrip()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");
        var customer = ds.Customer.FindByCustomerId(1)!;
        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Test order");

        // Serialize as base AsyncDataSet
        var json = JsonConvert.SerializeObject((AsyncDataSet)ds, NewtonsoftSettings());
        json.Should().NotBeNullOrEmpty();

        // Deserialize as base
        var baseDs = JsonConvert.DeserializeObject<AsyncDataSet>(json, NewtonsoftSettings());
        baseDs.Should().NotBeNull();

        // Check data survived
        System.Data.DataSet inner = baseDs!;
        inner.Tables["Customer"]!.Rows.Count.Should().Be(2);
        inner.Tables["Order"]!.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task Newtonsoft_Typed_Table_Roundtrip()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

        // Serialize the typed table as base
        var json = JsonConvert.SerializeObject((AsyncDataTable)ds.Customer, NewtonsoftSettings());

        // Deserialize as base
        var baseTable = JsonConvert.DeserializeObject<AsyncDataTable>(json, NewtonsoftSettings());
        baseTable.Should().NotBeNull();
        baseTable!.Rows.Count.Should().Be(2);
        ((string)baseTable.Rows[0]["Name"]).Should().Be("Alice");
    }

    [Fact]
    public async Task Newtonsoft_Typed_DataSet_Data_Preserved()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Order.AddOrderRowAsync(customer, new DateTime(2026, 1, 15), 49.99m, "Test notes");

        var json = JsonConvert.SerializeObject((AsyncDataSet)ds, NewtonsoftSettings());
        var restored = JsonConvert.DeserializeObject<AsyncDataSet>(json, NewtonsoftSettings())!;

        System.Data.DataSet inner = restored;
        var customerRow = inner.Tables["Customer"]!.Rows[0];
        ((int)customerRow["CustomerId"]).Should().Be(1);
        ((string)customerRow["Name"]).Should().Be("Alice");
        ((string)customerRow["Email"]).Should().Be("alice@example.com");

        var orderRow = inner.Tables["Order"]!.Rows[0];
        ((decimal)orderRow["Total"]).Should().Be(49.99m);
    }

    // --- System.Text.Json ---

    [Fact]
    public async Task STJ_Serialize_Typed_DataSet_Roundtrip()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        var customer = ds.Customer.FindByCustomerId(1)!;
        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Test order");

        var options = StjOptions();

        var json = STJ.JsonSerializer.Serialize((AsyncDataSet)ds, options);
        var restored = STJ.JsonSerializer.Deserialize<AsyncDataSet>(json, options)!;

        System.Data.DataSet inner = restored;
        inner.Tables["Customer"]!.Rows.Count.Should().Be(1);
        inner.Tables["Order"]!.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task STJ_Typed_Table_Roundtrip()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

        var options = StjOptions();

        var json = STJ.JsonSerializer.Serialize((AsyncDataTable)ds.Customer, options);
        var baseTable = STJ.JsonSerializer.Deserialize<AsyncDataTable>(json, options);
        baseTable.Should().NotBeNull();
        baseTable!.Rows.Count.Should().Be(2);
        ((string)baseTable.Rows[0]["Name"]).Should().Be("Alice");
    }

    [Fact]
    public async Task STJ_Typed_DataSet_Data_Preserved()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Order.AddOrderRowAsync(customer, new DateTime(2026, 1, 15), 49.99m, "Test notes");

        var options = StjOptions();

        var json = STJ.JsonSerializer.Serialize((AsyncDataSet)ds, options);
        var restored = STJ.JsonSerializer.Deserialize<AsyncDataSet>(json, options)!;

        System.Data.DataSet inner = restored;
        var customerRow = inner.Tables["Customer"]!.Rows[0];
        ((int)customerRow["CustomerId"]).Should().Be(1);
        ((string)customerRow["Name"]).Should().Be("Alice");
        ((string)customerRow["Email"]).Should().Be("alice@example.com");

        var orderRow = inner.Tables["Order"]!.Rows[0];
        ((decimal)orderRow["Total"]).Should().Be(49.99m);
    }
}

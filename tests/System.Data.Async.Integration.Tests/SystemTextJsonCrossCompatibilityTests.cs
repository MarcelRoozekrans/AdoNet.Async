using System.Data.Async.Converters;
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using NJsonConvert = Newtonsoft.Json.JsonConvert;

namespace System.Data.Async.Integration.Tests;

public class SystemTextJsonCrossCompatibilityTests
{
    private static Newtonsoft.Json.JsonSerializerSettings NewtonsoftAsyncSettings() => new Newtonsoft.Json.JsonSerializerSettings
    {
        Converters = { new AsyncDataTableConverter(), new AsyncDataSetConverter() }
    };

    private static JsonSerializerOptions StjOptions() => new JsonSerializerOptions
    {
        Converters = { new AsyncDataTableJsonConverter(), new AsyncDataSetJsonConverter() }
    };

    [Fact]
    public async Task STJ_And_Newtonsoft_Produce_Identical_Json_For_Simple_Table()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);
        await table.AcceptChangesAsync();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    [Fact]
    public async Task STJ_And_Newtonsoft_Produce_Identical_Json_For_All_Row_States()
    {
        var table = new AsyncDataTable("States");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Val", typeof(string));
        await table.Rows.AddAsync([1, "Unchanged"]);
        await table.Rows.AddAsync([2, "WillModify"]);
        await table.Rows.AddAsync([3, "WillDelete"]);
        await table.AcceptChangesAsync();
        await table.Rows.AddAsync([4, "Added"]);
        await table.Rows[1].SetValueAsync("Val", "Modified");
        await table.Rows[2].DeleteAsync();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    [Fact]
    public async Task STJ_And_Newtonsoft_Produce_Identical_Json_For_Decimal_And_Bytes()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Data", typeof(byte[]));
        await table.Rows.AddAsync([12345.6789012345678901234567m, new byte[] { 10, 20, 30 }]);
        await table.AcceptChangesAsync();

        var newtonsoftJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());

        stjJson.Should().Be(newtonsoftJson);
    }

    [Fact]
    public async Task AsyncDataTable_Round_Trips_Via_STJ()
    {
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Price", typeof(decimal));
        await table.Rows.AddAsync([1, 49.99m]);
        await table.Rows.AddAsync([2, 99.99m]);
        await table.AcceptChangesAsync();
        await table.Rows[1].SetValueAsync("Price", 89.99m);

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.TableName.Should().Be("Products");
        result.Rows.Count.Should().Be(2);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
        ((decimal)result.Rows[1]["Price"]).Should().Be(89.99m);
        ((decimal)result.Rows[1]["Price", DataRowVersion.Original]).Should().Be(99.99m);
    }

    [Fact]
    public async Task All_Row_States_Round_Trip_Via_STJ()
    {
        var table = new AsyncDataTable("States");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Val", typeof(string));
        await table.Rows.AddAsync([1, "Unchanged"]);
        await table.Rows.AddAsync([2, "WillModify"]);
        await table.Rows.AddAsync([3, "WillDelete"]);
        await table.AcceptChangesAsync();
        await table.Rows.AddAsync([4, "Added"]);
        await table.Rows[1].SetValueAsync("Val", "Modified");
        await table.Rows[2].DeleteAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Select("Id = 1")[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Select("Id = 2")[0].RowState.Should().Be(DataRowState.Modified);
        result.Select("Id = 4")[0].RowState.Should().Be(DataRowState.Added);
        var deleted = result.InnerDataTable.Select("Id = 3", null, DataViewRowState.Deleted);
        deleted.Should().HaveCount(1);
        deleted[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task Added_Row_Preserves_State_Via_STJ()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync([1]);

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public async Task All_Primitive_Types_Round_Trip_Via_STJ()
    {
        var dt = new AsyncDataTable("Types");
        dt.Columns.Add("Bool", typeof(bool));
        dt.Columns.Add("Int", typeof(int));
        dt.Columns.Add("Long", typeof(long));
        dt.Columns.Add("Double", typeof(double));
        dt.Columns.Add("Decimal", typeof(decimal));
        dt.Columns.Add("String", typeof(string));
        dt.Columns.Add("Guid", typeof(Guid));
        dt.Columns.Add("Bytes", typeof(byte[]));

        var guid = Guid.NewGuid();
        var bytes = new byte[] { 1, 2, 3 };
        await dt.Rows.AddAsync([true, 42, 9999999999L, 2.718, 12345.6789m, "hello", guid, bytes]);
        await dt.AcceptChangesAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(dt, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;
        var row = result.Rows[0];

        row["Bool"].Should().Be(true);
        row["Int"].Should().Be(42);
        row["Long"].Should().Be(9999999999L);
        row["Decimal"].Should().Be(12345.6789m);
        row["String"].Should().Be("hello");
        row["Guid"].Should().Be(guid);
        ((byte[])row["Bytes"]).Should().Equal(bytes);
    }

    [Fact]
    public async Task Null_Values_Round_Trip_Via_STJ()
    {
        var table = new AsyncDataTable("Nulls");
        table.Columns.Add("Id", typeof(int));
        var nameCol = table.Columns.Add("Name", typeof(string));
        nameCol.AllowDBNull = true;
        await table.Rows.AddAsync([1, DBNull.Value]);
        await table.AcceptChangesAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(json, StjOptions())!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task STJ_Json_Deserializes_With_Newtonsoft_And_Vice_Versa()
    {
        var table = new AsyncDataTable("CrossTest");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Value", typeof(string));
        await table.Rows.AddAsync([1, "Test"]);
        await table.AcceptChangesAsync();

        // Serialize with STJ, deserialize with Newtonsoft
        var stjJson = System.Text.Json.JsonSerializer.Serialize(table, StjOptions());
        var fromStj = NJsonConvert.DeserializeObject<AsyncDataTable>(stjJson, NewtonsoftAsyncSettings())!;
        fromStj.Rows[0]["Value"].Should().Be("Test");

        // Serialize with Newtonsoft, deserialize with STJ
        var nJson = NJsonConvert.SerializeObject(table, NewtonsoftAsyncSettings());
        var fromNewtonsoft = System.Text.Json.JsonSerializer.Deserialize<AsyncDataTable>(nJson, StjOptions())!;
        fromNewtonsoft.Rows[0]["Value"].Should().Be("Test");
    }

    [Fact]
    public void AsyncDataSet_Round_Trips_Via_STJ()
    {
        var ds = new System.Data.DataSet("MyDS");
        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("Id", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.PrimaryKey = [customers.Columns["Id"]!];
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("Id", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.PrimaryKey = [orders.Columns["Id"]!];
        ds.Relations.Add("CustOrders", customers.Columns["Id"]!, orders.Columns["CustomerId"]!);
        customers.Rows.Add(1, "Alice");
        orders.Rows.Add(100, 1);
        ds.AcceptChanges();

        var asyncDs = new AsyncDataSet(ds);
        var json = System.Text.Json.JsonSerializer.Serialize(asyncDs, StjOptions());
        var result = System.Text.Json.JsonSerializer.Deserialize<AsyncDataSet>(json, StjOptions())!;

        result.DataSetName.Should().Be("MyDS");
        result.Tables["Customers"]!.Rows[0]["Name"].Should().Be("Alice");
        result.Tables["Orders"]!.Rows[0]["CustomerId"].Should().Be(1);
        result.Relations.Count.Should().Be(1);
        result.Relations["CustOrders"]!.ParentTable.TableName.Should().Be("Customers");
    }
}

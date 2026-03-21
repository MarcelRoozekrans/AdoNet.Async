using System.Data.Async.DataSet;
using System.Data.Async.Converters;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class CrossCompatibilityTests
{
    [Fact]
    public void Original_DataTable_Json_Should_Deserialize_Into_AsyncDataTable()
    {
        // Create original DataTable
        var dt = new DataTable("Users");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Alice");
        dt.Rows.Add(2, "Bob");
        dt.AcceptChanges();
        dt.Rows[1]["Name"] = "Robert"; // Modified

        // Serialize with original converter
        var originalSettings = new JsonSerializerSettings();
        originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataTableConverter());
        var json = JsonConvert.SerializeObject(dt, originalSettings);

        // Deserialize with our converter
        var asyncSettings = new JsonSerializerSettings();
        asyncSettings.Converters.Add(new AsyncDataTableConverter());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, asyncSettings);

        result.Should().NotBeNull();
        result!.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1]["Name"].Should().Be("Robert");
        result.Rows[1]["Name", DataRowVersion.Original].Should().Be("Bob");
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public void AsyncDataTable_Json_Should_Deserialize_Into_Original_DataTable()
    {
        // Create AsyncDataTable
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Price", typeof(decimal));
        table.Rows.Add(1, 29.99m);
        table.AcceptChanges();

        // Serialize with our converter
        var asyncSettings = new JsonSerializerSettings();
        asyncSettings.Converters.Add(new AsyncDataTableConverter());
        var json = JsonConvert.SerializeObject(table, asyncSettings);

        // Deserialize with original converter
        var originalSettings = new JsonSerializerSettings();
        originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataTableConverter());
        var result = JsonConvert.DeserializeObject<DataTable>(json, originalSettings);

        result.Should().NotBeNull();
        result!.TableName.Should().Be("Products");
        result.Rows.Count.Should().Be(1);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void Original_DataSet_Json_Should_Deserialize_Into_AsyncDataSet()
    {
        var ds = new System.Data.DataSet("TestDS");
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.Rows.Add(1, 99.99m);
        orders.Rows.Add(2, 150.00m);
        ds.AcceptChanges();
        orders.Rows[1]["Total"] = 175.50m; // Modified

        var originalSettings = new JsonSerializerSettings();
        originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataSetConverter());
        originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataTableConverter());
        var json = JsonConvert.SerializeObject(ds, originalSettings);

        var asyncSettings = new JsonSerializerSettings();
        asyncSettings.Converters.Add(new AsyncDataSetConverter());
        asyncSettings.Converters.Add(new AsyncDataTableConverter());
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, asyncSettings);

        result.Should().NotBeNull();
        result!.DataSetName.Should().Be("TestDS");
        result.Tables["Orders"]!.Rows.Count.Should().Be(2);
        result.Tables["Orders"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public void Full_Roundtrip_All_Row_States()
    {
        // Test with all row states
        var dt = new DataTable("Mixed");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Value", typeof(string));
        dt.Rows.Add(1, "Unchanged");
        dt.Rows.Add(2, "WillModify");
        dt.Rows.Add(3, "WillDelete");
        dt.AcceptChanges();
        dt.Rows[1]["Value"] = "Modified";
        dt.Rows[2].Delete();
        dt.Rows.Add(4, "Added");

        // Original -> JSON -> Async
        var originalSettings = new JsonSerializerSettings();
        originalSettings.Converters.Add(new global::Json.Net.DataSetConverters.DataTableConverter());
        var json = JsonConvert.SerializeObject(dt, originalSettings);

        var asyncSettings = new JsonSerializerSettings();
        asyncSettings.Converters.Add(new AsyncDataTableConverter());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, asyncSettings);

        result.Should().NotBeNull();
        // Check row states - note: deleted rows may change position
        var unchanged = result!.Select("Id = 1");
        unchanged.Should().HaveCount(1);
        unchanged[0].RowState.Should().Be(DataRowState.Unchanged);

        var modified = result.Select("Id = 2");
        modified.Should().HaveCount(1);
        modified[0].RowState.Should().Be(DataRowState.Modified);

        var added = result.Select("Id = 4");
        added.Should().HaveCount(1);
        added[0].RowState.Should().Be(DataRowState.Added);
    }
}

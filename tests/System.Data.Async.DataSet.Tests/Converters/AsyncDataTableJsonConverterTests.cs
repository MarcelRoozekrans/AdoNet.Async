using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests.Converters;

public class AsyncDataTableJsonConverterTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new AsyncDataTableJsonConverter());
        return options;
    }

    [Fact]
    public async Task Should_Roundtrip_Simple_Table()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task Should_Roundtrip_Multiple_Column_Types()
    {
        var table = new AsyncDataTable("Types");
        table.Columns.Add("IntCol", typeof(int));
        table.Columns.Add("StringCol", typeof(string));
        table.Columns.Add("BoolCol", typeof(bool));
        table.Columns.Add("DecimalCol", typeof(decimal));
        table.Columns.Add("DateCol", typeof(DateTime));
        await table.Rows.AddAsync([42, "hello", true, 3.14m, new DateTime(2024, 1, 15)]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.Rows[0]["IntCol"].Should().Be(42);
        result.Rows[0]["StringCol"].Should().Be("hello");
        result.Rows[0]["BoolCol"].Should().Be(true);
        result.Rows[0]["DecimalCol"].Should().Be(3.14m);
    }

    [Fact]
    public async Task Should_Roundtrip_DBNull_Values()
    {
        var table = new AsyncDataTable("Nulls");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Optional", typeof(string));
        await table.Rows.AddAsync([1, DBNull.Value]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.Rows[0]["Optional"].Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task Should_Roundtrip_Empty_Table()
    {
        var table = new AsyncDataTable("Empty");
        table.Columns.Add("Id", typeof(int));
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.TableName.Should().Be("Empty");
        result.Rows.Count.Should().Be(0);
        result.Columns.Count.Should().Be(1);
    }

    [Fact]
    public void Should_Serialize_Null_As_JsonNull()
    {
        var options = CreateOptions();
        var json = JsonSerializer.Serialize<AsyncDataTable?>(null, options);
        json.Should().Be("null");
    }

    [Fact]
    public void Should_Deserialize_Null_Json_As_Null()
    {
        var options = CreateOptions();
        var result = JsonSerializer.Deserialize<AsyncDataTable>("null", options);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_Preserve_TableName_On_Roundtrip()
    {
        var table = new AsyncDataTable("MySpecialTable");
        table.Columns.Add("X", typeof(int));
        await table.Rows.AddAsync([1]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.TableName.Should().Be("MySpecialTable");
    }

    [Fact]
    public async Task Should_Preserve_Column_Count()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("A", typeof(int));
        table.Columns.Add("B", typeof(string));
        table.Columns.Add("C", typeof(bool));
        await table.Rows.AddAsync([1, "x", false]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);
        var result = JsonSerializer.Deserialize<AsyncDataTable>(json, options)!;

        result.Columns.Count.Should().Be(3);
    }

    [Fact]
    public async Task Json_Output_Is_Valid_Json()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync([1]);
        await table.AcceptChangesAsync();

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(table, options);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }
}

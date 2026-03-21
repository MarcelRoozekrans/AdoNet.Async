using System.Data.Async.Converters;
using System.Data.Async.DataSet;

using FluentAssertions;

using Newtonsoft.Json;

using Xunit;

namespace System.Data.Async.DataSet.Tests.Converters;

public class AsyncDataSetConverterTests
{
    private static JsonSerializerSettings CreateSettings()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataSetConverter());
        settings.Converters.Add(new AsyncDataTableConverter());
        return settings;
    }

    [Fact]
    public void Should_Roundtrip_Simple_DataSet()
    {
        var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "Alice");
        table.AcceptChanges();
        ds.Tables.Add(table);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.DataSetName.Should().Be("TestDS");
        result.Tables.Count.Should().Be(1);
        result.Tables["Users"]!.Rows.Count.Should().Be(1);
        result.Tables["Users"]!.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public void Should_Roundtrip_Multiple_Tables()
    {
        var ds = new AsyncDataSet("MultiDS");

        var users = new DataTable("Users");
        users.Columns.Add("Id", typeof(int));
        users.Columns.Add("Name", typeof(string));
        users.Rows.Add(1, "Alice");
        users.AcceptChanges();
        ds.Tables.Add(users);

        var orders = new DataTable("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("UserId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.Rows.Add(100, 1, 99.95m);
        orders.AcceptChanges();
        ds.Tables.Add(orders);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.Tables.Count.Should().Be(2);
        result.Tables["Users"]!.Rows.Count.Should().Be(1);
        result.Tables["Orders"]!.Rows.Count.Should().Be(1);
        ((decimal)result.Tables["Orders"]!.Rows[0]["Total"]).Should().Be(99.95m);
    }

    [Fact]
    public void Should_Roundtrip_DataSet_With_Relations()
    {
        var ds = new AsyncDataSet("RelDS");
        ds.EnforceConstraints = false;

        var parents = new DataTable("Parents");
        var parentId = parents.Columns.Add("Id", typeof(int));
        parents.Columns.Add("Name", typeof(string));
        parents.PrimaryKey = [parentId];
        parents.Rows.Add(1, "Parent1");
        parents.AcceptChanges();
        ds.Tables.Add(parents);

        var children = new DataTable("Children");
        var childId = children.Columns.Add("Id", typeof(int));
        var childParentId = children.Columns.Add("ParentId", typeof(int));
        children.Columns.Add("Value", typeof(string));
        children.Rows.Add(10, 1, "Child1");
        children.AcceptChanges();
        ds.Tables.Add(children);

        ds.Relations.Add("ParentChildren", parentId, childParentId);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);

        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.Relations.Count.Should().Be(1);
        result.Relations["ParentChildren"].Should().NotBeNull();
        result.Relations["ParentChildren"]!.ParentTable.TableName.Should().Be("Parents");
        result.Relations["ParentChildren"]!.ChildTable.TableName.Should().Be("Children");
    }

    [Fact]
    public void Should_Preserve_DataSet_Properties()
    {
        var ds = new AsyncDataSet("PropDS");
        ds.CaseSensitive = true;
        ds.Namespace = "http://test.com";
        ds.Prefix = "ds";

        var table = new DataTable("T1");
        table.Columns.Add("Id", typeof(int));
        ds.Tables.Add(table);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.CaseSensitive.Should().BeTrue();
        result.Namespace.Should().Be("http://test.com");
        result.Prefix.Should().Be("ds");
    }

    [Fact]
    public void Should_Handle_Empty_DataSet()
    {
        var ds = new AsyncDataSet("EmptyDS");

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.DataSetName.Should().Be("EmptyDS");
        result.Tables.Count.Should().Be(0);
        result.Relations.Count.Should().Be(0);
    }

    [Fact]
    public void Should_Handle_Null_Value()
    {
        var settings = CreateSettings();
        var result = JsonConvert.DeserializeObject<AsyncDataSet>("null", settings);
        result.Should().BeNull();
    }

    [Fact]
    public void Should_Serialize_Null_Value()
    {
        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject((AsyncDataSet?)null, settings);
        json.Should().Be("null");
    }

    [Fact]
    public void Should_Preserve_EnforceConstraints()
    {
        var ds = new AsyncDataSet("TestDS");
        ds.EnforceConstraints = false;

        var table = new DataTable("T1");
        table.Columns.Add("Id", typeof(int));
        ds.Tables.Add(table);

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(ds, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, settings)!;

        result.EnforceConstraints.Should().BeFalse();
    }
}

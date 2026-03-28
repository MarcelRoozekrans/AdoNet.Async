using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataSetTests
{
    [Fact]
    public void Constructor_Sets_DataSetName()
    {
        using var ds = new AsyncDataSet("TestDS");

        ds.DataSetName.Should().Be("TestDS");
    }

    [Fact]
    public void Default_Constructor_Creates_Empty_DataSet()
    {
        using var ds = new AsyncDataSet();

        ds.Tables.Count.Should().Be(0);
    }

    [Fact]
    public void Tables_Add_Works()
    {
        using var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Products");
        table.Columns.Add("Id", typeof(int));

        ds.Tables.Add(table);

        ds.Tables.Count.Should().Be(1);
        ds.Tables[0].TableName.Should().Be("Products");
    }

    [Fact]
    public void Relations_Work()
    {
        using var ds = new AsyncDataSet("TestDS");

        var parent = new DataTable("Parent");
        parent.Columns.Add("Id", typeof(int));
        parent.PrimaryKey = [parent.Columns["Id"]!];

        var child = new DataTable("Child");
        child.Columns.Add("Id", typeof(int));
        child.Columns.Add("ParentId", typeof(int));

        ds.Tables.Add(parent);
        ds.Tables.Add(child);
        ds.Relations.Add("FK_Child_Parent", parent.Columns["Id"]!, child.Columns["ParentId"]!);

        ds.Relations.Count.Should().Be(1);
        ds.Relations[0].RelationName.Should().Be("FK_Child_Parent");
    }

    [Fact]
    public void HasChanges_And_GetChanges_Work()
    {
        using var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Items");
        table.Columns.Add("Id", typeof(int));
        ds.Tables.Add(table);
        ds.AcceptChanges();

        table.Rows.Add(1);

        ds.HasChanges().Should().BeTrue();
        var changes = ds.GetChanges();
        changes.Should().NotBeNull();
        changes!.Tables[0].Rows.Count.Should().Be(1);
    }

    [Fact]
    public void AcceptChanges_Clears_Changes()
    {
        using var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Items");
        table.Columns.Add("Id", typeof(int));
        ds.Tables.Add(table);
        table.Rows.Add(1);

        ds.AcceptChanges();

        ds.HasChanges().Should().BeFalse();
    }

    [Fact]
    public void Merge_Works()
    {
        using var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.PrimaryKey = [table.Columns["Id"]!];
        table.Rows.Add(1);
        ds.Tables.Add(table);
        ds.AcceptChanges();

        var other = new System.Data.DataSet();
        var otherTable = new DataTable("Items");
        otherTable.Columns.Add("Id", typeof(int));
        otherTable.PrimaryKey = [otherTable.Columns["Id"]!];
        otherTable.Rows.Add(2);
        other.Tables.Add(otherTable);

        ds.Merge(other);

        ds.Tables["Items"]!.Rows.Count.Should().Be(2);
    }

    [Fact]
    public void Explicit_Conversion_To_DataSet_Works()
    {
        using var asyncDs = new AsyncDataSet("TestDS");
        asyncDs.Tables.Add(new DataTable("T1"));

        var ds = (System.Data.DataSet)asyncDs;

        ds.DataSetName.Should().Be("TestDS");
        ds.Tables.Count.Should().Be(1);
    }

    [Fact]
    public void Tables_Returns_AsyncDataTableCollection()
    {
        using var ds = new AsyncDataSet("Test");
        ds.Tables.Should().BeOfType<AsyncDataTableCollection>();
    }

    [Fact]
    public void Tables_Indexer_Returns_Cached_Instance()
    {
        using var ds = new AsyncDataSet("Test");
        ((System.Data.DataSet)ds).Tables.Add(new DataTable("T"));

        var t1 = ds.Tables["T"];
        var t2 = ds.Tables["T"];
        t1.Should().BeSameAs(t2);
    }

    [Fact]
    public void Clone_Returns_AsyncDataSet()
    {
        using var ds = new AsyncDataSet("Test");
        ds.Clone().Should().BeOfType<AsyncDataSet>();
    }

    [Fact]
    public void GetChanges_Returns_AsyncDataSet()
    {
        using var ds = new AsyncDataSet("Test");
        ((System.Data.DataSet)ds).Tables.Add(new DataTable("T"));
        ((System.Data.DataSet)ds).Tables["T"]!.Columns.Add("Id", typeof(int));
        ((System.Data.DataSet)ds).Tables["T"]!.Rows.Add(1);
        ds.GetChanges().Should().BeOfType<AsyncDataSet>();
    }

    [Fact]
    public void Table_DataSet_Property_Returns_Parent_AsyncDataSet()
    {
        using var ds = new AsyncDataSet("Test");
        var table = new AsyncDataTable("T");
        ds.Tables.Add(table);

        var retrieved = ds.Tables["T"];
        retrieved.DataSet.Should().BeSameAs(ds);
    }

    [Fact]
    public void Table_DataSet_Property_Returns_Null_When_Not_In_DataSet()
    {
        var table = new AsyncDataTable("T");
        table.DataSet.Should().BeNull();
    }
}

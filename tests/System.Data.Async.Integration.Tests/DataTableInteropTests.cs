using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class DataTableInteropTests
{
    private static DataTable BuildRichTable()
    {
        var dt = new DataTable("Orders");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Amount", typeof(decimal));
        dt.PrimaryKey = [dt.Columns["Id"]!];

        dt.Rows.Add(1, "Unchanged", 10m);
        dt.Rows.Add(2, "WillModify", 20m);
        dt.Rows.Add(3, "WillDelete", 30m);
        dt.AcceptChanges();

        dt.Rows.Add(4, "Added", 40m);         // Added
        dt.Rows[1]["Name"] = "Modified";       // Modified
        dt.Rows[2].Delete();                   // Deleted
        return dt;
    }

    [Fact]
    public void Wrap_And_Unwrap_Preserves_All_Data()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.InnerDataTable.Should().BeSameAs(dt);
        wrapped.TableName.Should().Be("Orders");
        wrapped.Rows.Count.Should().Be(4); // Deleted rows still counted
    }

    [Fact]
    public void Wrap_Preserves_Column_Schema()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Columns.Count.Should().Be(3);
        wrapped.Columns["Id"]!.DataType.Should().Be<int>();
        wrapped.Columns["Name"]!.DataType.Should().Be<string>();
        wrapped.Columns["Amount"]!.DataType.Should().Be<decimal>();
    }

    [Fact]
    public void Wrap_Preserves_PrimaryKey()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);
        wrapped.PrimaryKey.Should().HaveCount(1);
        wrapped.PrimaryKey[0].ColumnName.Should().Be("Id");
    }

    [Fact]
    public void Wrap_Preserves_All_Row_States()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        wrapped.Rows[1].RowState.Should().Be(DataRowState.Modified);
        wrapped.Rows[2].RowState.Should().Be(DataRowState.Deleted);
        wrapped.Rows[3].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Wrap_Preserves_Modified_Original_Values()
    {
        var dt = BuildRichTable();
        var wrapped = new AsyncDataTable(dt);

        wrapped.Rows[1]["Name"].Should().Be("Modified");
        wrapped.Rows[1]["Name", DataRowVersion.Original].Should().Be("WillModify");
    }

    [Fact]
    public void Wrap_Preserves_Constraints()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Constraints.Add(new UniqueConstraint("UQ_Id", dt.Columns["Id"]!, isPrimaryKey: true));

        var wrapped = new AsyncDataTable(dt);
        wrapped.Constraints.Count.Should().Be(1);
        ((UniqueConstraint)wrapped.Constraints[0]).IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Wrap_Preserves_Extended_Properties()
    {
        var dt = new DataTable("T");
        dt.ExtendedProperties["Author"] = "Test";

        var wrapped = new AsyncDataTable(dt);
        wrapped.ExtendedProperties["Author"].Should().Be("Test");
    }
}

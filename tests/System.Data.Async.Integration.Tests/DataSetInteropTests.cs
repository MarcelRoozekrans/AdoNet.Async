using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class DataSetInteropTests
{
    private static System.Data.DataSet BuildRichDataSet()
    {
        var ds = new System.Data.DataSet("Shop");
        ds.CaseSensitive = false;

        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("CustomerId", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.PrimaryKey = [customers.Columns["CustomerId"]!];

        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.PrimaryKey = [orders.Columns["OrderId"]!];

        ds.Relations.Add("CustomerOrders",
            customers.Columns["CustomerId"]!,
            orders.Columns["CustomerId"]!);

        customers.Rows.Add(1, "Alice");
        customers.Rows.Add(2, "Bob");
        orders.Rows.Add(100, 1, 99.99m);
        orders.Rows.Add(101, 2, 149.50m);
        ds.AcceptChanges();

        customers.Rows[1]["Name"] = "Robert";     // Modified
        orders.Rows.Add(102, 1, 25.00m);          // Added
        return ds;
    }

    [Fact]
    public void Wrap_And_Unwrap_Preserves_DataSetName()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        ((System.Data.DataSet)wrapped).Should().BeSameAs(ds);
        wrapped.DataSetName.Should().Be("Shop");
    }

    [Fact]
    public void Wrap_Preserves_All_Tables()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Tables.Count.Should().Be(2);
        wrapped.Tables["Customers"].Should().NotBeNull();
        wrapped.Tables["Orders"].Should().NotBeNull();
    }

    [Fact]
    public void Wrap_Preserves_Relations()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Relations.Count.Should().Be(1);
        wrapped.Relations["CustomerOrders"]!.ParentTable.TableName.Should().Be("Customers");
        wrapped.Relations["CustomerOrders"]!.ChildTable.TableName.Should().Be("Orders");
    }

    [Fact]
    public void Wrap_Preserves_Row_States_Across_Tables()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);

        wrapped.Tables["Customers"]!.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        wrapped.Tables["Customers"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
        wrapped.Tables["Orders"]!.Rows[2].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Wrap_Preserves_CaseSensitive_Flag()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.CaseSensitive.Should().BeFalse();
    }

    [Fact]
    public void Wrap_Preserves_PrimaryKeys()
    {
        var ds = BuildRichDataSet();
        var wrapped = new AsyncDataSet(ds);
        wrapped.Tables["Customers"]!.PrimaryKey.Should().HaveCount(1);
        wrapped.Tables["Customers"]!.PrimaryKey[0].ColumnName.Should().Be("CustomerId");
    }
}

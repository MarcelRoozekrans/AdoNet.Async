using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableTests
{
    [Fact]
    public void Constructor_Sets_TableName()
    {
        using var table = new AsyncDataTable("Products");

        table.TableName.Should().Be("Products");
    }

    [Fact]
    public void Default_Constructor_Creates_Empty_Table()
    {
        using var table = new AsyncDataTable();

        table.TableName.Should().BeEmpty();
        table.Columns.Count.Should().Be(0);
        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_With_Namespace_Sets_Both()
    {
        using var table = new AsyncDataTable("Products", "urn:test");

        table.TableName.Should().Be("Products");
        table.Namespace.Should().Be("urn:test");
    }

    [Fact]
    public void Columns_Add_And_Rows_Add_Work()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();
        row.InnerDataRow["Id"] = 1;
        row.InnerDataRow["Name"] = "Widget";
        table.InnerDataTable.Rows.Add(row.InnerDataRow);

        table.Columns.Count.Should().Be(2);
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Widget");
    }

    [Fact]
    public async Task AcceptChanges_Clears_Row_States()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);

        row.RowState.Should().Be(DataRowState.Added);

        await table.AcceptChangesAsync();

        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task RejectChanges_Reverts_Changes()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 1;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        await table.AcceptChangesAsync();

        row.InnerDataRow["Id"] = 99;
        row.RowState.Should().Be(DataRowState.Modified);

        table.RejectChanges();

        row["Id"].Should().Be(1);
        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void Clone_Copies_Schema_Only()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 1;
        row.InnerDataRow["Name"] = "Widget";
        table.InnerDataTable.Rows.Add(row.InnerDataRow);

        var clone = table.Clone();

        clone.Columns.Count.Should().Be(2);
        clone.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task Copy_Copies_Schema_And_Data()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 42;
        table.InnerDataTable.Rows.Add(row.InnerDataRow);
        await table.AcceptChangesAsync();

        var copy = table.Copy();

        copy.Columns.Count.Should().Be(1);
        copy.Rows.Count.Should().Be(1);
        copy.Rows[0]["Id"].Should().Be(42);
    }

    [Fact]
    public void Select_Filters_Rows()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.Rows.Add(2, "Bob");
        table.InnerDataTable.Rows.Add(3, "Alice");

        var filtered = table.Select("Name = 'Alice'");

        filtered.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChanges_Returns_Changed_Rows()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(1);
        await table.AcceptChangesAsync();

        table.InnerDataTable.Rows.Add(2);

        var changes = table.GetChanges();

        changes.Should().NotBeNull();
        changes!.Rows.Count.Should().Be(1);
        changes.Rows[0]["Id"].Should().Be(2);
    }

    [Fact]
    public async Task Clear_Removes_All_Rows()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(1);
        table.InnerDataTable.Rows.Add(2);

        await table.ClearAsync();

        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_Fires_TableClearingAsync_Before_And_TableClearedAsync_After()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(1);

        var clearingFired = false;
        var clearedFired = false;
        var clearingOrder = 0;
        var clearedOrder = 0;
        var callCounter = 0;
        var rowCountAtClearing = -1;

        table.TableClearingAsync += (_, _) =>
        {
            clearingOrder = ++callCounter;
            clearingFired = true;
            rowCountAtClearing = table.Rows.Count; // still 1 at this point
            return ValueTask.CompletedTask;
        };
        table.TableClearedAsync += (_, _) =>
        {
            clearedOrder = ++callCounter;
            clearedFired = true;
            return ValueTask.CompletedTask;
        };

        await table.ClearAsync();

        clearingFired.Should().BeTrue();
        clearedFired.Should().BeTrue();
        clearingOrder.Should().BeLessThan(clearedOrder);
        rowCountAtClearing.Should().Be(1); // rows still present when Clearing fired
        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_Respects_CancellationToken()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.InnerDataTable.Rows.Add(1);

        using var cts = new CancellationTokenSource();
        table.TableClearingAsync += (_, _) => { cts.Cancel(); return ValueTask.CompletedTask; };

        Func<Task> act = async () => await table.ClearAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        table.Rows.Count.Should().Be(1); // rows not cleared
    }

    [Fact]
    public async Task AcceptChangesAsync_Fires_RowChangedAsync_Commit_Per_Changed_Row()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.Rows.Add(2, "Bob");
        await table.AcceptChangesAsync();

        table.InnerDataTable.Rows.Add(3, "Added");
        table.InnerDataTable.Rows[0]["Name"] = "Modified";

        var commitArgs = new List<DataRowChangeEventArgs>();
        table.RowChangedAsync += (args, _) =>
        {
            commitArgs.Add(args);
            return ValueTask.CompletedTask;
        };

        await table.AcceptChangesAsync();

        commitArgs.Should().HaveCount(2);
        commitArgs.Should().AllSatisfy(a => a.Action.Should().Be(DataRowAction.Commit));
        table.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        table.Rows[1].RowState.Should().Be(DataRowState.Unchanged);
        table.Rows[2].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void Implicit_Conversion_To_DataTable_Works()
    {
        using var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));

        DataTable dt = asyncTable;

        dt.TableName.Should().Be("Test");
        dt.Columns.Count.Should().Be(1);
    }

    [Fact]
    public void Wrapping_DataTable_Via_Name_Constructor_Reflects_Changes()
    {
        // Since internal constructor is not accessible from test assembly,
        // verify that AsyncDataTable properly delegates to inner DataTable
        using var asyncTable = new AsyncDataTable("Existing");
        asyncTable.Columns.Add("Col1", typeof(string));
        asyncTable.InnerDataTable.Rows.Add("value");

        // Implicit conversion gives us the inner DataTable
        DataTable innerDt = asyncTable;

        innerDt.TableName.Should().Be("Existing");
        innerDt.Columns.Count.Should().Be(1);
        innerDt.Rows.Count.Should().Be(1);
        innerDt.Rows[0]["Col1"].Should().Be("value");
    }
}

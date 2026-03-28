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
    public async Task Columns_Add_And_Rows_Add_Work()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Widget");
        await table.Rows.AddAsync(row);

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
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);

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
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        await row.SetValueAsync("Id", 99);
        row.RowState.Should().Be(DataRowState.Modified);

        table.RejectChanges();

        row["Id"].Should().Be(1);
        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task Clone_Copies_Schema_Only()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        var row = table.NewRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Widget");
        await table.Rows.AddAsync(row);

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
        await row.SetValueAsync("Id", 42);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        var copy = table.Copy();

        copy.Columns.Count.Should().Be(1);
        copy.Rows.Count.Should().Be(1);
        copy.Rows[0]["Id"].Should().Be(42);
    }

    [Fact]
    public async Task Select_Filters_Rows()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        await table.Rows.AddAsync(new object?[] { 1, "Alice" });
        await table.Rows.AddAsync(new object?[] { 2, "Bob" });
        await table.Rows.AddAsync(new object?[] { 3, "Alice" });

        var filtered = table.Select("Name = 'Alice'");

        filtered.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChanges_Returns_Changed_Rows()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });
        await table.AcceptChangesAsync();

        await table.Rows.AddAsync(new object?[] { 2 });

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
        await table.Rows.AddAsync(new object?[] { 1 });
        await table.Rows.AddAsync(new object?[] { 2 });

        await table.ClearAsync();

        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_Fires_TableClearingAsync_Before_And_TableClearedAsync_After()
    {
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });

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
        await table.Rows.AddAsync(new object?[] { 1 });

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
        await table.Rows.AddAsync(new object?[] { 1, "Alice" });
        await table.Rows.AddAsync(new object?[] { 2, "Bob" });
        await table.AcceptChangesAsync();

        await table.Rows.AddAsync(new object?[] { 3, "Added" });
        await table.Rows[0].SetValueAsync("Name", "Modified");

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
    public async Task Row_Indexer_Returns_Same_Instance()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });

        var row1 = table.Rows[0];
        var row2 = table.Rows[0];

        row1.Should().BeSameAs(row2);
    }

    [Fact]
    public void Explicit_Conversion_To_DataTable_Works()
    {
        using var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));

        var dt = (DataTable)asyncTable;

        dt.TableName.Should().Be("Test");
        dt.Columns.Count.Should().Be(1);
    }

    [Fact]
    public async Task Wrapping_DataTable_Via_Name_Constructor_Reflects_Changes()
    {
        // Since internal constructor is not accessible from test assembly,
        // verify that AsyncDataTable properly delegates to inner DataTable
        using var asyncTable = new AsyncDataTable("Existing");
        asyncTable.Columns.Add("Col1", typeof(string));
        await asyncTable.Rows.AddAsync(new object?[] { "value" });

        // Explicit conversion gives us the inner DataTable
        var innerDt = (DataTable)asyncTable;

        innerDt.TableName.Should().Be("Existing");
        innerDt.Columns.Count.Should().Be(1);
        innerDt.Rows.Count.Should().Be(1);
        innerDt.Rows[0]["Col1"].Should().Be("value");
    }

    [Fact]
    public async Task Select_Returns_AsyncDataRow_Array()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });
        await table.Rows.AddAsync(new object?[] { 2 });

        var rows = table.Select();
        rows.Should().HaveCount(2);
        rows.Should().AllBeOfType<AsyncDataRow>();
    }

    [Fact]
    public async Task Select_Returns_Cached_Rows()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });

        var row1 = table.Select()[0];
        var row2 = table.Rows[0];
        row1.Should().BeSameAs(row2);
    }

    [Fact]
    public void Clone_Returns_AsyncDataTable()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        var clone = table.Clone();
        clone.Should().BeOfType<AsyncDataTable>();
    }

    [Fact]
    public async Task GetChanges_Returns_AsyncDataTable()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        await table.Rows.AddAsync(new object?[] { 1 });
        var changes = table.GetChanges();
        changes.Should().NotBeNull();
        changes.Should().BeOfType<AsyncDataTable>();
    }
}

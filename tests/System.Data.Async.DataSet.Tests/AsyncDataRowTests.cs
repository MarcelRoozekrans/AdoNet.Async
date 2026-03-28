using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowTests
{
    private static (AsyncDataTable table, AsyncDataRow row) BuildRow()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        var row = table.NewRow();
        return (table, row);
    }

    [Fact]
    public void NewRow_Returns_AsyncDataRow()
    {
        var (table, row) = BuildRow();
        row.Should().BeOfType<AsyncDataRow>();
        row.Should().NotBeNull();
    }

    [Fact]
    public async Task Indexer_Returns_Value_By_ColumnName()
    {
        var (_, row) = BuildRow();
        await row.SetValueAsync("Id", 42);
        row["Id"].Should().Be(42);
    }

    [Fact]
    public async Task Indexer_Returns_Value_By_ColumnIndex()
    {
        var (_, row) = BuildRow();
        await row.SetValueAsync("Id", 7);
        row[0].Should().Be(7);
    }

    [Fact]
    public async Task SetValueAsync_By_ColumnName_Mutates_Row()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        await row.SetValueAsync("Name", "Alice");

        row["Name"].Should().Be("Alice");
        row.RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public async Task SetValueAsync_By_ColumnIndex_Mutates_Row()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        await row.SetValueAsync(1, "Bob");

        row[1].Should().Be("Bob");
    }

    [Fact]
    public async Task SetValueAsync_By_DataColumn_Mutates_Row()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        var col = table.Columns["Name"]!;
        await row.SetValueAsync(col, "Carol");

        row["Name"].Should().Be("Carol");
    }

    [Fact]
    public async Task SetValueAsync_Fires_ColumnChangingAsync_Before_Mutation()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Before");
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        string? capturedName = null;
        table.ColumnChangingAsync += (args, ct) =>
        {
            capturedName = (string?)row["Name"]; // still "Before" at this point
            return ValueTask.CompletedTask;
        };

        await row.SetValueAsync("Name", "After");

        capturedName.Should().Be("Before");
        row["Name"].Should().Be("After");
    }

    [Fact]
    public async Task SetValueAsync_Fires_ColumnChangedAsync_After_Mutation()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        string? capturedName = null;
        table.ColumnChangedAsync += (args, ct) =>
        {
            capturedName = (string?)row["Name"];
            return ValueTask.CompletedTask;
        };

        await row.SetValueAsync("Name", "Alice");

        capturedName.Should().Be("Alice");
    }

    [Fact]
    public async Task DeleteAsync_Sets_RowState_To_Deleted()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        await row.DeleteAsync();

        row.RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_Fires_RowDeletingAsync_And_RowDeletedAsync()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        var deletingFired = false;
        var deletedFired = false;
        table.RowDeletingAsync += (_, _) => { deletingFired = true; return ValueTask.CompletedTask; };
        table.RowDeletedAsync += (_, _) => { deletedFired = true; return ValueTask.CompletedTask; };

        await row.DeleteAsync();

        deletingFired.Should().BeTrue();
        deletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task AcceptChangesAsync_Accepts_Pending_Changes()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Alice");
        await table.Rows.AddAsync(row);

        await row.AcceptChangesAsync();

        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task RejectChangesAsync_Reverts_Pending_Changes()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Alice");
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        await row.SetValueAsync("Name", "Rejected");
        await row.RejectChangesAsync();

        row["Name"].Should().Be("Alice");
        row.RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public async Task SetValueAsync_Respects_CancellationToken()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        using var cts = new CancellationTokenSource();
        table.ColumnChangingAsync += (_, ct) => { cts.Cancel(); return ValueTask.CompletedTask; };

        Func<Task> act = async () => await row.SetValueAsync("Name", "X", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeleteAsync_Respects_CancellationToken()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await row.DeleteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        row.RowState.Should().Be(DataRowState.Unchanged); // row not deleted
    }

    [Fact]
    public void AsyncDataRow_Can_Be_Subclassed()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        DataTable dt = table;
        var innerRow = dt.NewRow();

        var row = new TestAsyncDataRow(innerRow, table);

        row.Should().BeAssignableTo<AsyncDataRow>();
    }

    private sealed class TestAsyncDataRow : AsyncDataRow
    {
        public TestAsyncDataRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
    }

    [Fact]
    public async Task EndEditAsync_Fires_RowChangingAsync_Before_And_RowChangedAsync_After()
    {
        var (table, row) = BuildRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Before");
        await table.Rows.AddAsync(row);
        await table.AcceptChangesAsync();

        var changingFired = false;
        var changedFired = false;
        var changingOrder = 0;
        var changedOrder = 0;
        var callCounter = 0;

        table.RowChangingAsync += (_, _) => { changingOrder = ++callCounter; changingFired = true; return ValueTask.CompletedTask; };
        table.RowChangedAsync += (_, _) => { changedOrder = ++callCounter; changedFired = true; return ValueTask.CompletedTask; };

        await row.BeginEditAsync();
        await row.SetValueAsync("Name", "After");
        await row.EndEditAsync();

        changingFired.Should().BeTrue();
        changedFired.Should().BeTrue();
        changingOrder.Should().BeLessThan(changedOrder);
        row["Name"].Should().Be("After");
    }
}

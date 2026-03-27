using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowCollectionTests
{
    private static AsyncDataTable BuildTable()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        return table;
    }

    [Fact]
    public async Task AddAsync_With_Values_Adds_Row()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Id"].Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public async Task AddAsync_With_AsyncDataRow_Adds_Row()
    {
        var table = BuildTable();
        var row = table.NewRow();
        row.InnerDataRow["Id"] = 2;
        row.InnerDataRow["Name"] = "Bob";
        await table.Rows.AddAsync(row);
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task Indexer_Returns_AsyncDataRow()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.Rows[0].Should().BeOfType<AsyncDataRow>();
    }

    [Fact]
    public async Task Count_Reflects_Added_Rows()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "A"]);
        await table.Rows.AddAsync([2, "B"]);
        table.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Count_Includes_Deleted_Rows()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "A"]);
        table.AcceptChanges();
        await table.Rows[0].DeleteAsync();
        table.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_Removes_Row()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        var row = table.Rows[0];

        await table.Rows.RemoveAsync(row);

        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAtAsync_Removes_Row_By_Index()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);
        table.AcceptChanges();

        await table.Rows.RemoveAtAsync(0);

        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Id"].Should().Be(2);
    }

    [Fact]
    public async Task AddAsync_Fires_TableNewRowAsync_And_RowChangedAsync()
    {
        var table = BuildTable();
        var newRowFired = false;
        var rowChangedFired = false;
        table.TableNewRowAsync += (_, _) => { newRowFired = true; return ValueTask.CompletedTask; };
        table.RowChangedAsync += (_, _) => { rowChangedFired = true; return ValueTask.CompletedTask; };

        await table.Rows.AddAsync([1, "Alice"]);

        newRowFired.Should().BeTrue();
        rowChangedFired.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_Fires_RowDeletingAsync_And_RowDeletedAsync()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        table.AcceptChanges();
        var row = table.Rows[0];

        var deletingFired = false;
        var deletedFired = false;
        table.RowDeletingAsync += (_, _) => { deletingFired = true; return ValueTask.CompletedTask; };
        table.RowDeletedAsync += (_, _) => { deletedFired = true; return ValueTask.CompletedTask; };

        await table.Rows.RemoveAsync(row);

        deletingFired.Should().BeTrue();
        deletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task Enumerate_Returns_All_Rows_As_AsyncDataRow()
    {
        var table = BuildTable();
        await table.Rows.AddAsync([1, "Alice"]);
        await table.Rows.AddAsync([2, "Bob"]);

        var names = new List<string>();
        foreach (var row in table.Rows)
        {
            names.Add((string)row["Name"]);
        }

        names.Should().Equal("Alice", "Bob");
    }
}

using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableAdvancedTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AsyncDataTable MakeTable(string name, params (string col, Type type)[] columns)
    {
        var t = new AsyncDataTable(name);
        foreach (var (col, type) in columns)
            t.Columns.Add(col, type);
        return t;
    }

    // ------------------------------------------------------------------
    // Merge
    // ------------------------------------------------------------------

    [Fact]
    public async Task Merge_AppendRows_From_Source()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([2, "Bob"]);
        await source.AcceptChangesAsync();

        target.Merge(source);

        // No primary key — DataTable.Merge appends, so the source row is at index 1.
        target.Rows.Count.Should().Be(2);
        target.Rows[1]["Name"].Should().Be("Bob");
    }

    [Fact]
    public async Task Merge_PreserveChanges_True_Keeps_Target_Values()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        target.PrimaryKey = [target.Columns["Id"]!];
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();
        await target.Rows[0].SetValueAsync("Name", "AliceModified");   // pending change

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([1, "AliceFromSource"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: true);

        target.Rows[0]["Name"].Should().Be("AliceModified");
    }

    [Fact]
    public async Task Merge_PreserveChanges_False_Overwrites_With_Source()
    {
        using var target = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        target.PrimaryKey = [target.Columns["Id"]!];
        await target.Rows.AddAsync([1, "Alice"]);
        await target.AcceptChangesAsync();
        await target.Rows[0].SetValueAsync("Name", "AliceModified");   // pending change

        using var source = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await source.Rows.AddAsync([1, "AliceFromSource"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false);

        target.Rows[0]["Name"].Should().Be("AliceFromSource");
    }

    [Fact]
    public async Task Merge_MissingSchemaAction_Add_Adds_Missing_Columns()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));
        await target.Rows.AddAsync([1]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Extra", typeof(string)));
        await source.Rows.AddAsync([2, "extra"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false, MissingSchemaAction.Add);

        target.Columns.Contains("Extra").Should().BeTrue();
        target.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Merge_MissingSchemaAction_Ignore_Skips_Missing_Columns()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));
        await target.Rows.AddAsync([1]);
        await target.AcceptChangesAsync();

        using var source = MakeTable("T", ("Id", typeof(int)), ("Extra", typeof(string)));
        await source.Rows.AddAsync([2, "extra"]);
        await source.AcceptChangesAsync();

        target.Merge(source, preserveChanges: false, MissingSchemaAction.Ignore);

        target.Columns.Contains("Extra").Should().BeFalse();
        target.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Merge_Into_Empty_Table_Appends_All_Rows()
    {
        using var target = MakeTable("T", ("Id", typeof(int)));

        using var source = MakeTable("T", ("Id", typeof(int)));
        await source.Rows.AddAsync([1]);
        await source.Rows.AddAsync([2]);
        await source.AcceptChangesAsync();

        target.Merge(source);

        target.Rows.Count.Should().Be(2);
        target.Rows[0]["Id"].Should().Be(1);
        target.Rows[1]["Id"].Should().Be(2);
    }

    // ------------------------------------------------------------------
    // Compute
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compute_Sum_Returns_Correct_Total()
    {
        using var t = MakeTable("T", ("Price", typeof(decimal)));
        await t.Rows.AddAsync([10m]);
        await t.Rows.AddAsync([20m]);
        await t.Rows.AddAsync([30m]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Sum(Price)", null);

        result.Should().Be(60m);
    }

    [Fact]
    public async Task Compute_Count_Returns_Row_Count()
    {
        using var t = MakeTable("T", ("Id", typeof(int)));
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([2]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Count(Id)", null);

        Convert.ToInt32(result, CultureInfo.InvariantCulture).Should().Be(2);
    }

    [Fact]
    public async Task Compute_With_Filter_Counts_Matching_Rows()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Active", typeof(bool)));
        await t.Rows.AddAsync([1, true]);
        await t.Rows.AddAsync([2, false]);
        await t.Rows.AddAsync([3, true]);
        await t.AcceptChangesAsync();

        var result = t.Compute("Count(Id)", "Active = true");

        Convert.ToInt32(result, CultureInfo.InvariantCulture).Should().Be(2);
    }

    [Fact]
    public async Task Compute_Min_And_Max()
    {
        using var t = MakeTable("T", ("Score", typeof(int)));
        await t.Rows.AddAsync([5]);
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([9]);
        await t.AcceptChangesAsync();

        t.Compute("Min(Score)", null).Should().Be(1);
        t.Compute("Max(Score)", null).Should().Be(9);
    }

    [Fact]
    public async Task Compute_On_Empty_Table_Returns_DBNull()
    {
        using var t = MakeTable("T", ("Price", typeof(decimal)));
        await t.AcceptChangesAsync();

        var result = t.Compute("Sum(Price)", null);

        result.Should().Be(DBNull.Value);
    }
}

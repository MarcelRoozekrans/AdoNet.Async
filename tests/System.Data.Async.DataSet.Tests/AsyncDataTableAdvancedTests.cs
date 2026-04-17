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
    }
}

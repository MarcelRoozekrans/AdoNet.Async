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
    public async Task Compute_Min_Returns_Minimum_Value()
    {
        using var t = MakeTable("T", ("Score", typeof(int)));
        await t.Rows.AddAsync([5]);
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([9]);
        await t.AcceptChangesAsync();

        t.Compute("Min(Score)", null).Should().Be(1);
    }

    [Fact]
    public async Task Compute_Max_Returns_Maximum_Value()
    {
        using var t = MakeTable("T", ("Score", typeof(int)));
        await t.Rows.AddAsync([5]);
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([9]);
        await t.AcceptChangesAsync();

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

    // ------------------------------------------------------------------
    // LoadDataRow
    // ------------------------------------------------------------------

    [Fact]
    public void LoadDataRow_Bool_True_Accepts_Row()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];

        var row = t.LoadDataRow([1, "Alice"], fAcceptChanges: true);

        t.Rows.Count.Should().Be(1);
        row.RowState.Should().Be(DataRowState.Unchanged);
        row["Name"].Should().Be("Alice");
    }

    [Fact]
    public void LoadDataRow_Bool_False_Leaves_Row_Added()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];

        var row = t.LoadDataRow([1, "Alice"], fAcceptChanges: false);

        t.Rows.Count.Should().Be(1);
        row.RowState.Should().Be(DataRowState.Added);
        row["Id"].Should().Be(1);
    }

    [Fact]
    public void LoadDataRow_Upsert_Updates_Existing_Row()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);

        t.LoadDataRow([1, "AliceUpdated"], LoadOption.Upsert);

        t.Rows.Count.Should().Be(1);
        t.Rows[0]["Name"].Should().Be("AliceUpdated");
    }

    [Fact]
    public async Task LoadDataRow_OverwriteChanges_Overwrites_Current_And_Original()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);
        await t.Rows[0].SetValueAsync("Name", "AliceEdited");  // pending change

        t.LoadDataRow([1, "AliceOverwrite"], LoadOption.OverwriteChanges);

        t.Rows[0]["Name", DataRowVersion.Current].Should().Be("AliceOverwrite");
        t.Rows[0]["Name", DataRowVersion.Original].Should().Be("AliceOverwrite");
    }

    [Fact]
    public async Task LoadDataRow_PreserveChanges_Keeps_Current_Updates_Original()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        t.PrimaryKey = [t.Columns["Id"]!];
        t.LoadDataRow([1, "Alice"], fAcceptChanges: true);
        await t.Rows[0].SetValueAsync("Name", "AliceEdited");  // pending change

        t.LoadDataRow([1, "AlicePreserved"], LoadOption.PreserveChanges);

        t.Rows[0]["Name", DataRowVersion.Current].Should().Be("AliceEdited");
        t.Rows[0]["Name", DataRowVersion.Original].Should().Be("AlicePreserved");
    }

    // ------------------------------------------------------------------
    // BeginInit / EndInit
    // ------------------------------------------------------------------

    [Fact]
    public void BeginInit_And_EndInit_Do_Not_Throw()
    {
        using var t = new AsyncDataTable("T");

        var act = () =>
        {
            t.BeginInit();
            t.Columns.Add("Id", typeof(int));
            t.EndInit();
        };

        act.Should().NotThrow();
        t.Columns.Contains("Id").Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // BeginLoadData / EndLoadData
    // ------------------------------------------------------------------

    [Fact]
    public async Task BeginLoadData_And_EndLoadData_Do_Not_Throw()
    {
        using var t = MakeTable("T", ("Id", typeof(int)));

        t.BeginLoadData();
        await t.Rows.AddAsync([1]);
        t.EndLoadData();

        t.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task BeginLoadData_Allows_Bulk_Add_And_EndLoadData_Completes()
    {
        // BeginLoadData suspends inner DataTable index maintenance and sync row events.
        // The async RowChangedAsync event is independent of that suspension, so we
        // simply verify that rows added during the load window are present after EndLoadData.
        using var t = MakeTable("T", ("Id", typeof(int)));
        var rowChangedCount = 0;
        t.RowChangedAsync += (_, _) => { rowChangedCount++; return ValueTask.CompletedTask; };

        t.BeginLoadData();
        await t.Rows.AddAsync([1]);
        await t.Rows.AddAsync([2]);
        t.EndLoadData();

        t.Rows.Count.Should().Be(2);
        t.Rows[0]["Id"].Should().Be(1);
        t.Rows[1]["Id"].Should().Be(2);
        // RowChangedAsync fires for each AddAsync — BeginLoadData does not suppress async events
        rowChangedCount.Should().Be(2);
    }

    // ------------------------------------------------------------------
    // Reset
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reset_Clears_Rows_And_Columns()
    {
        using var t = MakeTable("T", ("Id", typeof(int)), ("Name", typeof(string)));
        await t.Rows.AddAsync([1, "Alice"]);
        await t.AcceptChangesAsync();

        t.Reset();

        t.Rows.Count.Should().Be(0);
        t.Columns.Count.Should().Be(0);
    }

    [Fact]
    public void Reset_On_Empty_Table_Does_Not_Throw()
    {
        using var t = new AsyncDataTable("T");

        var act = () => t.Reset();

        act.Should().NotThrow();
    }
}

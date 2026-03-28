using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class TypedDataRowTests
{
    [Fact]
    public async Task BeginEditAsync_EndEditAsync_Commits_New_Value()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        await row.BeginEditAsync();
        await row.SetNameAsync("Bob");
        await row.EndEditAsync();

        row.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task CancelEditAsync_Restores_Original_Value()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        await row.BeginEditAsync();
        await row.SetNameAsync("Bob");
        await row.CancelEditAsync();

        row.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task DeleteAsync_Sets_RowState_To_Deleted()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await row.AcceptChangesAsync();

        await row.DeleteAsync();

        row.RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task RowState_Transitions_Added_Unchanged_Modified_Deleted()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        // After add: Added
        row.RowState.Should().Be(DataRowState.Added);

        // After AcceptChanges: Unchanged
        await row.AcceptChangesAsync();
        row.RowState.Should().Be(DataRowState.Unchanged);

        // After modify: Modified
        await row.SetNameAsync("Bob");
        row.RowState.Should().Be(DataRowState.Modified);

        // After delete: Deleted
        await row.DeleteAsync();
        row.RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public async Task HasErrors_And_RowError()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        row.HasErrors.Should().BeFalse();

        // Access inner DataTable via implicit cast to set RowError
        DataTable innerTable = ds.Customer;
        innerTable.Rows[0].RowError = "Something went wrong";

        row.HasErrors.Should().BeTrue();
        row.RowError.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task HasVersion_Current_And_Original()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        // Added row has Current but not Original
        row.HasVersion(DataRowVersion.Current).Should().BeTrue();
        row.HasVersion(DataRowVersion.Original).Should().BeFalse();

        // After AcceptChanges, Original becomes available
        await row.AcceptChangesAsync();
        row.HasVersion(DataRowVersion.Current).Should().BeTrue();
        row.HasVersion(DataRowVersion.Original).Should().BeTrue();
    }

    [Fact]
    public async Task Indexer_By_Column_Index_Returns_Correct_Value()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        var row = ds.Customer[0];

        // Column 0 = CustomerId, Column 1 = Name
        row[0].Should().Be(1);
        row[1].Should().Be("Alice");
    }

    [Fact]
    public async Task Indexer_By_DataColumn_Returns_Correct_Value()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        var row = ds.Customer[0];

        row[ds.Customer.CustomerIdColumn].Should().Be(1);
        row[ds.Customer.NameColumn].Should().Be("Alice");
    }

    [Fact]
    public async Task Indexer_With_Version_Returns_Original_And_Current()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await row.AcceptChangesAsync();

        await row.SetNameAsync("Bob");

        row["Name", DataRowVersion.Original].Should().Be("Alice");
        row["Name", DataRowVersion.Current].Should().Be("Bob");
    }
}

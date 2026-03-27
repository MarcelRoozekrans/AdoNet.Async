using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class EventParityTests
{
    private readonly ValidationFixture _fixture;

    public EventParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RowChanged_And_RowChanging_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.Columns.Add("Name", typeof(string));
        rawTable.RowChanging += (_, e) => rawEvents.Add($"Changing:{e.Action}");
        rawTable.RowChanged += (_, e) => rawEvents.Add($"Changed:{e.Action}");

        var rawRow = rawTable.NewRow();
        rawRow["Id"] = 1;
        rawRow["Name"] = "Alice";
        rawTable.Rows.Add(rawRow);
        rawRow["Name"] = "Updated";

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.Columns.Add("Name", typeof(string));
        asyncTable.RowChanging += (_, e) => asyncEvents.Add($"Changing:{e.Action}");
        asyncTable.RowChanged += (_, e) => asyncEvents.Add($"Changed:{e.Action}");

        var asyncRow = asyncTable.NewRow();
        await asyncRow.SetValueAsync("Id", 1);
        await asyncRow.SetValueAsync("Name", "Alice");
        await asyncTable.Rows.AddAsync(asyncRow);
        await asyncRow.SetValueAsync("Name", "Updated");

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task RowDeleted_And_RowDeleting_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.RowDeleting += (_, e) => rawEvents.Add($"Deleting:{e.Action}");
        rawTable.RowDeleted += (_, e) => rawEvents.Add($"Deleted:{e.Action}");
        var rawRow = rawTable.NewRow();
        rawRow["Id"] = 1;
        rawTable.Rows.Add(rawRow);
        rawTable.AcceptChanges();
        rawRow.Delete();

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.RowDeleting += (_, e) => asyncEvents.Add($"Deleting:{e.Action}");
        asyncTable.RowDeleted += (_, e) => asyncEvents.Add($"Deleted:{e.Action}");
        var asyncRow = asyncTable.NewRow();
        await asyncRow.SetValueAsync("Id", 1);
        await asyncTable.Rows.AddAsync(asyncRow);
        await asyncTable.AcceptChangesAsync();
        await asyncRow.DeleteAsync();

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ColumnChanged_And_ColumnChanging_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Val", typeof(string));
        rawTable.ColumnChanging += (_, e) => rawEvents.Add($"Changing:{e.Column!.ColumnName}");
        rawTable.ColumnChanged += (_, e) => rawEvents.Add($"Changed:{e.Column!.ColumnName}");
        var rawRow = rawTable.NewRow();
        rawRow["Val"] = "original";
        rawTable.Rows.Add(rawRow);
        rawRow["Val"] = "updated";

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Val", typeof(string));
        asyncTable.ColumnChanging += (_, e) => asyncEvents.Add($"Changing:{e.Column!.ColumnName}");
        asyncTable.ColumnChanged += (_, e) => asyncEvents.Add($"Changed:{e.Column!.ColumnName}");
        var asyncRow = asyncTable.NewRow();
        await asyncRow.SetValueAsync("Val", "original");
        await asyncTable.Rows.AddAsync(asyncRow);
        await asyncRow.SetValueAsync("Val", "updated");

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task TableCleared_And_TableClearing_Fire_In_Same_Order()
    {
        var rawEvents = new List<string>();
        var asyncEvents = new List<string>();

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.Rows.Add(rawTable.NewRow());
        rawTable.TableClearing += (_, _) => rawEvents.Add("Clearing");
        rawTable.TableCleared += (_, _) => rawEvents.Add("Cleared");
        rawTable.Clear();

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        await asyncTable.Rows.AddAsync(asyncTable.NewRow());
        asyncTable.TableClearing += (_, _) => asyncEvents.Add("Clearing");
        asyncTable.TableCleared += (_, _) => asyncEvents.Add("Cleared");
        await asyncTable.ClearAsync();

        asyncEvents.Should().BeEquivalentTo(rawEvents, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task TableNewRow_Fires_Same_As_Raw()
    {
        var rawFired = false;
        var asyncFired = false;

        var rawTable = new DataTable("Test");
        rawTable.Columns.Add("Id", typeof(int));
        rawTable.TableNewRow += (_, _) => rawFired = true;
        rawTable.Rows.Add(rawTable.NewRow());

        var asyncTable = new AsyncDataTable("Test");
        asyncTable.Columns.Add("Id", typeof(int));
        asyncTable.TableNewRow += (_, _) => asyncFired = true;
        await asyncTable.Rows.AddAsync(asyncTable.NewRow());

        asyncFired.Should().Be(rawFired);
    }
}

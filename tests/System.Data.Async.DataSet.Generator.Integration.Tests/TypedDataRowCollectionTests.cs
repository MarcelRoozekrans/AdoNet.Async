using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class TypedDataRowCollectionTests
{
    [Fact]
    public async Task Count_Reflects_Added_Rows()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");
        await ds.Customer.AddCustomerRowAsync(3, "Charlie", "charlie@example.com");

        ds.Customer.Rows.Count.Should().Be(3);
    }

    [Fact]
    public async Task Typed_Enumeration_Yields_AsyncOrdersDSCustomerRow_Instances()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

        foreach (var row in ds.Customer.Rows)
        {
            row.Should().BeOfType<AsyncOrdersDSCustomerRow>();
        }
    }

    [Fact]
    public async Task Typed_Indexer_Returns_AsyncOrdersDSCustomerRow()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        AsyncOrdersDSCustomerRow row = ds.Customer.Rows[0];
        row.Should().BeOfType<AsyncOrdersDSCustomerRow>();
        row.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task RemoveAtAsync_Removes_Correct_Row()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");
        await ds.Customer.AddCustomerRowAsync(3, "Charlie", "charlie@example.com");

        await ds.Customer.Rows.RemoveAtAsync(1);

        ds.Customer.Rows.Count.Should().Be(2);
        ds.Customer[0].Name.Should().Be("Alice");
        ds.Customer[1].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task RemoveAsync_By_Typed_Row_Decreases_Count()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        var bob = await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");
        await ds.Customer.AddCustomerRowAsync(3, "Charlie", "charlie@example.com");

        await ds.Customer.Rows.RemoveAsync(bob);

        ds.Customer.Rows.Count.Should().Be(2);
        ds.Customer[0].Name.Should().Be("Alice");
        ds.Customer[1].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task Contains_Returns_True_For_Existing_Key_And_False_For_Missing()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

        ds.Customer.Rows.Contains(1).Should().BeTrue();
        ds.Customer.Rows.Contains(2).Should().BeTrue();
        ds.Customer.Rows.Contains(999).Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_Fires_RowChangedAsync_Event()
    {
        using var ds = new AsyncOrdersDS();
        var eventFired = false;

        ds.Customer.RowChangedAsync += (args, ct) =>
        {
            eventFired = true;
            return ValueTask.CompletedTask;
        };

        var row = ds.Customer.NewCustomerRow();
        await row.SetCustomerIdAsync(1);
        await row.SetNameAsync("Alice");
        await ds.Customer.Rows.AddAsync(row);

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task Multiple_Adds_And_Iteration_Returns_All_Typed_Rows()
    {
        using var ds = new AsyncOrdersDS();
        var names = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve" };

        for (int i = 0; i < names.Length; i++)
        {
            await ds.Customer.AddCustomerRowAsync(i + 1, names[i], $"{names[i].ToLowerInvariant()}@example.com");
        }

        var collected = new List<AsyncOrdersDSCustomerRow>();
        foreach (AsyncOrdersDSCustomerRow row in ds.Customer.Rows)
        {
            row.Should().BeOfType<AsyncOrdersDSCustomerRow>();
            collected.Add(row);
        }

        collected.Should().HaveCount(5);
        collected.Select(r => r.Name).Should().BeEquivalentTo(names);
    }
}

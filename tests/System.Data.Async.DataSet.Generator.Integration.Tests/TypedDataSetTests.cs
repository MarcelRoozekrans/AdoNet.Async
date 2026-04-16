using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class TypedDataSetTests
{
    [Fact]
    public void Can_Create_Typed_DataSet()
    {
        using var ds = new AsyncOrdersDS();
        ds.Customer.Should().NotBeNull();
        ds.Order.Should().NotBeNull();
    }

    [Fact]
    public void Typed_Table_Has_Column_Properties()
    {
        using var ds = new AsyncOrdersDS();
        ds.Customer.CustomerIdColumn.Should().NotBeNull();
        ds.Customer.NameColumn.Should().NotBeNull();
        ds.Customer.EmailColumn.Should().NotBeNull();
        ds.Order.OrderIdColumn.Should().NotBeNull();
        ds.Order.OrderDateColumn.Should().NotBeNull();
        ds.Order.TotalColumn.Should().NotBeNull();
    }

    [Fact]
    public async Task Can_Add_Typed_Row_Via_NewRow()
    {
        using var ds = new AsyncOrdersDS();
        var row = ds.Customer.NewCustomerRow();
        await row.SetCustomerIdAsync(1);
        await row.SetNameAsync("Alice");
        await ds.Customer.Rows.AddAsync(row);

        ds.Customer.Count.Should().Be(1);
        ds.Customer[0].CustomerId.Should().Be(1);
        ds.Customer[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Can_Add_Row_With_Parameters()
    {
        using var ds = new AsyncOrdersDS();
        // AddCustomerRowAsync includes all non-hidden non-autoincrement columns:
        // CustomerId, Name, Email
        var row = await ds.Customer.AddCustomerRowAsync(1, "Bob", "bob@example.com");

        ds.Customer.Count.Should().Be(1);
        row.Name.Should().Be("Bob");
        row.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public async Task FindBy_Primary_Key()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        await ds.Customer.AddCustomerRowAsync(2, "Bob", "bob@example.com");

        var found = ds.Customer.FindByCustomerId(2);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Nullable_Column_IsNull_And_SetValue()
    {
        using var ds = new AsyncOrdersDS();
        var row = ds.Customer.NewCustomerRow();
        await row.SetCustomerIdAsync(1);
        await row.SetNameAsync("Alice");
        await ds.Customer.Rows.AddAsync(row);

        // Email was not set, so it should be null
        ds.Customer[0].IsEmailNull().Should().BeTrue();

        await ds.Customer[0].SetEmailAsync("alice@example.com");
        ds.Customer[0].IsEmailNull().Should().BeFalse();
        ds.Customer[0].Email.Should().Be("alice@example.com");

        await ds.Customer[0].SetEmailNullAsync();
        ds.Customer[0].IsEmailNull().Should().BeTrue();
    }

    [Fact]
    public async Task Nullable_Column_With_Throw_Behavior_Throws_StrongTypingException()
    {
        using var ds = new AsyncOrdersDS();
        var row = ds.Customer.NewCustomerRow();
        await row.SetCustomerIdAsync(1);
        await row.SetNameAsync("Alice");
        await ds.Customer.Rows.AddAsync(row);

        // Email is null by default and has Throw behavior
        var act = () => ds.Customer[0].Email;
        act.Should().Throw<StrongTypingException>();
    }

    [Fact]
    public async Task Nullable_Column_With_Empty_Behavior_Returns_Empty()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        // Notes has codegen:nullValue="" so ReturnEmpty behavior
        var order = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Some notes");

        // Set notes to null
        await order.SetNotesNullAsync();
        order.IsNotesNull().Should().BeTrue();
        // With ReturnEmpty behavior, should return "" instead of throwing
        order.Notes.Should().Be("");
    }

    [Fact]
    public async Task Relation_Navigation_Parent_To_Child()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");

        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Order 1");
        await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 49.99m, "Order 2");

        var orders = customer.GetOrderRows();
        orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Relation_Navigation_Child_To_Parent()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        var order = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 99.99m, "Test order");

        order.CustomerRow.Should().NotBeNull();
        order.CustomerRow!.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Typed_Row_Is_AsyncDataRow()
    {
        using var ds = new AsyncOrdersDS();
        await ds.Customer.AddCustomerRowAsync(1, "Test", "test@example.com");

        AsyncDataRow untyped = ds.Customer[0];
        untyped.Should().BeOfType<AsyncOrdersDSCustomerRow>();
    }

    [Fact]
    public async Task Remove_Typed_Row()
    {
        using var ds = new AsyncOrdersDS();
        var row = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        ds.Customer.Count.Should().Be(1);

        await ds.Customer.RemoveCustomerRowAsync(row);
        ds.Customer.Count.Should().Be(0);
    }

    [Fact]
    public void Clone_Typed_DataSet()
    {
        using var ds = new AsyncOrdersDS();
        using var clone = ds.Clone();

        clone.Should().NotBeNull();
        clone.Should().BeOfType<AsyncOrdersDS>();
    }

    [Fact]
    public void DataSet_Has_Relation()
    {
        using var ds = new AsyncOrdersDS();
        ds.FK_Customer_Order.Should().NotBeNull();
        ds.FK_Customer_Order.RelationName.Should().Be("FK_Customer_Order");
    }

    [Fact]
    public async Task Order_AutoIncrement_Column()
    {
        using var ds = new AsyncOrdersDS();
        var customer = await ds.Customer.AddCustomerRowAsync(1, "Alice", "alice@example.com");
        var order1 = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 10m, "First");
        var order2 = await ds.Order.AddOrderRowAsync(customer, DateTime.Now, 20m, "Second");

        // OrderId is auto-increment starting at 1
        order1.OrderId.Should().Be(1);
        order2.OrderId.Should().Be(2);
    }
}

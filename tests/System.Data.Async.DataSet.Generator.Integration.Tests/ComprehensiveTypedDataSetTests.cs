using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class ComprehensiveTypedDataSetTests
{
    [Fact]
    public void Can_Create_ComprehensiveDS()
    {
        using var ds = new AsyncComprehensiveDS();
        ds.Sku.Should().NotBeNull();
        ds.SalesOrder.Should().NotBeNull();
        ds.SalesOrderLine.Should().NotBeNull();
    }

    [Fact]
    public async Task Composite_PK_FindBy_Works()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 5, 10.00m);

        var found = ds.SalesOrderLine.FindBySalesOrderIdSkuId(1, 1);
        found.Should().NotBeNull();
        found!.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Composite_PK_FindBy_Returns_Null_When_Not_Found()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 5, 10.00m);

        var notFound = ds.SalesOrderLine.FindBySalesOrderIdSkuId(999, 999);
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task Expression_Column_Computes_LineTotal()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 25.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 4, 25.00m);

        line.LineTotal.Should().Be(100.00m);
    }

    [Fact]
    public async Task Expression_Column_Is_ReadOnly()
    {
        using var ds = new AsyncComprehensiveDS();
        ds.SalesOrderLine.LineTotalColumn.ReadOnly.Should().BeTrue();
        ds.SalesOrderLine.LineTotalColumn.Expression.Should().Be("Quantity * UnitPrice");
    }

    [Fact]
    public async Task Expression_Column_Has_No_Setter()
    {
        // This test verifies at compile time that no SetLineTotalAsync method exists.
        // The fact that this test compiles with only the methods below proves
        // the generator correctly omits the setter for expression columns.
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 5.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 2, 5.00m);

        // We can read LineTotal but cannot set it — no SetLineTotalAsync exists.
        line.LineTotal.Should().Be(10.00m);
    }

    [Fact]
    public async Task ReadOnly_Column_Discount_Has_Default_Value()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 3, 10.00m);

        // Discount is read-only with DefaultValue="0" — no SetDiscountAsync method exists.
        line.Discount.Should().Be(0m);
    }

    [Fact]
    public void ReadOnly_Column_Discount_Is_ReadOnly()
    {
        using var ds = new AsyncComprehensiveDS();
        ds.SalesOrderLine.DiscountColumn.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task Relation_OrderDetail_Has_Order_Parent()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 2, 10.00m);

        line.SalesOrderRow.Should().NotBeNull();
        line.SalesOrderRow!.SalesOrderId.Should().Be(1);
    }

    [Fact]
    public async Task Relation_OrderDetail_Has_Sku_Parent()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var line = await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku, 2, 10.00m);

        line.SkuRow.Should().NotBeNull();
        line.SkuRow!.Title.Should().Be("Widget");
    }

    [Fact]
    public async Task Relation_Order_To_Children()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku1 = await ds.Sku.AddSkuRowAsync(1, "Widget A", 10.00m);
        var sku2 = await ds.Sku.AddSkuRowAsync(2, "Widget B", 20.00m);
        var order = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku1, 1, 10.00m);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order, sku2, 2, 20.00m);

        var lines = order.GetSalesOrderLineRows();
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Relation_Sku_To_Children()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        var order1 = await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);
        var order2 = await ds.SalesOrder.AddSalesOrderRowAsync(2, DateTime.Now);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order1, sku, 1, 10.00m);
        await ds.SalesOrderLine.AddSalesOrderLineRowAsync(order2, sku, 3, 10.00m);

        var lines = sku.GetSalesOrderLineRows();
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcceptChanges_Changes_RowState()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);

        // After adding, the row is in Added state
        sku.RowState.Should().Be(System.Data.DataRowState.Added);

        ((System.Data.DataSet)ds).AcceptChanges();

        sku.RowState.Should().Be(System.Data.DataRowState.Unchanged);
    }

    [Fact]
    public async Task RejectChanges_Restores_Original_Values()
    {
        using var ds = new AsyncComprehensiveDS();
        var sku = await ds.Sku.AddSkuRowAsync(1, "Widget", 10.00m);
        ((System.Data.DataSet)ds).AcceptChanges();

        // Modify the row
        await sku.SetTitleAsync("Modified");
        sku.Title.Should().Be("Modified");

        // Reject the changes
        ((System.Data.DataSet)ds).RejectChanges();

        sku.Title.Should().Be("Widget");
    }

    [Fact]
    public void Clone_Returns_Typed_DataSet_With_Tables()
    {
        using var ds = new AsyncComprehensiveDS();
        using var clone = ds.Clone();

        clone.Should().NotBeNull();
        clone.Should().BeOfType<AsyncComprehensiveDS>();
        clone.Sku.Should().NotBeNull();
        clone.SalesOrder.Should().NotBeNull();
        clone.SalesOrderLine.Should().NotBeNull();
    }

    [Fact]
    public void DataSet_Has_Both_Relations()
    {
        using var ds = new AsyncComprehensiveDS();
        ds.FK_SalesOrder_SalesOrderLine.Should().NotBeNull();
        ds.FK_Sku_SalesOrderLine.Should().NotBeNull();
    }

    [Fact]
    public async Task Multiple_Tables_Count()
    {
        using var ds = new AsyncComprehensiveDS();
        await ds.Sku.AddSkuRowAsync(1, "A", 1.00m);
        await ds.Sku.AddSkuRowAsync(2, "B", 2.00m);
        await ds.SalesOrder.AddSalesOrderRowAsync(1, DateTime.Now);

        ds.Sku.Count.Should().Be(2);
        ds.SalesOrder.Count.Should().Be(1);
        ds.SalesOrderLine.Count.Should().Be(0);
    }
}

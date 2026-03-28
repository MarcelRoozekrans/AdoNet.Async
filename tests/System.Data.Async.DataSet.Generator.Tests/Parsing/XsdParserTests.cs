using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Tests.Parsing;

public class XsdParserTests
{
    private static string LoadSchema(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", fileName);
        return File.ReadAllText(path);
    }

    private static DataSetModel ParseSimple()
    {
        return XsdParser.Parse(LoadSchema("Simple.xsd"));
    }

    [Fact]
    public void Parse_Simple_DataSetName()
    {
        var model = ParseSimple();
        model.Name.Should().Be("OrdersDS");
    }

    [Fact]
    public void Parse_Simple_Locale()
    {
        var model = ParseSimple();
        model.Locale.Should().Be("en-US");
    }

    [Fact]
    public void Parse_Simple_Tables()
    {
        var model = ParseSimple();
        model.Tables.Should().HaveCount(2);
        model.Tables.Select(t => t.Name).Should().BeEquivalentTo("Customer", "Order");
    }

    [Fact]
    public void Parse_Simple_Customer_Columns()
    {
        var model = ParseSimple();
        var customer = model.Tables.First(t => string.Equals(t.Name, "Customer", StringComparison.Ordinal));
        customer.Columns.Should().HaveCount(3);

        var customerId = customer.Columns.First(c => string.Equals(c.Name, "CustomerId", StringComparison.Ordinal));
        customerId.ClrTypeName.Should().Be("System.Int32");
        customerId.AllowDBNull.Should().BeFalse();

        var name = customer.Columns.First(c => string.Equals(c.Name, "Name", StringComparison.Ordinal));
        name.ClrTypeName.Should().Be("System.String");
        name.AllowDBNull.Should().BeFalse();

        var email = customer.Columns.First(c => string.Equals(c.Name, "Email", StringComparison.Ordinal));
        email.ClrTypeName.Should().Be("System.String");
        email.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_Simple_AutoIncrement()
    {
        var model = ParseSimple();
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        var orderId = order.Columns.First(c => string.Equals(c.Name, "OrderId", StringComparison.Ordinal));

        orderId.AutoIncrement.Should().BeTrue();
        orderId.AutoIncrementSeed.Should().Be(1);
        orderId.AutoIncrementStep.Should().Be(1);
    }

    [Fact]
    public void Parse_Simple_NullValueBehavior_Empty()
    {
        var model = ParseSimple();
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        var notes = order.Columns.First(c => string.Equals(c.Name, "Notes", StringComparison.Ordinal));

        notes.AllowDBNull.Should().BeTrue();
        notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnEmpty);
    }

    [Fact]
    public void Parse_Simple_PrimaryKeys()
    {
        var model = ParseSimple();
        var customer = model.Tables.First(t => string.Equals(t.Name, "Customer", StringComparison.Ordinal));
        customer.PrimaryKeyColumnNames.Should().BeEquivalentTo("CustomerId");
    }

    [Fact]
    public void Parse_Simple_Relations()
    {
        var model = ParseSimple();
        model.Relations.Should().HaveCount(1);

        var rel = model.Relations[0];
        rel.Name.Should().Be("FK_Customer_Order");
        rel.ParentTableName.Should().Be("Customer");
        rel.ParentColumnNames.Should().BeEquivalentTo("CustomerId");
        rel.ChildTableName.Should().Be("Order");
        rel.ChildColumnNames.Should().BeEquivalentTo("CustomerId");
    }

    // --- Advanced XSD tests ---

    [Fact]
    public void Parse_Advanced_CaseSensitive()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        model.CaseSensitive.Should().BeTrue();
        model.EnforceConstraints.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_TypedName_Override()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var category = model.Tables.First(t => string.Equals(t.Name, "Category", StringComparison.Ordinal));
        category.TypedName.Should().Be("CategoryEntry");
        category.TypedPlural.Should().Be("Categories");
    }

    [Fact]
    public void Parse_Advanced_NullValue_Null()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var desc = model.Tables.First(t => string.Equals(t.Name, "Category", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Description", StringComparison.Ordinal));
        desc.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnNull);
    }

    [Fact]
    public void Parse_Advanced_NullValue_Replacement()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var notes = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Notes", StringComparison.Ordinal));
        notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReplacementValue);
        notes.NullValueBehavior.ReplacementValue.Should().Be("N/A");
    }

    [Fact]
    public void Parse_Advanced_DefaultValue()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var price = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Price", StringComparison.Ordinal));
        price.DefaultValue.Should().Be("0");
    }

    [Fact]
    public void Parse_Advanced_ReadOnly()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var stock = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Stock", StringComparison.Ordinal));
        stock.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_Expression()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var total = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "TotalValue", StringComparison.Ordinal));
        total.Expression.Should().Be("Price * Stock");
        total.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_DataType_Override()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var sku = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Sku", StringComparison.Ordinal));
        sku.ClrTypeName.Should().Be("System.Guid");
    }

    [Fact]
    public void Parse_Advanced_TypedParent_TypedChildren()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var rel = model.Relations.First(r => string.Equals(r.Name, "FK_Category_Product", StringComparison.Ordinal));
        rel.TypedParent.Should().Be("Category");
        rel.TypedChildren.Should().Be("GetProducts");
    }
}

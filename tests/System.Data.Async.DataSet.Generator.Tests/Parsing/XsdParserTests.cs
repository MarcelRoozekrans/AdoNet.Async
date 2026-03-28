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
    private static DataSetModel ParseSimple()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", "Simple.xsd");
        var xsd = File.ReadAllText(path);
        return XsdParser.Parse(xsd);
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
}

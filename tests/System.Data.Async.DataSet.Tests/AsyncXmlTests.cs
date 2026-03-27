using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncXmlTests
{
    [Fact]
    public async Task AsyncDataTable_WriteXml_ReadXml_Roundtrip()
    {
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(double));
        table.InnerDataTable.Rows.Add(1, "Widget", 9.99);
        table.InnerDataTable.Rows.Add(2, "Gadget", 19.99);

        // Write XML with schema
        using var schemaStream = new MemoryStream();
        await table.WriteXmlSchemaAsync(schemaStream);

        using var dataStream = new MemoryStream();
        await table.WriteXmlAsync(dataStream);

        // Read XML back
        var result = new AsyncDataTable();
        schemaStream.Position = 0;
        await result.ReadXmlSchemaAsync(schemaStream);

        dataStream.Position = 0;
        await result.ReadXmlAsync(dataStream);

        result.TableName.Should().Be("Products");
        result.Columns.Count.Should().Be(3);
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Widget");
        result.Rows[1]["Name"].Should().Be("Gadget");
        ((double)result.Rows[0]["Price"]).Should().BeApproximately(9.99, 0.001);
    }

    [Fact]
    public async Task AsyncDataSet_WriteXml_ReadXml_Roundtrip()
    {
        var ds = new AsyncDataSet("TestDS");
        var table = new DataTable("Orders");
        table.Columns.Add("OrderId", typeof(int));
        table.Columns.Add("Total", typeof(double));
        table.Rows.Add(1, 99.99);
        table.Rows.Add(2, 150.00);
        ds.Tables.Add(table);

        // Write with schema mode
        using var stream = new MemoryStream();
        await ds.WriteXmlAsync(stream);

        // We also need the schema
        using var schemaStream = new MemoryStream();
        await ds.WriteXmlSchemaAsync(schemaStream);

        // Read back
        var result = new AsyncDataSet();
        schemaStream.Position = 0;
        await result.ReadXmlSchemaAsync(schemaStream);

        stream.Position = 0;
        await result.ReadXmlAsync(stream);

        result.Tables.Count.Should().Be(1);
        result.Tables["Orders"]!.Rows.Count.Should().Be(2);
        ((double)result.Tables["Orders"]!.Rows[0]["Total"]).Should().BeApproximately(99.99, 0.001);
    }

    [Fact]
    public async Task AsyncDataTable_WriteXmlAsync_Produces_Valid_Xml()
    {
        var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Value", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Test");

        using var stream = new MemoryStream();
        await table.WriteXmlAsync(stream);

        stream.Length.Should().BeGreaterThan(0);

        // Verify it's valid XML by reading it back
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var xml = await reader.ReadToEndAsync();
        xml.Should().Contain("Items");
        xml.Should().Contain("Test");
    }
}

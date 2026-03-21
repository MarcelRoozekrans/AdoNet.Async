using FluentAssertions;
using NSubstitute;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableLoadAsyncTests
{
#pragma warning disable CA2012 // NSubstitute internally handles ValueTask returns
    private static IAsyncDataReader CreateMockReader(DataTable schemaTable, object[][] rows)
    {
        var reader = Substitute.For<IAsyncDataReader>();
        reader.FieldCount.Returns(schemaTable.Rows.Count);
        reader.GetSchemaTableAsync(Arg.Any<CancellationToken>()).Returns(new ValueTask<DataTable>(schemaTable));

        int currentRow = -1;
        reader.ReadAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            currentRow++;
            return new ValueTask<bool>(currentRow < rows.Length);
        });

        reader.GetValues(Arg.Any<object[]>()).Returns(callInfo =>
        {
            var values = callInfo.ArgAt<object[]>(0);
            var row = rows[currentRow];
            Array.Copy(row, values, Math.Min(row.Length, values.Length));
            return row.Length;
        });

        return reader;
    }
#pragma warning restore CA2012

    private static DataTable CreateSchemaTable(params (string Name, Type DataType)[] columns)
    {
        var schema = new DataTable("SchemaTable");
        schema.Columns.Add("ColumnName", typeof(string));
        schema.Columns.Add("DataType", typeof(Type));

        foreach (var (name, dataType) in columns)
        {
            schema.Rows.Add(name, dataType);
        }

        return schema;
    }

    [Fact]
    public async Task LoadAsync_Loads_Rows_With_Schema_Inference()
    {
        using var table = new AsyncDataTable("Products");
        var schema = CreateSchemaTable(("Id", typeof(int)), ("Name", typeof(string)));
        var rows = new[]
        {
            new object[] { 1, "Widget" },
            new object[] { 2, "Gadget" },
        };

        var reader = CreateMockReader(schema, rows);
        var count = await table.LoadAsync(reader);

        count.Should().Be(2);
        table.Columns.Count.Should().Be(2);
        table.Columns[0].ColumnName.Should().Be("Id");
        table.Columns[1].ColumnName.Should().Be("Name");
        table.Rows.Count.Should().Be(2);
        table.Rows[0]["Id"].Should().Be(1);
        table.Rows[0]["Name"].Should().Be("Widget");
        table.Rows[1]["Id"].Should().Be(2);
        table.Rows[1]["Name"].Should().Be("Gadget");
    }

    [Fact]
    public async Task LoadAsync_With_Existing_Columns_Does_Not_Duplicate_Them()
    {
        using var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var schema = CreateSchemaTable(("Id", typeof(int)), ("Name", typeof(string)));
        var rows = new[]
        {
            new object[] { 1, "Widget" },
        };

        var reader = CreateMockReader(schema, rows);
        var count = await table.LoadAsync(reader);

        count.Should().Be(1);
        table.Columns.Count.Should().Be(2);
        table.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_Returns_Correct_Count()
    {
        using var table = new AsyncDataTable("Items");
        var schema = CreateSchemaTable(("Value", typeof(int)));
        var rows = new[]
        {
            new object[] { 10 },
            new object[] { 20 },
            new object[] { 30 },
        };

        var reader = CreateMockReader(schema, rows);
        var count = await table.LoadAsync(reader);

        count.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_With_Empty_Reader_Returns_Zero()
    {
        using var table = new AsyncDataTable("Empty");
        var schema = CreateSchemaTable(("Id", typeof(int)));
        var rows = Array.Empty<object[]>();

        var reader = CreateMockReader(schema, rows);
        var count = await table.LoadAsync(reader);

        count.Should().Be(0);
        table.Columns.Count.Should().Be(1);
        table.Rows.Count.Should().Be(0);
    }
}

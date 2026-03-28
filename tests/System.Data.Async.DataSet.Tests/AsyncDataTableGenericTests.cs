using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataTableGenericTests
{
    private sealed class TestRow : AsyncDataRow
    {
        public TestRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
        public int Id => (int)this["Id"];
        public string Name => (string)this["Name"];
    }

    private sealed class TestTable : AsyncDataTable<TestRow>
    {
        public TestTable() : base("Test") { }
        protected override TestRow WrapRow(DataRow innerRow) => new(innerRow, this);
    }

    [Fact]
    public void NewRow_Returns_Typed_Row()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();

        row.Should().BeOfType<TestRow>();
    }

    [Fact]
    public void Rows_Returns_Typed_Collection()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));

        table.Rows.Should().BeOfType<AsyncDataRowCollection<TestRow>>();
    }

    [Fact]
    public async Task Rows_AddAsync_And_Indexer_Return_Typed_Rows()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        var row = table.NewRow();
        await row.SetValueAsync("Id", 1);
        await row.SetValueAsync("Name", "Alice");
        await table.Rows.AddAsync(row);

        table.Rows[0].Should().BeOfType<TestRow>();
        table.Rows[0].Id.Should().Be(1);
        table.Rows[0].Name.Should().Be("Alice");
    }

    [Fact]
    public void Indexer_Returns_Typed_Row()
    {
        using var table = new TestTable();
        table.Columns.Add("Id", typeof(int));

        // Add row via inner DataTable to test indexer
        DataTable dt = table; // implicit conversion
        dt.Rows.Add(42);

        table[0].Should().BeOfType<TestRow>();
        table[0].Id.Should().Be(42);
    }

    [Fact]
    public void Can_Cast_To_Untyped_AsyncDataTable()
    {
        using var table = new TestTable();

        AsyncDataTable untyped = table;
        untyped.Should().BeSameAs(table);
    }
}

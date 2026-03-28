using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncDataRowCollectionGenericTests
{
    private sealed class TestRow : AsyncDataRow
    {
        public TestRow(DataRow inner, AsyncDataTable table) : base(inner, table) { }
        public int Id => (int)this["Id"];
    }

    [Fact]
    public void Indexer_Returns_Typed_Row()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        DataTable dt = table;
        dt.Rows.Add(42);

        var collection = new AsyncDataRowCollection<TestRow>(
            dt.Rows, table, (inner, t) => new TestRow(inner, t));

        collection[0].Should().BeOfType<TestRow>();
        collection[0].Id.Should().Be(42);
    }

    [Fact]
    public void Enumeration_Returns_Typed_Rows()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        DataTable dt = table;
        dt.Rows.Add(1);
        dt.Rows.Add(2);

        var collection = new AsyncDataRowCollection<TestRow>(
            dt.Rows, table, (inner, t) => new TestRow(inner, t));

        ((IEnumerable<TestRow>)collection).Should().HaveCount(2);
        ((IEnumerable<TestRow>)collection).Should().AllBeOfType<TestRow>();
    }

    [Fact]
    public async Task AddAsync_Returns_Typed_Row_Via_Indexer()
    {
        using var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        DataTable dt = table;

        var collection = new AsyncDataRowCollection<TestRow>(
            dt.Rows, table, (inner, t) => new TestRow(inner, t));

        var innerRow = dt.NewRow();
        innerRow["Id"] = 99;
        var row = new TestRow(innerRow, table);
        await collection.AddAsync(row);

        collection[0].Id.Should().Be(99);
    }
}

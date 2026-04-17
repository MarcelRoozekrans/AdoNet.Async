using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class XmlSerializationBenchmarks : IDisposable
{
    private AsyncDataTable _table = null!;
    private string _xml = null!;

    [Params(10, 100)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _table = new AsyncDataTable("Orders");
        _table.Columns.Add("Id", typeof(int));
        _table.Columns.Add("Product", typeof(string));
        _table.Columns.Add("Quantity", typeof(int));
        _table.Columns.Add("Price", typeof(decimal));
        _table.Columns.Add("OrderDate", typeof(DateTime));

        var baseDate = DateTime.UtcNow;
        for (int i = 1; i <= RowCount; i++)
            await _table.Rows.AddAsync(new object?[] { i, $"Product{i}", i % 10 + 1, (decimal)(i * 9.99), baseDate.AddDays(-i) });
        await _table.AcceptChangesAsync();

        using var writer = new System.IO.StringWriter();
        await _table.WriteXmlAsync(writer);
        _xml = writer.ToString();
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Dispose();

    public void Dispose()
    {
        _table?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public async Task<string> WriteXml_Async()
    {
        using var writer = new System.IO.StringWriter();
        await _table.WriteXmlAsync(writer);
        return writer.ToString();
    }

    [Benchmark]
    public string WriteXml_Sync()
    {
        using var writer = new System.IO.StringWriter();
        ((System.Data.DataTable)_table).WriteXml(writer);
        return writer.ToString();
    }

    [Benchmark]
    public async Task<AsyncDataTable> ReadXml_Async()
    {
        var table = new AsyncDataTable();
        using var reader = new System.IO.StringReader(_xml);
        await table.ReadXmlAsync(reader);
        return table;
    }

    [Benchmark]
    public System.Data.DataTable ReadXml_Sync()
    {
        var table = new System.Data.DataTable();
        using var reader = new System.IO.StringReader(_xml);
        table.ReadXml(reader);
        return table;
    }
}

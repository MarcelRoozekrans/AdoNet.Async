using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class XmlSerializationBenchmarks : IDisposable
{
    private AsyncDataTable _table = null!;
    private byte[] _xmlBytes = null!;

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

        using var ms = new System.IO.MemoryStream();
        await _table.WriteXmlAsync(ms);
        _xmlBytes = ms.ToArray();
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Dispose();

    public void Dispose()
    {
        _table?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public async Task<byte[]> WriteXml_Async()
    {
        using var ms = new System.IO.MemoryStream();
        await _table.WriteXmlAsync(ms);
        return ms.ToArray();
    }

    [Benchmark]
    public byte[] WriteXml_Sync()
    {
        using var ms = new System.IO.MemoryStream();
        ((System.Data.DataTable)_table).WriteXml(ms);
        return ms.ToArray();
    }

    [Benchmark]
    public async Task ReadXml_Async()
    {
        using var table = new AsyncDataTable();
        using var ms = new System.IO.MemoryStream(_xmlBytes);
        await table.ReadXmlAsync(ms);
    }

    [Benchmark]
    public void ReadXml_Sync()
    {
        using var table = new System.Data.DataTable();
        using var ms = new System.IO.MemoryStream(_xmlBytes);
        table.ReadXml(ms);
    }
}

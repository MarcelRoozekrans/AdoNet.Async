using System.Data.Async.Adapters;
using System.Data.Async.Benchmarks.Infrastructure;
using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class DataAdapterBenchmarks : BenchmarkBase
{
    [Params(10, 100)]
    public int RowLimit { get; set; }

    [Benchmark(Baseline = true)]
    public DataTable Raw_Fill()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM Orders LIMIT {RowLimit}";
        var table = new DataTable("Orders");
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    [Benchmark]
    public async Task<AsyncDataTable> Async_Fill()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM Orders LIMIT {RowLimit}";
        var adapter = new AdapterDbDataAdapter(cmd);
        var table = new AsyncDataTable("Orders");
        await adapter.FillAsync(table);
        return table;
    }
}

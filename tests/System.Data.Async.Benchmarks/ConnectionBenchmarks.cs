using System.Data.Async.Adapters;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class ConnectionBenchmarks : IDisposable
{
    private const string ConnStr = "Data Source=ConnBench;Mode=Memory;Cache=Shared";
    private SqliteConnection _keepAlive = null!;

    [GlobalSetup]
    public void Setup()
    {
        _keepAlive = new SqliteConnection(ConnStr);
        _keepAlive.Open();
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAlive?.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public void Raw_OpenClose()
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        conn.Close();
    }

    [Benchmark]
    public async Task Async_OpenClose()
    {
        await using var conn = new SqliteConnection(ConnStr).AsAsync();
        await conn.OpenAsync();
        await conn.CloseAsync();
    }
}

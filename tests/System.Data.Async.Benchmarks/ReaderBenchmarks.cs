using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class ReaderBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public IList<string> Raw_ReadAll_Fields()
    {
        var results = new List<string>();
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(1));
        }
        return results;
    }

    [Benchmark]
    public async Task<IList<string>> Async_ReadAll_ManualLoop()
    {
        var results = new List<string>();
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(1));
        }
        return results;
    }

    [Benchmark]
    public async Task<IList<string>> Async_ReadAll_AwaitForeach()
    {
        var results = new List<string>();
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, Age, Balance FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        await foreach (var record in reader)
        {
            results.Add(record.GetString(1));
        }
        return results;
    }
}

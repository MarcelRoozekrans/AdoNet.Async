using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class CommandExecutionBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public object Raw_ExecuteScalar()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users";
        return cmd.ExecuteScalar()!;
    }

    [Benchmark]
    public async Task<object> Async_ExecuteScalar()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users";
        return (await cmd.ExecuteScalarAsync())!;
    }

    [Benchmark]
    public int Raw_ExecuteNonQuery()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        return cmd.ExecuteNonQuery();
    }

    [Benchmark]
    public async Task<int> Async_ExecuteNonQuery()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        return await cmd.ExecuteNonQueryAsync();
    }

    [Benchmark]
    public int Raw_ExecuteReader_Iterate()
    {
        using var cmd = RawConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";
        using var reader = cmd.ExecuteReader();
        int count = 0;
        while (reader.Read()) count++;
        return count;
    }

    [Benchmark]
    public async Task<int> Async_ExecuteReader_Iterate()
    {
        var cmd = AsyncConnection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        int count = 0;
        while (await reader.ReadAsync()) count++;
        return count;
    }
}

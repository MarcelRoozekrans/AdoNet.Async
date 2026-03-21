using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class TransactionBenchmarks : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public void Raw_BeginCommit()
    {
        using var tx = RawConnection.BeginTransaction();
        using var cmd = RawConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    [Benchmark]
    public async Task Async_BeginCommit()
    {
        await using var tx = await AsyncConnection.BeginTransactionAsync();
        var cmd = AsyncConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    [Benchmark]
    public void Raw_BeginRollback()
    {
        using var tx = RawConnection.BeginTransaction();
        using var cmd = RawConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        cmd.ExecuteNonQuery();
        tx.Rollback();
    }

    [Benchmark]
    public async Task Async_BeginRollback()
    {
        await using var tx = await AsyncConnection.BeginTransactionAsync();
        var cmd = AsyncConnection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE Users SET Name = Name WHERE Id = 1";
        await cmd.ExecuteNonQueryAsync();
        await tx.RollbackAsync();
    }
}

using System.Data.Async.Adapters;
using System.Data.Async.Adapters.Tests.Fakes;
using System.Data.Common;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

/// <summary>
/// The other benchmarks run against in-memory SQLite, whose async calls complete synchronously,
/// so they never exercise what a network provider does: return a task that completes later.
/// Here the provider's task always completes asynchronously, as a network round trip's would, and
/// each adapter call is measured against the same call made straight on the provider. The provider's
/// own task costs the same on both sides, so any difference is what the adapter adds.
/// </summary>
[MemoryDiagnoser]
public class AsyncCompletionBenchmarks : IDisposable
{
    private readonly PendingSlot _slot = new();
    private DbDataReader _rawReader = null!;
    private AdapterDbDataReader _adapterReader = null!;
    private DbCommand _rawCommand = null!;
    private AdapterDbCommand _adapterCommand = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _rawReader = new PendingDbDataReader(_slot);
        _adapterReader = new AdapterDbDataReader(_rawReader);
        _rawCommand = new PendingDbCommand(_slot);
        _adapterCommand = new AdapterDbCommand(_rawCommand, connection: null);
    }

    [Benchmark(Baseline = true)]
    public async Task<bool> Provider_ReadAsync()
    {
        _slot.Next = CompleteLater(true);
        return await _rawReader.ReadAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<bool> Adapter_ReadAsync()
    {
        _slot.Next = CompleteLater(true);
        return await _adapterReader.ReadAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> Provider_ExecuteNonQueryAsync()
    {
        _slot.Next = CompleteLater(1);
        return await _rawCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> Adapter_ExecuteNonQueryAsync()
    {
        _slot.Next = CompleteLater(1);
        return await _adapterCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _adapterReader.Dispose();
        _adapterCommand.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<T> CompleteLater<T>(T result)
    {
        await Task.Yield();
        return result;
    }
}

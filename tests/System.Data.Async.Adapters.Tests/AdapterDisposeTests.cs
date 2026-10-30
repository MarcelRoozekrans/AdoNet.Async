using System.Data.Async.Adapters.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

/// <summary>
/// Disposing an adapter has to dispose the provider object it wraps, whatever static type the
/// caller disposes it through. Generated data-access code disposes through the interfaces, so a
/// dispose that only reaches the concrete adapter type leaves the provider object undisposed.
/// Each async case runs twice: once with a provider whose dispose completes synchronously, and
/// once with one whose dispose yields, as a network provider's does.
/// </summary>
public class AdapterDisposeTests
{
    private const int Iterations = 200;

    public static TheoryData<string, bool> ReaderAsyncViews() => AsyncViews("adapter", "base class", "IAsyncDataReader", "interface");

    public static TheoryData<string, bool> ConnectionAsyncViews() => AsyncViews("adapter", "base class", "IAsyncDbConnection", "interface");

    public static TheoryData<string, bool> TransactionAsyncViews() => AsyncViews("adapter", "base class", "IAsyncDbTransaction", "interface");

    public static TheoryData<string> ReaderViews() => ["adapter", "base class", "IAsyncDataReader", "interface"];

    public static TheoryData<string> ConnectionViews() => ["adapter", "base class", "IAsyncDbConnection", "interface"];

    public static TheoryData<string> Kinds() => ["reader", "connection", "transaction"];

    private static TheoryData<string, bool> AsyncViews(params string[] views)
    {
        var data = new TheoryData<string, bool>();
        foreach (var view in views)
        {
            data.Add(view, false);
            data.Add(view, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ReaderAsyncViews))]
    public Task Reader_DisposeAsync_disposes_the_provider_reader(string view, bool providerYields)
    {
        var inner = new DisposeRecordingDbDataReader();
        var adapter = new AdapterDbDataReader(inner);

        return AssertDisposesProvider(
            pending => inner.PendingDispose = pending,
            () => view switch
            {
                "adapter" => adapter.DisposeAsync(),
                "base class" => ((AsyncDbDataReader)adapter).DisposeAsync(),
                "IAsyncDataReader" => ((IAsyncDataReader)adapter).DisposeAsync(),
                _ => ((IAsyncDisposable)adapter).DisposeAsync(),
            },
            () => inner.DisposeAsyncCalls,
            providerYields);
    }

    [Theory]
    [MemberData(nameof(ReaderViews))]
    public void Reader_Dispose_disposes_the_provider_reader(string view)
    {
        var inner = new DisposeRecordingDbDataReader();
        var adapter = new AdapterDbDataReader(inner);

        switch (view)
        {
            case "adapter": adapter.Dispose(); break;
            case "base class": ((AsyncDbDataReader)adapter).Dispose(); break;
            case "IAsyncDataReader": ((IAsyncDataReader)adapter).Dispose(); break;
            default: ((IDisposable)adapter).Dispose(); break;
        }

        inner.DisposeCalls.Should().Be(1);
        inner.DisposeAsyncCalls.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(ConnectionAsyncViews))]
    public Task Connection_DisposeAsync_disposes_the_provider_connection(string view, bool providerYields)
    {
        var inner = new DisposeRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);

        return AssertDisposesProvider(
            pending => inner.PendingDispose = pending,
            () => view switch
            {
                "adapter" => adapter.DisposeAsync(),
                "base class" => ((AsyncDbConnection)adapter).DisposeAsync(),
                "IAsyncDbConnection" => ((IAsyncDbConnection)adapter).DisposeAsync(),
                _ => ((IAsyncDisposable)adapter).DisposeAsync(),
            },
            () => inner.DisposeAsyncCalls,
            providerYields);
    }

    [Theory]
    [MemberData(nameof(ConnectionViews))]
    public void Connection_Dispose_disposes_the_provider_connection(string view)
    {
        var inner = new DisposeRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);

        switch (view)
        {
            case "adapter": adapter.Dispose(); break;
            case "base class": ((AsyncDbConnection)adapter).Dispose(); break;
            case "IAsyncDbConnection": ((IAsyncDbConnection)adapter).Dispose(); break;
            default: ((IDisposable)adapter).Dispose(); break;
        }

        inner.DisposeCalls.Should().Be(1);
        inner.DisposeAsyncCalls.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(TransactionAsyncViews))]
    public Task Transaction_DisposeAsync_disposes_the_provider_transaction(string view, bool providerYields)
    {
        var connection = new DisposeRecordingDbConnection();
        var inner = new DisposeRecordingDbTransaction(connection);
        var adapter = new AdapterDbTransaction(inner, new AdapterDbConnection(connection));

        return AssertDisposesProvider(
            pending => inner.PendingDispose = pending,
            () => view switch
            {
                "adapter" => adapter.DisposeAsync(),
                "base class" => ((AsyncDbTransaction)adapter).DisposeAsync(),
                "IAsyncDbTransaction" => ((IAsyncDbTransaction)adapter).DisposeAsync(),
                _ => ((IAsyncDisposable)adapter).DisposeAsync(),
            },
            () => inner.DisposeAsyncCalls,
            providerYields);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void DisposeAsync_over_a_yielding_provider_allocates_nothing_beyond_the_provider(string kind)
    {
        // Without a SynchronizationContext, a continuation the adapter registers on the
        // provider's task runs on this thread when the test completes that task, so everything
        // the adapter allocates for the dispose lands in this thread's counter. See
        // AsyncForwarderTests for the same setup.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        long bytesPerDispose;
        try
        {
            bytesPerDispose = MeasureBytesPerYieldingDispose(kind);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        bytesPerDispose.Should().Be(0, $"the {kind} adapter should hand the provider's dispose straight to the caller");
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task DisposeAsync_surfaces_a_synchronous_provider_throw_on_await(string kind)
    {
        var thrown = new InvalidOperationException("provider dispose threw synchronously");
        var adapter = CreateAdapter(kind, inner => inner.ThrowOnDisposeAsync = thrown);

        var pending = adapter.DisposeAsync();

        pending.IsFaulted.Should().BeTrue();
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        caught.Should().BeSameAs(thrown);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task DisposeAsync_surfaces_a_faulted_provider_dispose(string kind)
    {
        var thrown = new InvalidOperationException("provider dispose faulted");
        var adapter = CreateAdapter(kind, inner => inner.PendingDispose = Task.FromException(thrown));

        var pending = adapter.DisposeAsync();

        pending.IsFaulted.Should().BeTrue();
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        caught.Should().BeSameAs(thrown);
    }

    [Fact]
    public void Transaction_Dispose_waits_for_a_provider_dispose_that_is_not_backed_by_a_task()
    {
        // The base transaction's sync Dispose runs DisposeAsyncCore, which now hands back the
        // provider's own ValueTask. GetResult on a pending ValueTask over an IValueTaskSource
        // is not allowed, so the sync Dispose has to block on it some other way.
        var source = new ManualValueTaskSource();
        var connection = new DisposeRecordingDbConnection();
        var inner = new DisposeRecordingDbTransaction(connection) { PendingDisposeSource = source };
        var adapter = new AdapterDbTransaction(inner, new AdapterDbConnection(connection));
        using var completer = new Timer(_ => source.SetResult(), state: null, dueTime: 50, period: Timeout.Infinite);

        adapter.Dispose();

        inner.DisposeAsyncCalls.Should().Be(1);
    }

    private static async Task AssertDisposesProvider(
        Action<Task> setPendingDispose, Func<ValueTask> dispose, Func<int> disposeAsyncCalls, bool providerYields)
    {
        if (!providerYields)
        {
            await dispose();
            disposeAsyncCalls().Should().Be(1);
            return;
        }

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setPendingDispose(source.Task);

        var pending = dispose();

        disposeAsyncCalls().Should().Be(1);
        pending.IsCompleted.Should().BeFalse("the adapter's dispose completes with the provider's");
        source.SetResult();
        await pending;
    }

    private static long MeasureBytesPerYieldingDispose(string kind)
    {
        // Adapters, provider objects and the provider's pending tasks are all built up front, so
        // the measured window holds only the dispose calls and the provider completing.
        var cases = Enumerable.Range(0, Iterations * 2).Select(_ => YieldingDisposeCase.Create(kind)).ToArray();

        // First half warms up every code path, second half is measured.
        Run(cases.AsSpan(0, Iterations));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(cases.AsSpan(Iterations));
        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / Iterations;

        static void Run(ReadOnlySpan<YieldingDisposeCase> batch)
        {
            foreach (ref readonly var item in batch)
            {
                var pending = item.Adapter.DisposeAsync();
                if (pending.IsCompleted)
                {
                    throw new InvalidOperationException("The adapter's dispose did not wait for the provider's.");
                }

                item.Source.SetResult();
                if (!pending.IsCompletedSuccessfully)
                {
                    throw new InvalidOperationException("The adapter's dispose did not complete with the provider's.");
                }
            }
        }
    }

    private static IAsyncDisposable CreateAdapter(string kind, Action<IDisposeRecordingProvider> configure)
    {
        switch (kind)
        {
            case "reader":
                var reader = new DisposeRecordingDbDataReader();
                configure(reader);
                return new AdapterDbDataReader(reader);
            case "connection":
                var connection = new DisposeRecordingDbConnection();
                configure(connection);
                return new AdapterDbConnection(connection);
            default:
                var owner = new DisposeRecordingDbConnection();
                var transaction = new DisposeRecordingDbTransaction(owner);
                configure(transaction);
                return new AdapterDbTransaction(transaction, new AdapterDbConnection(owner));
        }
    }

    private readonly record struct YieldingDisposeCase(IAsyncDisposable Adapter, TaskCompletionSource Source)
    {
        public static YieldingDisposeCase Create(string kind)
        {
            var source = new TaskCompletionSource();
            return new YieldingDisposeCase(CreateAdapter(kind, inner => inner.PendingDispose = source.Task), source);
        }
    }
}

using System.Data.Async.Adapters.Tests.Fakes;
using System.Data.Common;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

/// <summary>
/// The adapters' reader and transaction factories wrap the provider's result in an adapter
/// object, so they await the provider's task instead of forwarding it. These tests drive them
/// with a provider whose tasks complete asynchronously and pin two things: a yielding call
/// allocates only the wrapper, and every outcome reaches the caller on await, the same as the
/// forwarders in <see cref="AsyncForwarderTests"/> deliver it.
/// </summary>
public class ResultWrappingCoreTests
{
    private const int Iterations = 200;

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in WrappingCase.All.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void An_asynchronously_completing_provider_call_allocates_only_the_wrapper(string name)
    {
        // See AsyncForwarderTests: without a SynchronizationContext the adapter's continuation
        // runs inline on this thread, so everything the call allocates lands in its counter.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        long bytesPerCall;
        long wrapperBytes;
        try
        {
            var wrappingCase = WrappingCase.All[name];
            wrapperBytes = wrappingCase.MeasureWrapperBytes(Iterations);
            bytesPerCall = wrappingCase.MeasureBytesPerAsyncCall(Iterations);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        wrapperBytes.Should().BePositive();
        bytesPerCall.Should().Be(wrapperBytes, $"{name} should allocate its adapter object and no state machine");
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Pending_provider_task_completes_with_a_wrapper_around_the_provider_result(string name)
        => WrappingCase.All[name].AssertPendingCompletes();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Synchronous_provider_throw_surfaces_on_await_not_on_call(string name)
        => WrappingCase.All[name].AssertSynchronousThrowSurfacesOnAwait();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Synchronous_provider_cancellation_yields_a_canceled_task_with_the_same_exception(string name)
        => WrappingCase.All[name].AssertSynchronousCancellationIsCanceled();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Faulted_provider_task_surfaces_the_same_exception(string name)
        => WrappingCase.All[name].AssertFaultedTaskSurfaces();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Canceled_provider_task_yields_a_canceled_task(string name)
        => WrappingCase.All[name].AssertCanceledTaskIsCanceled();
}

internal abstract class WrappingCase
{
    public static readonly Dictionary<string, WrappingCase> All = new WrappingCase[]
    {
        new WrappingCase<IAsyncDataReader, DbDataReader>(
            "Command.ExecuteReaderAsync",
            f => () => f.Command.ExecuteReaderAsync(),
            f => new PendingDbDataReader(f.Slot),
            (_, inner) => new AdapterDbDataReader(inner),
            wrapper => (DbDataReader)(AdapterDbDataReader)wrapper),
        new WrappingCase<IAsyncDataReader, DbDataReader>(
            "Batch.ExecuteReaderAsync",
            f => () => f.Batch.ExecuteReaderAsync(),
            f => new PendingDbDataReader(f.Slot),
            (_, inner) => new AdapterDbDataReader(inner),
            wrapper => (DbDataReader)(AdapterDbDataReader)wrapper),
        new WrappingCase<IAsyncDbTransaction, DbTransaction>(
            "Connection.BeginTransactionAsync",
            f => () => f.Connection.BeginTransactionAsync(),
            f => new PendingDbTransaction(f.Slot, new PendingDbConnection(f.Slot)),
            (f, inner) => new AdapterDbTransaction(inner, f.Connection),
            wrapper => (DbTransaction)(AdapterDbTransaction)wrapper),
    }.ToDictionary(c => c.Name, StringComparer.Ordinal);

    protected WrappingCase(string name) => Name = name;

    public string Name { get; }

    public abstract long MeasureWrapperBytes(int iterations);
    public abstract long MeasureBytesPerAsyncCall(int iterations);
    public abstract Task AssertPendingCompletes();
    public abstract Task AssertSynchronousThrowSurfacesOnAwait();
    public abstract Task AssertSynchronousCancellationIsCanceled();
    public abstract Task AssertFaultedTaskSurfaces();
    public abstract Task AssertCanceledTaskIsCanceled();
}

internal sealed class WrappingCase<TResult, TInner>(
    string name,
    Func<Fixture, Func<ValueTask<TResult>>> bind,
    Func<Fixture, TInner> createInner,
    Func<Fixture, TInner, TResult> wrap,
    Func<TResult, TInner> unwrap) : WrappingCase(name)
    where TResult : class
    where TInner : class
{
    public override long MeasureWrapperBytes(int iterations)
    {
        var fixture = new Fixture();
        var inner = createInner(fixture);
        var wrappers = new TResult[iterations];

        var before = GC.GetAllocatedBytesForCurrentThread();
        foreach (ref var wrapper in wrappers.AsSpan())
        {
            wrapper = wrap(fixture, inner);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(wrappers);
        return (after - before) / iterations;
    }

    public override long MeasureBytesPerAsyncCall(int iterations)
    {
        var fixture = new Fixture();
        var call = bind(fixture);
        var inner = createInner(fixture);
        var sources = Enumerable.Range(0, iterations * 2).Select(_ => new TaskCompletionSource<TInner>()).ToArray();

        // First half warms up every code path, second half is measured.
        RunPending(fixture, call, inner, sources.AsSpan(0, iterations));
        var before = GC.GetAllocatedBytesForCurrentThread();
        RunPending(fixture, call, inner, sources.AsSpan(iterations));
        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / iterations;
    }

    private void RunPending(Fixture fixture, Func<ValueTask<TResult>> call, TInner inner, ReadOnlySpan<TaskCompletionSource<TInner>> sources)
    {
        foreach (ref readonly var source in sources)
        {
            fixture.Slot.Next = source.Task;
            var pending = call();
            source.SetResult(inner);
            if (!pending.IsCompleted)
            {
                throw new InvalidOperationException("Wrapping task did not complete with the provider's.");
            }

            if (!ReferenceEquals(unwrap(pending.Result), inner))
            {
                throw new InvalidOperationException("Wrapper does not wrap the provider's result.");
            }
        }
    }

    public override async Task AssertPendingCompletes()
    {
        var fixture = new Fixture();
        var inner = createInner(fixture);
        var source = new TaskCompletionSource<TInner>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Slot.Next = source.Task;

        var pending = bind(fixture)();

        pending.IsCompleted.Should().BeFalse();
        source.SetResult(inner);
        unwrap(await pending).Should().BeSameAs(inner);
    }

    public override async Task AssertSynchronousThrowSurfacesOnAwait()
    {
        var fixture = new Fixture();
        var thrown = new InvalidOperationException("provider threw synchronously");
        fixture.Slot.ThrowOnCall = thrown;

        var pending = bind(fixture)();

        pending.IsFaulted.Should().BeTrue();
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        caught.Should().BeSameAs(thrown);
    }

    public override async Task AssertSynchronousCancellationIsCanceled()
    {
        var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var thrown = new OperationCanceledException(cts.Token);
        fixture.Slot.ThrowOnCall = thrown;

        var pending = bind(fixture)();

        pending.IsCanceled.Should().BeTrue();
        var caught = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        caught.Should().BeSameAs(thrown);
    }

    public override async Task AssertFaultedTaskSurfaces()
    {
        var fixture = new Fixture();
        var thrown = new InvalidOperationException("provider task faulted");
        fixture.Slot.Next = Task.FromException<TInner>(thrown);

        var pending = bind(fixture)();

        pending.IsFaulted.Should().BeTrue();
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        caught.Should().BeSameAs(thrown);
    }

    public override async Task AssertCanceledTaskIsCanceled()
    {
        var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        fixture.Slot.Next = Task.FromCanceled<TInner>(cts.Token);

        var pending = bind(fixture)();

        pending.IsCanceled.Should().BeTrue();
        var caught = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        caught.CancellationToken.Should().Be(cts.Token);
    }
}

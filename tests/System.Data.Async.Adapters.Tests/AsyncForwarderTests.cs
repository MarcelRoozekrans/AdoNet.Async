using System.Data.Async.Adapters.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

/// <summary>
/// The adapters' async members forward a provider's <see cref="Task"/> as a
/// <see cref="ValueTask"/>. These tests drive them with a provider whose tasks complete
/// asynchronously, as a network database's do, and pin two things: forwarding allocates nothing
/// of its own, and every outcome, including a provider that throws synchronously, reaches the
/// caller on await exactly as an <c>async</c> method would deliver it.
/// </summary>
public class AsyncForwarderTests
{
    private const int Iterations = 200;

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in ForwarderCase.All.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Forwarding_an_asynchronously_completing_provider_call_allocates_nothing(string name)
    {
        // xUnit installs a SynchronizationContext, under which the runtime will not run a
        // completing task's continuations inline. Without one, any continuation the adapter
        // registers on the provider's task runs on this thread when the test completes that task,
        // so everything the adapter allocates for the call lands in this thread's counter.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        long bytesPerCall;
        try
        {
            bytesPerCall = ForwarderCase.All[name].MeasureBytesPerAsyncCall(Iterations);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        bytesPerCall.Should().Be(0, $"{name} should hand the provider's task straight to the caller");
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Pending_provider_task_is_forwarded_and_completes_with_the_provider(string name)
        => ForwarderCase.All[name].AssertPendingCompletes();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Synchronous_provider_throw_surfaces_on_await_not_on_call(string name)
        => ForwarderCase.All[name].AssertSynchronousThrowSurfacesOnAwait();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Synchronous_provider_cancellation_yields_a_canceled_task_with_the_same_exception(string name)
        => ForwarderCase.All[name].AssertSynchronousCancellationIsCanceled();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Faulted_provider_task_surfaces_the_same_exception(string name)
        => ForwarderCase.All[name].AssertFaultedTaskSurfaces();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Canceled_provider_task_yields_a_canceled_task(string name)
        => ForwarderCase.All[name].AssertCanceledTaskIsCanceled();
}

internal sealed class Fixture
{
    public Fixture()
    {
        Slot = new PendingSlot();
        var connection = new PendingDbConnection(Slot);
        Connection = new AdapterDbConnection(connection);
        Command = new AdapterDbCommand(new PendingDbCommand(Slot), Connection);
        Reader = new AdapterDbDataReader(new PendingDbDataReader(Slot));
        Transaction = new AdapterDbTransaction(new PendingDbTransaction(Slot, connection), Connection);
        Batch = new AdapterDbBatch(new PendingDbBatch(Slot), Connection);
    }

    public PendingSlot Slot { get; }
    public AdapterDbConnection Connection { get; }
    public AdapterDbCommand Command { get; }
    public AdapterDbDataReader Reader { get; }
    public AdapterDbTransaction Transaction { get; }
    public AdapterDbBatch Batch { get; }
}

internal abstract class ForwarderCase
{
    public static readonly Dictionary<string, ForwarderCase> All = new ForwarderCase[]
    {
        new ResultCase<bool>("Reader.ReadAsync", f => () => f.Reader.ReadAsync(), true),
        new ResultCase<bool>("Reader.NextResultAsync", f => () => f.Reader.NextResultAsync(), true),
        new VoidCase("Reader.CloseAsync", f => () => f.Reader.CloseAsync()),
        new ResultCase<bool>("Reader.IsDBNullAsync", f => () => f.Reader.IsDBNullAsync(0), true),
        new ResultCase<int>("Reader.GetFieldValueAsync", f => () => f.Reader.GetFieldValueAsync<int>(0), 42),
        new ResultCase<int>("Command.ExecuteNonQueryAsync", f => () => f.Command.ExecuteNonQueryAsync(), 3),
        new ResultCase<object?>("Command.ExecuteScalarAsync", f => () => f.Command.ExecuteScalarAsync(), "scalar"),
        new VoidCase("Command.PrepareAsync", f => () => f.Command.PrepareAsync()),
        new VoidCase("Connection.OpenAsync", f => () => f.Connection.OpenAsync()),
        new VoidCase("Connection.CloseAsync", f => () => f.Connection.CloseAsync()),
        new VoidCase("Connection.ChangeDatabaseAsync", f => () => f.Connection.ChangeDatabaseAsync("other")),
        new VoidCase("Transaction.CommitAsync", f => () => f.Transaction.CommitAsync()),
        new VoidCase("Transaction.RollbackAsync", f => () => f.Transaction.RollbackAsync()),
        new ResultCase<int>("Batch.ExecuteNonQueryAsync", f => () => f.Batch.ExecuteNonQueryAsync(), 7),
        new ResultCase<object?>("Batch.ExecuteScalarAsync", f => () => f.Batch.ExecuteScalarAsync(), "scalar"),
        new VoidCase("Batch.PrepareAsync", f => () => f.Batch.PrepareAsync()),
    }.ToDictionary(c => c.Name, StringComparer.Ordinal);

    protected ForwarderCase(string name) => Name = name;

    public string Name { get; }

    public abstract long MeasureBytesPerAsyncCall(int iterations);
    public abstract Task AssertPendingCompletes();
    public abstract Task AssertSynchronousThrowSurfacesOnAwait();
    public abstract Task AssertSynchronousCancellationIsCanceled();
    public abstract Task AssertFaultedTaskSurfaces();
    public abstract Task AssertCanceledTaskIsCanceled();
}

internal sealed class ResultCase<T>(string name, Func<Fixture, Func<ValueTask<T>>> bind, T result) : ForwarderCase(name)
{
    public override long MeasureBytesPerAsyncCall(int iterations)
    {
        var fixture = new Fixture();
        var call = bind(fixture);
        var sources = Enumerable.Range(0, iterations * 2).Select(_ => new TaskCompletionSource<T>()).ToArray();

        // First half warms up every code path, second half is measured.
        RunPending(fixture, call, sources.AsSpan(0, iterations));
        var before = GC.GetAllocatedBytesForCurrentThread();
        RunPending(fixture, call, sources.AsSpan(iterations));
        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / iterations;
    }

    private void RunPending(Fixture fixture, Func<ValueTask<T>> call, ReadOnlySpan<TaskCompletionSource<T>> sources)
    {
        foreach (ref readonly var source in sources)
        {
            fixture.Slot.Next = source.Task;
            var pending = call();
            source.SetResult(result);
            if (!pending.IsCompleted)
            {
                throw new InvalidOperationException("Forwarded task did not complete with the provider's.");
            }

            if (!EqualityComparer<T>.Default.Equals(pending.Result, result))
            {
                throw new InvalidOperationException("Forwarded result differs from the provider's.");
            }
        }
    }

    public override async Task AssertPendingCompletes()
    {
        var fixture = new Fixture();
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Slot.Next = source.Task;

        var pending = bind(fixture)();

        pending.IsCompleted.Should().BeFalse();
        source.SetResult(result);
        (await pending).Should().Be(result);
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
        fixture.Slot.Next = Task.FromException<T>(thrown);

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
        fixture.Slot.Next = Task.FromCanceled<T>(cts.Token);

        var pending = bind(fixture)();

        pending.IsCanceled.Should().BeTrue();
        var caught = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        caught.CancellationToken.Should().Be(cts.Token);
    }
}

internal sealed class VoidCase(string name, Func<Fixture, Func<ValueTask>> bind) : ForwarderCase(name)
{
    public override long MeasureBytesPerAsyncCall(int iterations)
    {
        var fixture = new Fixture();
        var call = bind(fixture);
        var sources = Enumerable.Range(0, iterations * 2).Select(_ => new TaskCompletionSource()).ToArray();

        RunPending(fixture, call, sources.AsSpan(0, iterations));
        var before = GC.GetAllocatedBytesForCurrentThread();
        RunPending(fixture, call, sources.AsSpan(iterations));
        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / iterations;
    }

    private static void RunPending(Fixture fixture, Func<ValueTask> call, ReadOnlySpan<TaskCompletionSource> sources)
    {
        foreach (ref readonly var source in sources)
        {
            fixture.Slot.Next = source.Task;
            var pending = call();
            source.SetResult();
            if (!pending.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException("Forwarded task did not complete with the provider's.");
            }
        }
    }

    public override async Task AssertPendingCompletes()
    {
        var fixture = new Fixture();
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Slot.Next = source.Task;

        var pending = bind(fixture)();

        pending.IsCompleted.Should().BeFalse();
        source.SetResult();
        await pending;
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
        fixture.Slot.Next = Task.FromException(thrown);

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
        fixture.Slot.Next = Task.FromCanceled(cts.Token);

        var pending = bind(fixture)();

        pending.IsCanceled.Should().BeTrue();
        var caught = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        caught.CancellationToken.Should().Be(cts.Token);
    }
}

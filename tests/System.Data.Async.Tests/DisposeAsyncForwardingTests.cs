#pragma warning disable CA1065 // Members the tests never call throw NotSupportedException by design
#pragma warning disable MA0025 // NotSupportedException is the intended signal for members the tests never call

using FluentAssertions;
using Xunit;

namespace System.Data.Async.Tests;

/// <summary>
/// The base classes' DisposeAsync hands DisposeAsyncCore's task straight to the caller. These
/// tests drive it with a core that completes asynchronously, through the default core where
/// that closes the object, and pin that the base class adds no
/// allocation of its own and still completes with the core.
/// </summary>
public class DisposeAsyncForwardingTests
{
    private const int Iterations = 200;

    public static TheoryData<string> Kinds() => ["reader", "connection", "transaction"];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void DisposeAsync_forwards_an_asynchronously_completing_core_without_allocating(string kind)
    {
        // Without a SynchronizationContext, a continuation registered on the core's task would
        // run on this thread when the test completes that task, so it would land in the counter.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        long bytesPerCall;
        try
        {
            var sources = Enumerable.Range(0, Iterations * 2).Select(_ => new TaskCompletionSource()).ToArray();
            var disposables = sources.Select(s => Create(kind, s.Task)).ToArray();

            Run(disposables, sources, 0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Run(disposables, sources, Iterations);
            bytesPerCall = (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        bytesPerCall.Should().Be(0, $"the {kind} base class should hand the core's task straight to the caller");
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task DisposeAsync_completes_with_the_core(string kind)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Create(kind, source.Task).DisposeAsync();

        pending.IsCompleted.Should().BeFalse();
        source.SetResult();
        await pending;
    }

    private static void Run(IAsyncDisposable[] disposables, TaskCompletionSource[] sources, int from)
    {
        for (var i = from; i < from + Iterations; i++)
        {
            var pending = disposables[i].DisposeAsync();
            sources[i].SetResult();
            if (!pending.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException("DisposeAsync did not complete with its core.");
            }
        }
    }

    private static IAsyncDisposable Create(string kind, Task core) => kind switch
    {
        "reader" => new CoreReader(core),
        "connection" => new CoreConnection(core),
        _ => new CoreTransaction(core),
    };

    private sealed class CoreTransaction(Task core) : AsyncDbTransaction
    {
        public override IAsyncDbConnection Connection => throw new NotSupportedException();
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override ValueTask CommitCoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override ValueTask RollbackCoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        protected override ValueTask DisposeAsyncCore()
        {
            // The base core has nothing to release and completes synchronously.
            var baseCore = base.DisposeAsyncCore();
            return baseCore.IsCompletedSuccessfully ? new ValueTask(core) : baseCore;
        }
    }

    private sealed class CoreConnection(Task core) : AsyncDbConnection
    {
        public override string ConnectionString { get => ""; set => throw new NotSupportedException(); }
        public override string Database => "";
        public override ConnectionState State => ConnectionState.Open;
        public override int ConnectionTimeout => 0;

        protected override ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override ValueTask OpenCoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override ValueTask ChangeDatabaseCoreAsync(string databaseName, CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override IAsyncDbCommand CreateDbCommand() => throw new NotSupportedException();

        // The default DisposeAsyncCore closes an open connection; that close is the pending core.
        protected override ValueTask CloseCoreAsync() => new(core);
    }

    private sealed class CoreReader(Task core) : AsyncDbDataReader
    {
        public override int FieldCount => 0;
        public override int Depth => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool HasRows => false;
        public override object this[int i] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();

        public override bool GetBoolean(int i) => throw new NotSupportedException();
        public override byte GetByte(int i) => throw new NotSupportedException();
        public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int i) => throw new NotSupportedException();
        public override long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override Guid GetGuid(int i) => throw new NotSupportedException();
        public override short GetInt16(int i) => throw new NotSupportedException();
        public override int GetInt32(int i) => throw new NotSupportedException();
        public override long GetInt64(int i) => throw new NotSupportedException();
        public override float GetFloat(int i) => throw new NotSupportedException();
        public override double GetDouble(int i) => throw new NotSupportedException();
        public override string GetString(int i) => throw new NotSupportedException();
        public override decimal GetDecimal(int i) => throw new NotSupportedException();
        public override DateTime GetDateTime(int i) => throw new NotSupportedException();
        public override IDataReader GetData(int i) => throw new NotSupportedException();
        public override string GetDataTypeName(int i) => throw new NotSupportedException();
        public override Type GetFieldType(int i) => throw new NotSupportedException();
        public override string GetName(int i) => throw new NotSupportedException();
        public override int GetOrdinal(string name) => throw new NotSupportedException();
        public override object GetValue(int i) => throw new NotSupportedException();
        public override int GetValues(object[] values) => throw new NotSupportedException();
        public override bool IsDBNull(int i) => throw new NotSupportedException();
        public override DataTable GetSchemaTable() => throw new NotSupportedException();

        protected override ValueTask<bool> ReadCoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override ValueTask<bool> NextResultCoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        // The default DisposeAsyncCore closes an open reader; that close is the pending core.
        protected override ValueTask CloseCoreAsync() => new(core);
    }
}

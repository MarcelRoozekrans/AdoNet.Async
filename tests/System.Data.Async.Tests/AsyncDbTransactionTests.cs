using FluentAssertions;
using Xunit;

namespace System.Data.Async.Tests;

public class AsyncDbTransactionTests
{
    private sealed class TestDbTransaction : AsyncDbTransaction
    {
        public override IAsyncDbConnection Connection { get; }
        public override IsolationLevel IsolationLevel { get; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public int DisposeCount { get; private set; }

        public TestDbTransaction(IAsyncDbConnection connection, IsolationLevel isolationLevel)
        {
            Connection = connection;
            IsolationLevel = isolationLevel;
        }

        protected override ValueTask CommitCoreAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return default;
        }

        protected override ValueTask RollbackCoreAsync(CancellationToken cancellationToken)
        {
            RolledBack = true;
            return default;
        }

        protected override ValueTask DisposeAsyncCore()
        {
            DisposeCount++;
            return base.DisposeAsyncCore();
        }
    }

    private static TestDbTransaction CreateTransaction()
        => new(NSubstitute.Substitute.For<IAsyncDbConnection>(), IsolationLevel.ReadCommitted);

    [Fact]
    public async Task CommitAsync_Commits()
    {
        var tx = CreateTransaction();

        await tx.CommitAsync();

        tx.Committed.Should().BeTrue();
    }

    [Fact]
    public void Commit_Sync_Commits()
    {
        var tx = CreateTransaction();

        tx.Commit();

        tx.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackAsync_Rolls_Back()
    {
        var tx = CreateTransaction();

        await tx.RollbackAsync();

        tx.RolledBack.Should().BeTrue();
    }

    [Fact]
    public void Rollback_Sync_Rolls_Back()
    {
        var tx = CreateTransaction();

        tx.Rollback();

        tx.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_Disposes_Only_Once()
    {
        var tx = CreateTransaction();

        await tx.DisposeAsync();
        await tx.DisposeAsync();

        tx.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_Disposes_Only_Once()
    {
        var tx = CreateTransaction();

        tx.Dispose();
        tx.Dispose();

        tx.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void IsolationLevel_Returns_Configured_Value()
    {
        var tx = CreateTransaction();

        tx.IsolationLevel.Should().Be(IsolationLevel.ReadCommitted);
    }

    [Fact]
    public void Implements_IAsyncDbTransaction()
    {
        typeof(IAsyncDbTransaction).IsAssignableFrom(typeof(AsyncDbTransaction)).Should().BeTrue();
    }
}

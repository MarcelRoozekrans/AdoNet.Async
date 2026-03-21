using FluentAssertions;
using NSubstitute;
using Xunit;

namespace System.Data.Async.Tests;

public class AsyncDbCommandTests
{
    private sealed class TestDbCommand : AsyncDbCommand
    {
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override IAsyncDbConnection? Connection { get; set; }
        public override IAsyncDbTransaction? Transaction { get; set; }
        public override IDataParameterCollection Parameters { get; } = Substitute.For<IDataParameterCollection>();
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;
        public override IDbDataParameter CreateParameter() => Substitute.For<IDbDataParameter>();
        public override void Cancel() { }

        public int NonQueryResult { get; set; } = 42;
        public object? ScalarResult { get; set; } = "scalar";
        public bool Prepared { get; private set; }
        public CommandBehavior? LastBehavior { get; private set; }
        public bool Disposed { get; private set; }

        protected override ValueTask<IAsyncDataReader> ExecuteDbReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            LastBehavior = behavior;
            return new(Substitute.For<IAsyncDataReader>());
        }

        protected override ValueTask<int> ExecuteNonQueryCoreAsync(CancellationToken cancellationToken)
            => new(NonQueryResult);

        protected override ValueTask<object?> ExecuteScalarCoreAsync(CancellationToken cancellationToken)
            => new(ScalarResult);

        protected override ValueTask PrepareCoreAsync(CancellationToken cancellationToken)
        {
            Prepared = true;
            return default;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task ExecuteReaderAsync_Returns_Reader()
    {
        var cmd = new TestDbCommand();

        IAsyncDataReader reader = await cmd.ExecuteReaderAsync();

        reader.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteReaderAsync_With_Behavior_Passes_Behavior()
    {
        var cmd = new TestDbCommand();

        await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        cmd.LastBehavior.Should().Be(CommandBehavior.CloseConnection);
    }

    [Fact]
    public void ExecuteReader_Sync_Returns_Reader()
    {
        var cmd = new TestDbCommand();

        IAsyncDataReader reader = cmd.ExecuteReader();

        reader.Should().NotBeNull();
        cmd.LastBehavior.Should().Be(CommandBehavior.Default);
    }

    [Fact]
    public void ExecuteReader_Sync_With_Behavior()
    {
        var cmd = new TestDbCommand();

        cmd.ExecuteReader(CommandBehavior.SchemaOnly);

        cmd.LastBehavior.Should().Be(CommandBehavior.SchemaOnly);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_Returns_Result()
    {
        var cmd = new TestDbCommand { NonQueryResult = 7 };

        int result = await cmd.ExecuteNonQueryAsync();

        result.Should().Be(7);
    }

    [Fact]
    public void ExecuteNonQuery_Sync_Returns_Result()
    {
        var cmd = new TestDbCommand { NonQueryResult = 7 };

        cmd.ExecuteNonQuery().Should().Be(7);
    }

    [Fact]
    public async Task ExecuteScalarAsync_Returns_Result()
    {
        var cmd = new TestDbCommand { ScalarResult = "hello" };

        object? result = await cmd.ExecuteScalarAsync();

        result.Should().Be("hello");
    }

    [Fact]
    public void ExecuteScalar_Sync_Returns_Result()
    {
        var cmd = new TestDbCommand { ScalarResult = "hello" };

        cmd.ExecuteScalar().Should().Be("hello");
    }

    [Fact]
    public async Task PrepareAsync_Prepares()
    {
        var cmd = new TestDbCommand();

        await cmd.PrepareAsync();

        cmd.Prepared.Should().BeTrue();
    }

    [Fact]
    public void Prepare_Sync_Prepares()
    {
        var cmd = new TestDbCommand();

        cmd.Prepare();

        cmd.Prepared.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_Disposes_Only_Once()
    {
        var cmd = new TestDbCommand();

        await cmd.DisposeAsync();
        cmd.Disposed.Should().BeTrue();

        await cmd.DisposeAsync();
    }

    [Fact]
    public void Dispose_Disposes_Only_Once()
    {
        var cmd = new TestDbCommand();

        cmd.Dispose();
        cmd.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Implements_IAsyncDbCommand()
    {
        typeof(IAsyncDbCommand).IsAssignableFrom(typeof(AsyncDbCommand)).Should().BeTrue();
    }
}

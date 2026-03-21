using FluentAssertions;
using NSubstitute;
using Xunit;

namespace System.Data.Async.Tests;

public class AsyncDbConnectionTests
{
    private sealed class TestDbConnection : AsyncDbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        private string _connectionString = string.Empty;
        private string _database = "TestDb";

        public override string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value;
        }

        public override string Database => _database;
        public override ConnectionState State => _state;
        public override int ConnectionTimeout => 30;

        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public IsolationLevel? LastIsolationLevel { get; private set; }
        public string? LastDatabaseChanged { get; private set; }

        protected override ValueTask<IAsyncDbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            LastIsolationLevel = isolationLevel;
            return new(Substitute.For<IAsyncDbTransaction>());
        }

        protected override ValueTask OpenCoreAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            OpenCount++;
            return default;
        }

        protected override ValueTask CloseCoreAsync()
        {
            _state = ConnectionState.Closed;
            CloseCount++;
            return default;
        }

        protected override ValueTask ChangeDatabaseCoreAsync(string databaseName, CancellationToken cancellationToken)
        {
            _database = databaseName;
            LastDatabaseChanged = databaseName;
            return default;
        }

        protected override IAsyncDbCommand CreateDbCommand() => Substitute.For<IAsyncDbCommand>();
    }

    [Fact]
    public async Task OpenAsync_Opens_Connection()
    {
        var conn = new TestDbConnection();

        await conn.OpenAsync();

        conn.State.Should().Be(ConnectionState.Open);
        conn.OpenCount.Should().Be(1);
    }

    [Fact]
    public void Open_Sync_Opens_Connection()
    {
        var conn = new TestDbConnection();

        conn.Open();

        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task CloseAsync_Closes_Connection()
    {
        var conn = new TestDbConnection();
        await conn.OpenAsync();

        await conn.CloseAsync();

        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void Close_Sync_Closes_Connection()
    {
        var conn = new TestDbConnection();
        conn.Open();

        conn.Close();

        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task BeginTransactionAsync_Returns_Transaction()
    {
        var conn = new TestDbConnection();

        IAsyncDbTransaction tx = await conn.BeginTransactionAsync();

        tx.Should().NotBeNull();
        conn.LastIsolationLevel.Should().Be(IsolationLevel.Unspecified);
    }

    [Fact]
    public async Task BeginTransactionAsync_With_IsolationLevel()
    {
        var conn = new TestDbConnection();

        await conn.BeginTransactionAsync(IsolationLevel.Serializable);

        conn.LastIsolationLevel.Should().Be(IsolationLevel.Serializable);
    }

    [Fact]
    public void BeginTransaction_Sync_Returns_Transaction()
    {
        var conn = new TestDbConnection();

        IAsyncDbTransaction tx = conn.BeginTransaction();

        tx.Should().NotBeNull();
    }

    [Fact]
    public void BeginTransaction_Sync_With_IsolationLevel()
    {
        var conn = new TestDbConnection();

        conn.BeginTransaction(IsolationLevel.RepeatableRead);

        conn.LastIsolationLevel.Should().Be(IsolationLevel.RepeatableRead);
    }

    [Fact]
    public async Task ChangeDatabaseAsync_Changes_Database()
    {
        var conn = new TestDbConnection();

        await conn.ChangeDatabaseAsync("NewDb");

        conn.Database.Should().Be("NewDb");
    }

    [Fact]
    public void ChangeDatabase_Sync_Changes_Database()
    {
        var conn = new TestDbConnection();

        conn.ChangeDatabase("NewDb");

        conn.Database.Should().Be("NewDb");
    }

    [Fact]
    public void CreateCommand_Returns_Command()
    {
        var conn = new TestDbConnection();

        IAsyncDbCommand cmd = conn.CreateCommand();

        cmd.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_Closes_Open_Connection()
    {
        var conn = new TestDbConnection();
        await conn.OpenAsync();

        await conn.DisposeAsync();

        conn.State.Should().Be(ConnectionState.Closed);
        conn.CloseCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_Does_Not_Close_If_Already_Closed()
    {
        var conn = new TestDbConnection();

        await conn.DisposeAsync();

        conn.CloseCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_Closes_Open_Connection()
    {
        var conn = new TestDbConnection();
        conn.Open();

        conn.Dispose();

        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void Dispose_Does_Not_Close_If_Already_Closed()
    {
        var conn = new TestDbConnection();

        conn.Dispose();

        conn.CloseCount.Should().Be(0);
    }

    [Fact]
    public void Implements_IAsyncDbConnection()
    {
        typeof(IAsyncDbConnection).IsAssignableFrom(typeof(AsyncDbConnection)).Should().BeTrue();
    }
}

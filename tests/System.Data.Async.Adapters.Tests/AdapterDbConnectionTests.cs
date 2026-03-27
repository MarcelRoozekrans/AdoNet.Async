using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbConnectionTests
{
    [Fact]
    public async Task OpenAsync_Opens_Connection()
    {
        await using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);

        await conn.OpenAsync();

        conn.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task CloseAsync_Closes_Connection()
    {
        await using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);
        await conn.OpenAsync();

        await conn.CloseAsync();

        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task CreateCommand_Returns_AdapterDbCommand()
    {
        await using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);
        await conn.OpenAsync();

        IAsyncDbCommand cmd = conn.CreateCommand();

        cmd.Should().BeOfType<AdapterDbCommand>();
    }

    [Fact]
    public async Task BeginTransactionAsync_Returns_AdapterDbTransaction()
    {
        await using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);
        await conn.OpenAsync();

        IAsyncDbTransaction tx = await conn.BeginTransactionAsync();

        tx.Should().BeOfType<AdapterDbTransaction>();
        await tx.RollbackAsync();
    }

    [Fact]
    public void AsAsync_Extension_Wraps_DbConnection()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");

        IAsyncDbConnection conn = inner.AsAsync();

        conn.Should().BeOfType<AdapterDbConnection>();
        ((DbConnection)(AdapterDbConnection)conn).Should().BeSameAs(inner);
    }

    [Fact]
    public void InnerConnection_Returns_Wrapped_Connection()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);

        ((DbConnection)conn).Should().BeSameAs(inner);
    }

    [Fact]
    public void ConnectionString_Delegates_To_Inner()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);

        conn.ConnectionString.Should().Be("Data Source=:memory:");
    }

    [Fact]
    public void Sync_Open_Delegates_To_Inner()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        var conn = new AdapterDbConnection(inner);

        conn.Open();

        conn.State.Should().Be(ConnectionState.Open);
    }
}

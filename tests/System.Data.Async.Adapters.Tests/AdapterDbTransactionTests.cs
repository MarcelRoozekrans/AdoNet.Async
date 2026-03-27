#pragma warning disable CA1001 // Type owns disposable field(s) -- disposed via IAsyncLifetime

using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbTransactionTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT)";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CommitAsync_Persists_Data()
    {
        var adapterConn = new AdapterDbConnection(_connection);
        DbTransaction innerTx = await _connection.BeginTransactionAsync();
        var tx = new AdapterDbTransaction(innerTx, adapterConn);

        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = (SqliteTransaction)innerTx;
        cmd.CommandText = "INSERT INTO TestTable (Id, Name) VALUES (1, 'Alice')";
        await cmd.ExecuteNonQueryAsync();

        await tx.CommitAsync();

        // Verify data persists
        await using var verifyCmd = _connection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM TestTable";
        long count = (long)(await verifyCmd.ExecuteScalarAsync())!;
        count.Should().Be(1);
    }

    [Fact]
    public async Task RollbackAsync_Reverts_Data()
    {
        var adapterConn = new AdapterDbConnection(_connection);
        DbTransaction innerTx = await _connection.BeginTransactionAsync();
        var tx = new AdapterDbTransaction(innerTx, adapterConn);

        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = (SqliteTransaction)innerTx;
        cmd.CommandText = "INSERT INTO TestTable (Id, Name) VALUES (1, 'Alice')";
        await cmd.ExecuteNonQueryAsync();

        await tx.RollbackAsync();

        // Verify data is rolled back
        await using var verifyCmd = _connection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM TestTable";
        long count = (long)(await verifyCmd.ExecuteScalarAsync())!;
        count.Should().Be(0);
    }

    [Fact]
    public async Task IsolationLevel_Returns_Correct_Value()
    {
        var adapterConn = new AdapterDbConnection(_connection);
        DbTransaction innerTx = await _connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var tx = new AdapterDbTransaction(innerTx, adapterConn);

        tx.IsolationLevel.Should().Be(IsolationLevel.Serializable);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Connection_Returns_AdapterDbConnection()
    {
        var adapterConn = new AdapterDbConnection(_connection);
        DbTransaction innerTx = await _connection.BeginTransactionAsync();
        var tx = new AdapterDbTransaction(innerTx, adapterConn);

        tx.Connection.Should().BeSameAs(adapterConn);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task InnerTransaction_Returns_Wrapped_Transaction()
    {
        var adapterConn = new AdapterDbConnection(_connection);
        DbTransaction innerTx = await _connection.BeginTransactionAsync();
        var tx = new AdapterDbTransaction(innerTx, adapterConn);

        ((DbTransaction)tx).Should().BeSameAs(innerTx);

        await tx.RollbackAsync();
    }
}

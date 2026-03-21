#pragma warning disable CA1001 // Type owns disposable field(s) -- disposed via IAsyncLifetime

using System.Data.Async.DataSet;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbDataAdapterTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AdapterDbConnection _adapterConnection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _adapterConnection = new AdapterDbConnection(_connection);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Products (Id INTEGER PRIMARY KEY, Name TEXT, Price REAL);
            INSERT INTO Products (Id, Name, Price) VALUES (1, 'Widget', 9.99);
            INSERT INTO Products (Id, Name, Price) VALUES (2, 'Gadget', 19.99);
            INSERT INTO Products (Id, Name, Price) VALUES (3, 'Doohickey', 4.99);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private AdapterDbCommand CreateSelectCommand(string sql)
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = sql;
        return new AdapterDbCommand(innerCmd, _adapterConnection);
    }

    [Fact]
    public async Task FillAsync_AsyncDataTable_Loads_Data_From_SQLite()
    {
        using var cmd = CreateSelectCommand("SELECT Id, Name, Price FROM Products ORDER BY Id");
        var adapter = new AdapterDbDataAdapter(cmd);
        using var table = new AsyncDataTable("Products");

        var count = await adapter.FillAsync(table);

        count.Should().Be(3);
        table.Columns.Count.Should().Be(3);
        table.Rows.Count.Should().Be(3);
        table.Rows[0]["Name"].Should().Be("Widget");
        table.Rows[2]["Name"].Should().Be("Doohickey");
    }

    [Fact]
    public async Task FillAsync_AsyncDataSet_Loads_Data()
    {
        using var cmd = CreateSelectCommand("SELECT Id, Name FROM Products");
        var adapter = new AdapterDbDataAdapter(cmd);
        using var dataSet = new AsyncDataSet("TestDS");

        var count = await adapter.FillAsync(dataSet);

        count.Should().Be(3);
        dataSet.Tables.Count.Should().Be(1);
        dataSet.Tables[0].TableName.Should().Be("Table");
        dataSet.Tables[0].Rows.Count.Should().Be(3);
    }

    [Fact]
    public async Task FillAsync_Opens_Closed_Connection_And_Closes_After()
    {
        // Create a separate connection that starts closed
        await using var closedConn = new SqliteConnection(_connection.ConnectionString);
        // We need a separate in-memory db, so let's use a shared cache
        await using var setupConn = new SqliteConnection("Data Source=TestFillOpens;Mode=Memory;Cache=Shared");
        await setupConn.OpenAsync();
        await using var setupCmd = setupConn.CreateCommand();
        setupCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Items (Id INTEGER PRIMARY KEY);
            INSERT INTO Items (Id) VALUES (1);
            """;
        await setupCmd.ExecuteNonQueryAsync();

        await using var testConn = new SqliteConnection("Data Source=TestFillOpens;Mode=Memory;Cache=Shared");
        var adapterConn = new AdapterDbConnection(testConn);

        testConn.State.Should().Be(ConnectionState.Closed);

        var innerCmd = testConn.CreateCommand();
        innerCmd.CommandText = "SELECT Id FROM Items";
        using var cmd = new AdapterDbCommand(innerCmd, adapterConn);
        var adapter = new AdapterDbDataAdapter(cmd);
        using var table = new AsyncDataTable();

        var count = await adapter.FillAsync(table);

        count.Should().Be(1);
        testConn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task FillAsync_With_Already_Open_Connection_Does_Not_Close_It()
    {
        _connection.State.Should().Be(ConnectionState.Open);

        using var cmd = CreateSelectCommand("SELECT Id FROM Products");
        var adapter = new AdapterDbDataAdapter(cmd);
        using var table = new AsyncDataTable();

        await adapter.FillAsync(table);

        _connection.State.Should().Be(ConnectionState.Open);
    }
}

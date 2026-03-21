#pragma warning disable CA1001 // Type owns disposable field(s) -- disposed via IAsyncLifetime

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbCommandTests : IAsyncLifetime
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
            CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT);
            INSERT INTO TestTable (Id, Name) VALUES (1, 'Alice');
            INSERT INTO TestTable (Id, Name) VALUES (2, 'Bob');
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteReaderAsync_Returns_AdapterDbDataReader()
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = "SELECT * FROM TestTable";
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        IAsyncDataReader reader = await cmd.ExecuteReaderAsync();

        reader.Should().BeOfType<AdapterDbDataReader>();
        await reader.DisposeAsync();
        cmd.Dispose();
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_Returns_Affected_Rows()
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = "INSERT INTO TestTable (Id, Name) VALUES (3, 'Charlie')";
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        int affected = await cmd.ExecuteNonQueryAsync();

        affected.Should().Be(1);
        cmd.Dispose();
    }

    [Fact]
    public async Task ExecuteScalarAsync_Returns_Scalar_Value()
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = "SELECT COUNT(*) FROM TestTable";
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        object? result = await cmd.ExecuteScalarAsync();

        ((long)result!).Should().Be(2);
        cmd.Dispose();
    }

    [Fact]
    public async Task Parameters_Work_With_Parameterized_Queries()
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = "SELECT Name FROM TestTable WHERE Id = @id";
        var param = innerCmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = 1;
        innerCmd.Parameters.Add(param);
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        IAsyncDataReader reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("Alice");

        await reader.DisposeAsync();
        cmd.Dispose();
    }

    [Fact]
    public void CreateParameter_Works()
    {
        var innerCmd = _connection.CreateCommand();
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        IDbDataParameter parameter = cmd.CreateParameter();

        parameter.Should().NotBeNull();
        cmd.Dispose();
    }

    [Fact]
    public void InnerCommand_Returns_Wrapped_Command()
    {
        var innerCmd = _connection.CreateCommand();
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        cmd.InnerCommand.Should().BeSameAs(innerCmd);
        cmd.Dispose();
    }

    [Fact]
    public void CommandText_Delegates_To_Inner()
    {
        var innerCmd = _connection.CreateCommand();
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        cmd.CommandText = "SELECT 1";
        cmd.CommandText.Should().Be("SELECT 1");
        innerCmd.CommandText.Should().Be("SELECT 1");
        cmd.Dispose();
    }

    [Fact]
    public void Connection_Property_Returns_AdapterDbConnection()
    {
        var innerCmd = _connection.CreateCommand();
        var cmd = new AdapterDbCommand(innerCmd, _adapterConnection);

        cmd.Connection.Should().BeSameAs(_adapterConnection);
        cmd.Dispose();
    }
}

#pragma warning disable CA1001 // Type owns disposable field(s) -- disposed via IAsyncLifetime

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbDataReaderTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT, Value REAL, IsActive INTEGER, Data TEXT NULL);
            INSERT INTO TestTable (Id, Name, Value, IsActive, Data) VALUES (1, 'Alice', 1.5, 1, NULL);
            INSERT INTO TestTable (Id, Name, Value, IsActive, Data) VALUES (2, 'Bob', 2.5, 0, 'some data');
            INSERT INTO TestTable (Id, Name, Value, IsActive, Data) VALUES (3, 'Charlie', 3.5, 1, NULL);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ReadAsync_Returns_True_Then_False()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM TestTable";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Await_Foreach_Iterates_All_Rows()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM TestTable";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        int count = 0;
        await foreach (IAsyncDataRecord _ in reader)
        {
            count++;
        }

        count.Should().Be(3);
    }

    [Fact]
    public async Task Get_Methods_Return_Correct_Values()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Value FROM TestTable WHERE Id = 1";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        (await reader.ReadAsync()).Should().BeTrue();

        reader.GetInt64(0).Should().Be(1);
        reader.GetString(1).Should().Be("Alice");
        reader.GetDouble(2).Should().Be(1.5);
        reader.GetName(0).Should().Be("Id");
        reader.GetOrdinal("Name").Should().Be(1);
        reader.FieldCount.Should().Be(3);
    }

    [Fact]
    public async Task IsDBNullAsync_Works()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Data FROM TestTable WHERE Id = 1";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.IsDBNullAsync(0)).Should().BeTrue();
    }

    [Fact]
    public async Task IsDBNullAsync_Returns_False_For_NonNull()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Data FROM TestTable WHERE Id = 2";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.IsDBNullAsync(0)).Should().BeFalse();
    }

    [Fact]
    public async Task GetFieldValueAsync_Works()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM TestTable WHERE Id = 1";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.GetFieldValueAsync<string>(0)).Should().Be("Alice");
    }

    [Fact]
    public async Task InnerReader_Returns_Wrapped_Reader()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM TestTable";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        ((DbDataReader)reader).Should().BeSameAs(innerReader);
    }

    [Fact]
    public async Task HasRows_Returns_True_When_Rows_Exist()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM TestTable";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        reader.HasRows.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_Read_Delegates_To_Inner()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM TestTable";
        await using var innerReader = await cmd.ExecuteReaderAsync();
        var reader = new AdapterDbDataReader(innerReader);

        reader.Read().Should().BeTrue();
        reader.GetInt64(0).Should().Be(1);
    }
}

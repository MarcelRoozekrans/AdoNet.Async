using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class EdgeCaseParityTests
{
    private readonly ValidationFixture _fixture;

    public EdgeCaseParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Empty_ResultSet_Behaves_Same()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM Users WHERE Id = -1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawHasRows = rawReader.HasRows;
        var rawReadResult = rawReader.Read();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM Users WHERE Id = -1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncHasRows = asyncReader.HasRows;
        var asyncReadResult = await asyncReader.ReadAsync();

        asyncHasRows.Should().Be(rawHasRows);
        asyncReadResult.Should().Be(rawReadResult);
    }

    [Fact]
    public async Task Large_ResultSet_Returns_All_Rows()
    {
        // Insert 1000 rows into a temp table
        using var setup = _fixture.Provider.CreateRawConnection();
        setup.Open();
        using var createCmd = setup.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS LargeTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        createCmd.ExecuteNonQuery();
        using var insertCmd = setup.CreateCommand();
        var sb = new System.Text.StringBuilder("INSERT OR IGNORE INTO LargeTest VALUES ");
        for (int i = 1; i <= 1000; i++)
        {
            if (i > 1) sb.Append(", ");
            sb.Append(CultureInfo.InvariantCulture, $"({i}, 'val{i}')");
        }
        insertCmd.CommandText = sb.ToString();
        insertCmd.ExecuteNonQuery();

        // Raw count
        using var rawCmd = setup.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM LargeTest";
        using var rawReader = rawCmd.ExecuteReader();
        int rawCount = 0;
        while (rawReader.Read()) rawCount++;

        // Async count
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM LargeTest";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        int asyncCount = 0;
        while (await asyncReader.ReadAsync()) asyncCount++;

        asyncCount.Should().Be(rawCount);
        asyncCount.Should().Be(1000);
    }

    [Fact]
    public async Task Fill_Empty_Table_Produces_Zero_Rows()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS EmptyFillTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        await createCmd.ExecuteNonQueryAsync();

        var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM EmptyFillTest";
        await deleteCmd.ExecuteNonQueryAsync();

        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM EmptyFillTest";
        var adapter = new AdapterDbDataAdapter(selectCmd);
        var table = new AsyncDataTable("EmptyFillTest");
        var rowCount = await adapter.FillAsync(table);

        rowCount.Should().Be(0);
        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task CancellationToken_Is_Respected()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users";

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        Func<Task> act = async () =>
        {
            await using var reader = await cmd.ExecuteReaderAsync(cts.Token);
            while (await reader.ReadAsync(cts.Token)) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

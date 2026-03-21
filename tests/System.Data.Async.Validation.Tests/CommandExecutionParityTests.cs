using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class CommandExecutionParityTests
{
    private readonly ValidationFixture _fixture;

    public CommandExecutionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExecuteScalar_Returns_Same_Value()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var rawResult = rawCmd.ExecuteScalar();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var asyncResult = await asyncCmd.ExecuteScalarAsync();

        Convert.ToInt64(asyncResult, CultureInfo.InvariantCulture)
            .Should().Be(Convert.ToInt64(rawResult, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ExecuteNonQuery_Returns_Same_Affected_Rows()
    {
        // Use temp tables so we don't affect shared data
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var setup1 = raw.CreateCommand();
        setup1.CommandText = "CREATE TABLE IF NOT EXISTS TempNQ (Id INTEGER PRIMARY KEY, Val TEXT)";
        setup1.ExecuteNonQuery();
        using var rawIns = raw.CreateCommand();
        rawIns.CommandText = "INSERT INTO TempNQ VALUES (1, 'a'), (2, 'b')";
        var rawInserted = rawIns.ExecuteNonQuery();
        using var rawUpd = raw.CreateCommand();
        rawUpd.CommandText = "UPDATE TempNQ SET Val = 'x'";
        var rawUpdated = rawUpd.ExecuteNonQuery();
        using var rawDel = raw.CreateCommand();
        rawDel.CommandText = "DELETE FROM TempNQ";
        var rawDeleted = rawDel.ExecuteNonQuery();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var setup2 = async_.CreateCommand();
        setup2.CommandText = "CREATE TABLE IF NOT EXISTS TempNQAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await setup2.ExecuteNonQueryAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.CommandText = "INSERT INTO TempNQAsync VALUES (1, 'a'), (2, 'b')";
        var asyncInserted = await asyncIns.ExecuteNonQueryAsync();
        var asyncUpd = async_.CreateCommand();
        asyncUpd.CommandText = "UPDATE TempNQAsync SET Val = 'x'";
        var asyncUpdated = await asyncUpd.ExecuteNonQueryAsync();
        var asyncDel = async_.CreateCommand();
        asyncDel.CommandText = "DELETE FROM TempNQAsync";
        var asyncDeleted = await asyncDel.ExecuteNonQueryAsync();

        asyncInserted.Should().Be(rawInserted);
        asyncUpdated.Should().Be(rawUpdated);
        asyncDeleted.Should().Be(rawDeleted);
    }

    [Fact]
    public async Task ExecuteReader_Returns_Same_Data()
    {
        var rawRows = new List<(long Id, string Name, string Email)>();
        var asyncRows = new List<(long Id, string Name, string Email)>();

        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read())
        {
            rawRows.Add((rawReader.GetInt64(0), rawReader.GetString(1), rawReader.GetString(2)));
        }

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync())
        {
            asyncRows.Add((asyncReader.GetInt64(0), asyncReader.GetString(1), asyncReader.GetString(2)));
        }

        asyncRows.Should().BeEquivalentTo(rawRows, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Parameterized_Query_Returns_Same_Results()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age AND IsActive = @active ORDER BY Id";
        var p1 = rawCmd.CreateParameter();
        p1.ParameterName = "@age";
        p1.Value = 30;
        rawCmd.Parameters.Add(p1);
        var p2 = rawCmd.CreateParameter();
        p2.ParameterName = "@active";
        p2.Value = 1;
        rawCmd.Parameters.Add(p2);
        var rawNames = new List<string>();
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read()) rawNames.Add(rawReader.GetString(0));

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age AND IsActive = @active ORDER BY Id";
        var ap1 = asyncCmd.CreateParameter();
        ap1.ParameterName = "@age";
        ap1.Value = 30;
        asyncCmd.Parameters.Add(ap1);
        var ap2 = asyncCmd.CreateParameter();
        ap2.ParameterName = "@active";
        ap2.Value = 1;
        asyncCmd.Parameters.Add(ap2);
        var asyncNames = new List<string>();
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync()) asyncNames.Add(asyncReader.GetString(0));

        asyncNames.Should().BeEquivalentTo(rawNames, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task PrepareAsync_Does_Not_Throw()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age";
        var rp = rawCmd.CreateParameter();
        rp.ParameterName = "@age";
        rp.Value = 25;
        rawCmd.Parameters.Add(rp);

        var preparingRaw = () => rawCmd.Prepare();
        preparingRaw.Should().NotThrow();

        var rawNames = new List<string>();
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read()) rawNames.Add(rawReader.GetString(0));

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Name FROM Users WHERE Age > @age";
        var ap = asyncCmd.CreateParameter();
        ap.ParameterName = "@age";
        ap.Value = 25;
        asyncCmd.Parameters.Add(ap);

        var preparingAsync = async () => await asyncCmd.PrepareAsync();
        await preparingAsync.Should().NotThrowAsync();

        var asyncNames = new List<string>();
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync()) asyncNames.Add(asyncReader.GetString(0));

        asyncNames.Should().BeEquivalentTo(rawNames, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteReader_With_CommandBehavior_SchemaOnly()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users";
        using var rawReader = rawCmd.ExecuteReader(CommandBehavior.SchemaOnly);
        var rawHasRows = rawReader.Read();
        var rawFieldCount = rawReader.FieldCount;
        var rawColumns = new List<string>();
        for (var i = 0; i < rawFieldCount; i++) rawColumns.Add(rawReader.GetName(i));

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly);
        var asyncHasRows = await asyncReader.ReadAsync();
        var asyncFieldCount = asyncReader.FieldCount;
        var asyncColumns = new List<string>();
        for (var i = 0; i < asyncFieldCount; i++) asyncColumns.Add(asyncReader.GetName(i));

        asyncHasRows.Should().Be(rawHasRows, "async SchemaOnly should match raw SchemaOnly read behavior");
        asyncFieldCount.Should().Be(rawFieldCount);
        asyncColumns.Should().BeEquivalentTo(rawColumns, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteReader_With_CommandBehavior_SingleRow()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id";
        using var rawReader = rawCmd.ExecuteReader(CommandBehavior.SingleRow);
        var rawRowCount = 0;
        while (rawReader.Read()) rawRowCount++;

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        var asyncRowCount = 0;
        while (await asyncReader.ReadAsync()) asyncRowCount++;

        asyncRowCount.Should().Be(rawRowCount, "async SingleRow should return the same number of rows as raw SingleRow");
    }
}

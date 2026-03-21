using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class ReaderParityTests
{
    private readonly ValidationFixture _fixture;

    public ReaderParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Field_Access_By_Index_And_Name_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email, Age FROM Users WHERE Id = 1";
        using var rawReader = rawCmd.ExecuteReader();
        rawReader.Read();
        var rawById = (rawReader.GetInt64(0), rawReader.GetString(1), rawReader.GetString(2), rawReader.GetInt64(3));
        var rawByName = (rawReader["Id"], rawReader["Name"], rawReader["Email"], rawReader["Age"]);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email, Age FROM Users WHERE Id = 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        await asyncReader.ReadAsync();
        var asyncById = (asyncReader.GetInt64(0), asyncReader.GetString(1), asyncReader.GetString(2), asyncReader.GetInt64(3));
        var asyncByName = (asyncReader["Id"], asyncReader["Name"], asyncReader["Email"], asyncReader["Age"]);

        asyncById.Should().Be(rawById);
        asyncByName.Should().BeEquivalentTo(rawByName);
    }

    [Fact]
    public async Task FieldCount_And_GetName_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawFieldCount = rawReader.FieldCount;
        var rawNames = Enumerable.Range(0, rawFieldCount).Select(rawReader.GetName).ToList();
        var rawOrdinals = rawNames.Select(rawReader.GetOrdinal).ToList();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncFieldCount = asyncReader.FieldCount;
        var asyncNames = Enumerable.Range(0, asyncFieldCount).Select(asyncReader.GetName).ToList();
        var asyncOrdinals = asyncNames.Select(asyncReader.GetOrdinal).ToList();

        asyncFieldCount.Should().Be(rawFieldCount);
        asyncNames.Should().BeEquivalentTo(rawNames, opts => opts.WithStrictOrdering());
        asyncOrdinals.Should().BeEquivalentTo(rawOrdinals, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task IsDBNull_Matches_For_Null_Values()
    {
        // Create a table with NULL values
        using var setup = _fixture.Provider.CreateRawConnection();
        setup.Open();
        using var setupCmd = setup.CreateCommand();
        setupCmd.CommandText = "CREATE TABLE IF NOT EXISTS NullTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        setupCmd.ExecuteNonQuery();
        using var insCmd = setup.CreateCommand();
        insCmd.CommandText = "INSERT OR IGNORE INTO NullTest VALUES (1, NULL), (2, 'hello')";
        insCmd.ExecuteNonQuery();

        var rawNulls = new List<bool>();
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Val FROM NullTest ORDER BY Id";
        using var rawReader = rawCmd.ExecuteReader();
        while (rawReader.Read()) rawNulls.Add(rawReader.IsDBNull(0));

        var asyncNulls = new List<bool>();
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Val FROM NullTest ORDER BY Id";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        while (await asyncReader.ReadAsync()) asyncNulls.Add(await asyncReader.IsDBNullAsync(0));

        asyncNulls.Should().BeEquivalentTo(rawNulls, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetSchemaTable_Returns_Equivalent_Schema()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawSchema = rawReader.GetSchemaTable();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name, Email FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncSchema = await asyncReader.GetSchemaTableAsync();

        asyncSchema.Rows.Count.Should().Be(rawSchema!.Rows.Count);
        for (int i = 0; i < rawSchema.Rows.Count; i++)
        {
            asyncSchema.Rows[i]["ColumnName"].Should().Be(rawSchema.Rows[i]["ColumnName"]);
        }
    }

    [Fact]
    public async Task AwaitForeach_Produces_Same_Data_As_Manual_Loop()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        // Manual loop
        var manualCmd = conn.CreateCommand();
        manualCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var manualRows = new List<(long, string)>();
        await using var reader1 = await manualCmd.ExecuteReaderAsync();
        while (await reader1.ReadAsync())
        {
            manualRows.Add((reader1.GetInt64(0), reader1.GetString(1)));
        }

        // await foreach
        var foreachCmd = conn.CreateCommand();
        foreachCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var foreachRows = new List<(long, string)>();
        await using var reader2 = await foreachCmd.ExecuteReaderAsync();
        await foreach (var record in reader2)
        {
            foreachRows.Add((record.GetInt64(0), record.GetString(1)));
        }

        foreachRows.Should().BeEquivalentTo(manualRows, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task HasRows_And_RecordsAffected_Match()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT * FROM Users LIMIT 5";
        using var rawReader = rawCmd.ExecuteReader();
        var rawHasRows = rawReader.HasRows;
        var rawRecordsAffected = rawReader.RecordsAffected;

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT * FROM Users LIMIT 5";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncHasRows = asyncReader.HasRows;
        var asyncRecordsAffected = asyncReader.RecordsAffected;

        asyncHasRows.Should().Be(rawHasRows);
        asyncRecordsAffected.Should().Be(rawRecordsAffected);
    }

    [Fact]
    public async Task NextResult_For_MultiResultSet()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 2; SELECT Id, UserId FROM Orders ORDER BY Id LIMIT 2";
        using var rawReader = rawCmd.ExecuteReader();

        var rawFirstSet = new List<(long Id, string Name)>();
        while (rawReader.Read())
        {
            rawFirstSet.Add((rawReader.GetInt64(0), rawReader.GetString(1)));
        }

        var rawHasSecond = rawReader.NextResult();
        var rawSecondSet = new List<(long Id, long UserId)>();
        while (rawReader.Read())
        {
            rawSecondSet.Add((rawReader.GetInt64(0), rawReader.GetInt64(1)));
        }

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 2; SELECT Id, UserId FROM Orders ORDER BY Id LIMIT 2";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();

        var asyncFirstSet = new List<(long Id, string Name)>();
        while (await asyncReader.ReadAsync())
        {
            asyncFirstSet.Add((asyncReader.GetInt64(0), asyncReader.GetString(1)));
        }

        var asyncHasSecond = await asyncReader.NextResultAsync();
        var asyncSecondSet = new List<(long Id, long UserId)>();
        while (await asyncReader.ReadAsync())
        {
            asyncSecondSet.Add((asyncReader.GetInt64(0), asyncReader.GetInt64(1)));
        }

        asyncHasSecond.Should().Be(rawHasSecond);
        asyncFirstSet.Should().BeEquivalentTo(rawFirstSet, opts => opts.WithStrictOrdering());
        asyncSecondSet.Should().BeEquivalentTo(rawSecondSet, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFieldValueAsync_Matches_Sync_GetFieldValue()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name FROM Users WHERE Id = 1";
        using var rawReader = rawCmd.ExecuteReader();
        rawReader.Read();
        var rawId = rawReader.GetInt64(0);
        var rawName = rawReader.GetString(1);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name FROM Users WHERE Id = 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        await asyncReader.ReadAsync();
        var asyncId = await asyncReader.GetFieldValueAsync<long>(0);
        var asyncName = await asyncReader.GetFieldValueAsync<string>(1);

        asyncId.Should().Be(rawId);
        asyncName.Should().Be(rawName);
    }

    [Fact]
    public async Task Close_And_IsClosed_Behave_Same()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawIsClosedBefore = rawReader.IsClosed;
        rawReader.Close();
        var rawIsClosedAfter = rawReader.IsClosed;

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncIsClosedBefore = asyncReader.IsClosed;
        await asyncReader.CloseAsync();
        var asyncIsClosedAfter = asyncReader.IsClosed;

        asyncIsClosedBefore.Should().Be(rawIsClosedBefore);
        rawIsClosedBefore.Should().BeFalse();
        asyncIsClosedAfter.Should().Be(rawIsClosedAfter);
        rawIsClosedAfter.Should().BeTrue();
    }

    [Fact]
    public async Task Various_Field_Type_Accessors_Match()
    {
        using var setup = _fixture.Provider.CreateRawConnection();
        setup.Open();
        using var setupCmd = setup.CreateCommand();
        setupCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TypeTest (IntVal INTEGER, RealVal REAL, TextVal TEXT);
            INSERT OR IGNORE INTO TypeTest VALUES (42, 3.14, 'hello');
            """;
        setupCmd.ExecuteNonQuery();

        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT IntVal, RealVal, TextVal FROM TypeTest LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        rawReader.Read();
        var rawInt = rawReader.GetInt64(0);
        var rawDouble = rawReader.GetDouble(1);
        var rawString = rawReader.GetString(2);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT IntVal, RealVal, TextVal FROM TypeTest LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        await asyncReader.ReadAsync();
        var asyncInt = asyncReader.GetInt64(0);
        var asyncDouble = asyncReader.GetDouble(1);
        var asyncString = asyncReader.GetString(2);

        asyncInt.Should().Be(rawInt);
        asyncDouble.Should().Be(rawDouble);
        asyncString.Should().Be(rawString);
    }

    [Fact]
    public async Task Depth_Property_Matches()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id FROM Users LIMIT 1";
        using var rawReader = rawCmd.ExecuteReader();
        var rawDepth = rawReader.Depth;

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id FROM Users LIMIT 1";
        await using var asyncReader = await asyncCmd.ExecuteReaderAsync();
        var asyncDepth = asyncReader.Depth;

        asyncDepth.Should().Be(rawDepth);
    }
}

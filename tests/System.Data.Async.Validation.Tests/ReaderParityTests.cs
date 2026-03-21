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
}

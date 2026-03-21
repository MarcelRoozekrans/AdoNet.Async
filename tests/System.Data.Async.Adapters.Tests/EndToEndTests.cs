using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class EndToEndTests
{
    [Fact]
    public async Task Full_Workflow_Create_Query_Fill_Serialize_Roundtrip()
    {
        // 1. Create connection via AsAsync()
        await using var conn = new SqliteConnection("Data Source=:memory:").AsAsync();
        await conn.OpenAsync();

        // 2. Create table
        await using var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT, Balance REAL)";
        await createCmd.ExecuteNonQueryAsync();

        // 3. Insert data
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Users VALUES (1, 'Alice', 100.50), (2, 'Bob', 200.75)";
        await insertCmd.ExecuteNonQueryAsync();

        // 4. Query with IAsyncEnumerable
        await using var queryCmd = conn.CreateCommand();
        queryCmd.CommandText = "SELECT * FROM Users";
        var names = new List<string>();
        await using var reader = await queryCmd.ExecuteReaderAsync();
        await foreach (var record in reader)
        {
            names.Add(record.GetString(1));
        }
        names.Should().BeEquivalentTo(["Alice", "Bob"]);

        // 5. Fill AsyncDataTable via adapter
        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Users";
        var adapter = new AdapterDbDataAdapter(selectCmd);
        var table = new AsyncDataTable("Users");
        await adapter.FillAsync(table);
        table.Rows.Count.Should().Be(2);

        // 6. Serialize to JSON and back
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());
        var json = JsonConvert.SerializeObject(table, settings);
        var deserialized = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);
        deserialized!.Rows.Count.Should().Be(2);
        ((string)deserialized.Rows[0]["Name"]).Should().Be("Alice");
    }

    [Fact]
    public async Task Transaction_Commit_And_Rollback()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:").AsAsync();
        await conn.OpenAsync();

        var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT)";
        await createCmd.ExecuteNonQueryAsync();

        // Commit
        await using (var tx = await conn.BeginTransactionAsync())
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items VALUES (1, 'Committed')";
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }

        // Verify committed
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Items";
        var count = await checkCmd.ExecuteScalarAsync();
        Convert.ToInt32(count, CultureInfo.InvariantCulture).Should().Be(1);

        // Rollback
        await using (var tx = await conn.BeginTransactionAsync())
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items VALUES (2, 'Rolled Back')";
            await cmd.ExecuteNonQueryAsync();
            await tx.RollbackAsync();
        }

        // Verify not committed
        var checkCmd2 = conn.CreateCommand();
        checkCmd2.CommandText = "SELECT COUNT(*) FROM Items";
        var count2 = await checkCmd2.ExecuteScalarAsync();
        Convert.ToInt32(count2, CultureInfo.InvariantCulture).Should().Be(1); // Still 1
    }
}

using System.Data.Async.Adapters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using System.Data.Common;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class DataAdapterParityTests
{
    private readonly ValidationFixture _fixture;

    public DataAdapterParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Fill_Produces_Same_RowCount_And_Data()
    {
        // Raw: use DbDataReader to manually load a DataTable
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id";
        var rawTable = new DataTable("Users");
        using var rawReader = rawCmd.ExecuteReader();
        rawTable.Load(rawReader);

        // Async: use AdapterDbDataAdapter.FillAsync
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncSelectCmd = async_.CreateCommand();
        asyncSelectCmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id";
        var adapter = new AdapterDbDataAdapter(asyncSelectCmd);
        var asyncTable = new AsyncDataTable("Users");
        await adapter.FillAsync(asyncTable);

        asyncTable.Rows.Count.Should().Be(rawTable.Rows.Count);
        for (int i = 0; i < rawTable.Rows.Count; i++)
        {
            asyncTable.Rows[i]["Id"].Should().Be(rawTable.Rows[i]["Id"]);
            asyncTable.Rows[i]["Name"].Should().Be(rawTable.Rows[i]["Name"]);
            asyncTable.Rows[i]["Email"].Should().Be(rawTable.Rows[i]["Email"]);
        }
    }

    [Fact]
    public async Task Fill_AsyncDataSet_Produces_Same_Data()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCmd = raw.CreateCommand();
        rawCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var rawDs = new System.Data.DataSet("TestDS");
        var rawTable = new DataTable("Users");
        rawDs.Tables.Add(rawTable);
        using var rawReader = rawCmd.ExecuteReader();
        rawTable.Load(rawReader);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCmd = async_.CreateCommand();
        asyncCmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 10";
        var adapter = new AdapterDbDataAdapter(asyncCmd);
        var asyncDs = new AsyncDataSet("TestDS");
        await adapter.FillAsync(asyncDs);

        asyncDs.Tables.Count.Should().Be(rawDs.Tables.Count);
        asyncDs.Tables[0].Rows.Count.Should().Be(rawDs.Tables[0].Rows.Count);
    }

    [Fact]
    public async Task Update_Roundtrip_Produces_Same_Affected_Rows()
    {
        // Setup: create table with data for update test
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();

        var createCmd = conn.CreateCommand();
        createCmd.CommandText = "CREATE TABLE IF NOT EXISTS AdapterUpdateTest (Id INTEGER PRIMARY KEY, Val TEXT)";
        await createCmd.ExecuteNonQueryAsync();

        var insCmd = conn.CreateCommand();
        insCmd.CommandText = "INSERT OR IGNORE INTO AdapterUpdateTest VALUES (1, 'original'), (2, 'original')";
        await insCmd.ExecuteNonQueryAsync();

        // Fill
        var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Val FROM AdapterUpdateTest";

        var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE AdapterUpdateTest SET Val = @Val WHERE Id = @Id";
        var pVal = updateCmd.CreateParameter();
        pVal.ParameterName = "@Val";
        pVal.SourceColumn = "Val";
        updateCmd.Parameters.Add(pVal);
        var pId = updateCmd.CreateParameter();
        pId.ParameterName = "@Id";
        pId.SourceColumn = "Id";
        updateCmd.Parameters.Add(pId);

        var adapter = new AdapterDbDataAdapter(selectCmd) { UpdateCommand = updateCmd };
        var table = new AsyncDataTable("AdapterUpdateTest");
        await adapter.FillAsync(table);

        // Modify
        await table.Rows[0].SetValueAsync("Val", "modified");

        // Update
        var affected = await adapter.UpdateAsync(table);
        affected.Should().BeGreaterThan(0);

        // Verify round-trip
        var verifyTable = new AsyncDataTable("AdapterUpdateTest");
        await adapter.FillAsync(verifyTable);
        ((string)verifyTable.Rows[0]["Val"]).Should().Be("modified");
    }
}

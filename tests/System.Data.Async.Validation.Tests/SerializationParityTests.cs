using System.Data.Async.Adapters;
using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class SerializationParityTests
{
    private readonly ValidationFixture _fixture;

    public SerializationParityTests(ValidationFixture fixture) => _fixture = fixture;

    private async Task<AsyncDataTable> LoadUsersTableAsync()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email FROM Users ORDER BY Id LIMIT 10";
        var adapter = new AdapterDbDataAdapter(cmd);
        var table = new AsyncDataTable("Users");
        await adapter.FillAsync(table);
        return table;
    }

    [Fact]
    public async Task Xml_WriteRead_Roundtrip_Preserves_Data()
    {
        var original = await LoadUsersTableAsync();

        // DataTable.ReadXml requires the schema to be loaded first; write schema
        // and data separately, then restore schema before reading data.
        using var schemaStream = new MemoryStream();
        await original.WriteXmlSchemaAsync(schemaStream);

        using var xmlStream = new MemoryStream();
        await original.WriteXmlAsync(xmlStream);

        var restored = new AsyncDataTable("Users");

        schemaStream.Position = 0;
        await restored.ReadXmlSchemaAsync(schemaStream);

        xmlStream.Position = 0;
        await restored.ReadXmlAsync(xmlStream);

        restored.Rows.Count.Should().Be(original.Rows.Count);
        for (int i = 0; i < original.Rows.Count; i++)
        {
            restored.Rows[i]["Name"].Should().Be(original.Rows[i]["Name"]);
            restored.Rows[i]["Email"].Should().Be(original.Rows[i]["Email"]);
        }
    }

    [Fact]
    public async Task Xml_Schema_Roundtrip_Preserves_Columns()
    {
        var original = await LoadUsersTableAsync();

        using var schemaStream = new MemoryStream();
        await original.WriteXmlSchemaAsync(schemaStream);
        schemaStream.Position = 0;

        var restored = new AsyncDataTable("Users");
        await restored.ReadXmlSchemaAsync(schemaStream);

        restored.Columns.Count.Should().Be(original.Columns.Count);
        for (int i = 0; i < original.Columns.Count; i++)
        {
            restored.Columns[i].ColumnName.Should().Be(original.Columns[i].ColumnName);
        }
    }

    [Fact]
    public async Task Json_Roundtrip_Preserves_Data()
    {
        var original = await LoadUsersTableAsync();

        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());

        var json = JsonConvert.SerializeObject(original, settings);
        var restored = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings);

        restored!.Rows.Count.Should().Be(original.Rows.Count);
        for (int i = 0; i < original.Rows.Count; i++)
        {
            restored.Rows[i]["Name"].Should().Be(original.Rows[i]["Name"]);
        }
    }

    [Fact]
    public async Task AsyncDataSet_Xml_Roundtrip_Preserves_Data()
    {
        await using var conn = _fixture.Provider.CreateAsyncConnection();
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Users ORDER BY Id LIMIT 5";
        var adapter = new AdapterDbDataAdapter(cmd);
        var ds = new AsyncDataSet("TestDS");
        await adapter.FillAsync(ds);

        using var xmlStream = new MemoryStream();
        await ds.WriteXmlAsync(xmlStream);
        xmlStream.Position = 0;

        var restored = new AsyncDataSet("TestDS");
        await restored.ReadXmlAsync(xmlStream);

        restored.Tables.Count.Should().Be(ds.Tables.Count);
        restored.Tables[0].Rows.Count.Should().Be(ds.Tables[0].Rows.Count);
    }
}

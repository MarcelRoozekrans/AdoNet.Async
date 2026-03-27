using System.Data.Async.Converters;
using System.Data.Async.DataSet;

using FluentAssertions;

using Newtonsoft.Json;

using Xunit;

namespace System.Data.Async.DataSet.Tests.Converters;

public class AsyncDataTableConverterTests
{
    private static JsonSerializerSettings CreateSettings()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new AsyncDataTableConverter());
        return settings;
    }

    [Fact]
    public void Should_Roundtrip_Simple_Table()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.Rows.Add(2, "Bob");
        table.InnerDataTable.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["Name"].Should().Be("Bob");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void Should_Handle_Modified_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.AcceptChanges();
        table.InnerDataTable.Rows[0]["Name"] = "Alicia";

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Modified);
        result.Rows[0]["Name"].Should().Be("Alicia");
        result.Rows[0]["Name", DataRowVersion.Original].Should().Be("Alice");
    }

    [Fact]
    public void Should_Handle_Added_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Alice");

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
        result.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public void Should_Handle_Deleted_Rows()
    {
        var table = new AsyncDataTable("Users");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.AcceptChanges();
        table.InnerDataTable.Rows[0].Delete();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public void Should_Handle_DBNull()
    {
        var table = new AsyncDataTable("Test");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, DBNull.Value);
        table.InnerDataTable.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public void Should_Handle_Decimal_Precision()
    {
        var table = new AsyncDataTable("Test");
        table.Columns.Add("Amount", typeof(decimal));
        table.InnerDataTable.Rows.Add(123.456789012345678901234567890m);
        table.InnerDataTable.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        ((decimal)result.Rows[0]["Amount"]).Should().Be(123.456789012345678901234567890m);
    }

    [Fact]
    public void Should_Handle_Constraints()
    {
        var table = new AsyncDataTable("Users");
        var idCol = table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.PrimaryKey = [idCol];
        table.InnerDataTable.Rows.Add(1, "Alice");
        table.InnerDataTable.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.PrimaryKey.Should().HaveCount(1);
        result.PrimaryKey[0].ColumnName.Should().Be("Id");
    }

    [Fact]
    public void Should_Handle_Empty_Table()
    {
        var table = new AsyncDataTable("Empty");
        table.Columns.Add("Id", typeof(int));

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.TableName.Should().Be("Empty");
        result.Columns.Count.Should().Be(1);
        result.Rows.Count.Should().Be(0);
    }

    [Fact]
    public void Should_Handle_Multiple_DataTypes()
    {
        var table = new AsyncDataTable("Types");
        table.Columns.Add("Int", typeof(int));
        table.Columns.Add("String", typeof(string));
        table.Columns.Add("Bool", typeof(bool));
        table.Columns.Add("Double", typeof(double));
        table.Columns.Add("DateTime", typeof(DateTime));
        table.Columns.Add("Long", typeof(long));

        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        table.InnerDataTable.Rows.Add(42, "hello", true, 3.14, dt, 9876543210L);
        table.InnerDataTable.AcceptChanges();

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Int"].Should().Be(42);
        result.Rows[0]["String"].Should().Be("hello");
        result.Rows[0]["Bool"].Should().Be(true);
        result.Rows[0]["Double"].Should().Be(3.14);
        result.Rows[0]["Long"].Should().Be(9876543210L);
    }

    [Fact]
    public void Should_Handle_Null_Value()
    {
        var settings = CreateSettings();
        var result = JsonConvert.DeserializeObject<AsyncDataTable>("null", settings);
        result.Should().BeNull();
    }

    [Fact]
    public void Should_Serialize_Null_Value()
    {
        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject((AsyncDataTable?)null, settings);
        json.Should().Be("null");
    }

    [Fact]
    public void Should_Preserve_Table_Properties()
    {
        var table = new AsyncDataTable("Test");
        table.CaseSensitive = true;
        table.MinimumCapacity = 100;
        table.Namespace = "http://test.com";
        table.Prefix = "t";
        table.Columns.Add("Id", typeof(int));

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.CaseSensitive.Should().BeTrue();
        result.MinimumCapacity.Should().Be(100);
        result.Namespace.Should().Be("http://test.com");
        result.Prefix.Should().Be("t");
    }

    [Fact]
    public void Should_Handle_Mixed_Row_States()
    {
        var table = new AsyncDataTable("Mixed");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));

        table.InnerDataTable.Rows.Add(1, "Unchanged");
        table.InnerDataTable.Rows.Add(2, "ToModify");
        table.InnerDataTable.Rows.Add(3, "ToDelete");
        table.InnerDataTable.AcceptChanges();

        table.InnerDataTable.Rows[1]["Name"] = "Modified";
        table.InnerDataTable.Rows[2].Delete();
        table.InnerDataTable.Rows.Add(4, "Added");

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows.Count.Should().Be(4);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
        result.Rows[2].RowState.Should().Be(DataRowState.Deleted);
        result.Rows[3].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Should_Serialize_Proposed_Version_When_Row_In_BeginEdit()
    {
        var table = new AsyncDataTable("T");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.InnerDataTable.Rows.Add(1, "Original");
        table.InnerDataTable.AcceptChanges();
        table.InnerDataTable.Rows[0].BeginEdit();
        table.InnerDataTable.Rows[0]["Name"] = "Proposed";
        // EndEdit NOT called — row has DataRowVersion.Proposed

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        json.Should().Contain("Proposed");
        json.Should().NotContain("\"Original\"");

        // Deserialize and verify the proposed value is preserved
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;
        result.Rows[0]["Name"].Should().Be("Proposed");
    }

    [Fact]
    public void Should_Deserialize_Detached_RowState_As_Added()
    {
        // Build the JSON dynamically to avoid fragile string-replace and hardcoded type names
        var intTypeName = typeof(int).AssemblyQualifiedName!;
        var json = $$"""
            {
              "CaseSensitive": false,
              "DisplayExpression": "",
              "Locale": "",
              "MinimumCapacity": 50,
              "Namespace": "",
              "Prefix": "",
              "RemotingFormat": 0,
              "TableName": "T",
              "Columns": [
                {
                  "AllowDBNull": true,
                  "AutoIncrement": false,
                  "AutoIncrementSeed": 0,
                  "AutoIncrementStep": 1,
                  "Caption": "Id",
                  "ColumnMapping": 1,
                  "ColumnName": "Id",
                  "DataType": "{{intTypeName}}",
                  "DefaultValue": null,
                  "Expression": "",
                  "ExtendedProperties": [],
                  "MaxLength": -1,
                  "Namespace": "",
                  "Prefix": "",
                  "ReadOnly": false
                }
              ],
              "Constraints": [],
              "Rows": [
                {
                  "OriginalRow": null,
                  "Id": 1,
                  "RowState": 64
                }
              ]
            }
            """;

        var settings = CreateSettings();
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        result.Rows[0]["Id"].Should().Be(1);
        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Should_Handle_Column_Properties()
    {
        var table = new AsyncDataTable("Test");
        var col = table.Columns.Add("Id", typeof(int));
        col.AutoIncrement = true;
        col.AutoIncrementSeed = 10;
        col.AutoIncrementStep = 5;
        col.Caption = "Identifier";
        col.AllowDBNull = false;

        var settings = CreateSettings();
        var json = JsonConvert.SerializeObject(table, settings);
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, settings)!;

        var resultCol = result.Columns["Id"]!;
        resultCol.AutoIncrement.Should().BeTrue();
        resultCol.AutoIncrementSeed.Should().Be(10);
        resultCol.AutoIncrementStep.Should().Be(5);
        resultCol.Caption.Should().Be("Identifier");
        resultCol.AllowDBNull.Should().BeFalse();
    }
}

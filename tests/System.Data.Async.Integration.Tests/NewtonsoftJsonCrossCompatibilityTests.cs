using System.Data.Async.Converters;
using System.Data.Async.DataSet;
using FluentAssertions;
using Json.Net.DataSetConverters;
using Newtonsoft.Json;
using Xunit;

namespace System.Data.Async.Integration.Tests;

public class NewtonsoftJsonCrossCompatibilityTests
{
    private static JsonSerializerSettings ReferenceSettings() => new JsonSerializerSettings
    {
        Converters = { new DataTableConverter(), new DataSetConverter() }
    };

    private static JsonSerializerSettings AsyncSettings() => new JsonSerializerSettings
    {
        Converters = { new AsyncDataTableConverter(), new AsyncDataSetConverter() }
    };

    [Fact]
    public void DataTable_Reference_To_AsyncDataTable()
    {
        var dt = new DataTable("Users");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Alice");
        dt.Rows.Add(2, "Bob");
        dt.AcceptChanges();
        dt.Rows[1]["Name"] = "Robert";

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.TableName.Should().Be("Users");
        result.Rows.Count.Should().Be(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Rows[1]["Name"].Should().Be("Robert");
        result.Rows[1]["Name", DataRowVersion.Original].Should().Be("Bob");
        result.Rows[1].RowState.Should().Be(DataRowState.Modified);
    }

    [Fact]
    public void AsyncDataTable_To_Reference_DataTable()
    {
        var table = new AsyncDataTable("Products");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Price", typeof(decimal));
        table.Rows.Add(1, 29.99m);
        table.AcceptChanges();

        var json = JsonConvert.SerializeObject(table, AsyncSettings());
        var result = JsonConvert.DeserializeObject<DataTable>(json, ReferenceSettings())!;

        result.TableName.Should().Be("Products");
        result.Rows.Count.Should().Be(1);
        result.Rows[0]["Price"].Should().Be(29.99m);
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void All_RowStates_Round_Trip_Via_Reference()
    {
        var dt = new DataTable("States");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Val", typeof(string));
        dt.Rows.Add(1, "Unchanged");
        dt.Rows.Add(2, "WillModify");
        dt.Rows.Add(3, "WillDelete");
        dt.AcceptChanges();
        dt.Rows.Add(4, "Added");
        dt.Rows[1]["Val"] = "Modified";
        dt.Rows[2].Delete();

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Select("Id = 1")[0].RowState.Should().Be(DataRowState.Unchanged);
        result.Select("Id = 2")[0].RowState.Should().Be(DataRowState.Modified);
        result.Select("Id = 2")[0]["Val"].Should().Be("Modified");
        result.Select("Id = 2")[0]["Val", DataRowVersion.Original].Should().Be("WillModify");
        result.Select("Id = 4")[0].RowState.Should().Be(DataRowState.Added);
        var deletedRows = result.InnerDataTable.Select("Id = 3", null, DataViewRowState.Deleted);
        deletedRows.Should().HaveCount(1);
        deletedRows[0].RowState.Should().Be(DataRowState.Deleted);
    }

    [Fact]
    public void Added_Row_Preserves_State_Round_Trip()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Rows.Add(1);

        var json = JsonConvert.SerializeObject(dt, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0].RowState.Should().Be(DataRowState.Added);
    }

    [Fact]
    public void Proposed_Version_Serializes_Current_Values()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(1, "Original");
        dt.AcceptChanges();
        dt.Rows[0].BeginEdit();
        dt.Rows[0]["Name"] = "Proposed";
        // EndEdit NOT called — row has DataRowVersion.Proposed

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0]["Name"].Should().Be("Proposed");
        result.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
    }

    [Fact]
    public void All_Primitive_Types_Round_Trip()
    {
        var dt = new DataTable("Types");
        dt.Columns.Add("Bool", typeof(bool));
        dt.Columns.Add("Int", typeof(int));
        dt.Columns.Add("Long", typeof(long));
        dt.Columns.Add("Float", typeof(float));
        dt.Columns.Add("Double", typeof(double));
        dt.Columns.Add("Decimal", typeof(decimal));
        dt.Columns.Add("String", typeof(string));
        dt.Columns.Add("DateTime", typeof(DateTime));
        dt.Columns.Add("Guid", typeof(Guid));
        dt.Columns.Add("Bytes", typeof(byte[]));

        var guid = Guid.NewGuid();
        var now = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        dt.Rows.Add(true, 42, 9999999999L, 3.14f, 2.718281828, 12345.6789012345678901234567m, "hello", now, guid, bytes);
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;
        var row = result.Rows[0];

        row["Bool"].Should().Be(true);
        row["Int"].Should().Be(42);
        row["Long"].Should().Be(9999999999L);
        ((float)row["Float"]).Should().BeApproximately(3.14f, 0.001f);
        row["Decimal"].Should().Be(12345.6789012345678901234567m);
        row["String"].Should().Be("hello");
        row["Guid"].Should().Be(guid);
        ((byte[])row["Bytes"]).Should().Equal(bytes);
    }

    [Fact]
    public void Null_Values_Round_Trip()
    {
        var dt = new DataTable("Nulls");
        dt.Columns.Add("Id", typeof(int));
        var nameCol = dt.Columns.Add("Name", typeof(string));
        nameCol.AllowDBNull = true;
        dt.Rows.Add(1, DBNull.Value);
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0]["Name"].Should().Be(DBNull.Value);
    }

    [Fact]
    public void AutoIncrement_Column_Restores_Id()
    {
        var dt = new DataTable("AutoInc");
        var idCol = dt.Columns.Add("Id", typeof(int));
        idCol.AutoIncrement = true;
        idCol.AutoIncrementSeed = 100;
        idCol.AutoIncrementStep = 1;
        dt.Columns.Add("Name", typeof(string));
        dt.Rows.Add(null, "Alice");
        dt.Rows.Add(null, "Bob");
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.Rows[0]["Id"].Should().Be(100);
        result.Rows[1]["Id"].Should().Be(101);
        result.Columns["Id"]!.AutoIncrement.Should().BeTrue();
    }

    [Fact]
    public void UniqueConstraint_And_PrimaryKey_Round_Trip()
    {
        var dt = new DataTable("T");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Code", typeof(string));
        dt.PrimaryKey = [dt.Columns["Id"]!];
        dt.Constraints.Add(new UniqueConstraint("UQ_Code", dt.Columns["Code"]!, isPrimaryKey: false));
        dt.Rows.Add(1, "A");
        dt.AcceptChanges();

        var json = JsonConvert.SerializeObject(new AsyncDataTable(dt), AsyncSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataTable>(json, AsyncSettings())!;

        result.PrimaryKey.Should().HaveCount(1);
        result.PrimaryKey[0].ColumnName.Should().Be("Id");
        bool hasUqCode = false;
        foreach (Constraint c in result.Constraints)
        {
            if (string.Equals(c.ConstraintName, "UQ_Code", StringComparison.Ordinal))
            {
                hasUqCode = true;
                break;
            }
        }
        hasUqCode.Should().BeTrue(because: "a UniqueConstraint named 'UQ_Code' should have been deserialized");
    }

    [Fact]
    public void DataSet_Reference_To_AsyncDataSet()
    {
        var ds = new System.Data.DataSet("TestDS");
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("OrderId", typeof(int));
        orders.Columns.Add("Total", typeof(decimal));
        orders.Rows.Add(1, 99.99m);
        orders.Rows.Add(2, 150.00m);
        ds.AcceptChanges();
        orders.Rows[1]["Total"] = 175.50m;

        var json = JsonConvert.SerializeObject(ds, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, AsyncSettings())!;

        result.DataSetName.Should().Be("TestDS");
        result.Tables["Orders"]!.Rows.Count.Should().Be(2);
        result.Tables["Orders"]!.Rows[1].RowState.Should().Be(DataRowState.Modified);
        ((decimal)result.Tables["Orders"]!.Rows[1]["Total"]).Should().Be(175.50m);
    }

    [Fact]
    public void AsyncDataSet_To_Reference_DataSet()
    {
        var table = new AsyncDataTable("Customers");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "Alice");
        table.AcceptChanges();

        var asyncDs = new AsyncDataSet("MyDS");
        asyncDs.Tables.Add(table.InnerDataTable);

        var json = JsonConvert.SerializeObject(asyncDs, AsyncSettings());
        var result = JsonConvert.DeserializeObject<System.Data.DataSet>(json, ReferenceSettings())!;

        result.DataSetName.Should().Be("MyDS");
        result.Tables["Customers"]!.Rows[0]["Name"].Should().Be("Alice");
    }

    [Fact]
    public void DataSet_With_Relation_Round_Trips()
    {
        var ds = new System.Data.DataSet("Shop");
        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("Id", typeof(int));
        customers.PrimaryKey = [customers.Columns["Id"]!];
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("Id", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.PrimaryKey = [orders.Columns["Id"]!];
        ds.Relations.Add("CustOrders", customers.Columns["Id"]!, orders.Columns["CustomerId"]!);
        customers.Rows.Add(1);
        orders.Rows.Add(100, 1);
        ds.AcceptChanges();

        var json = JsonConvert.SerializeObject(ds, ReferenceSettings());
        var result = JsonConvert.DeserializeObject<AsyncDataSet>(json, AsyncSettings())!;

        result.Relations.Count.Should().Be(1);
        result.Relations["CustOrders"]!.ParentTable.TableName.Should().Be("Customers");
        result.Relations["CustOrders"]!.ChildTable.TableName.Should().Be("Orders");
    }
}

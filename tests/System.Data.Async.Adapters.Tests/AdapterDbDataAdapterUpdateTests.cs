#pragma warning disable CA1001 // Type owns disposable field(s) -- disposed via IAsyncLifetime

using System.Data.Async.DataSet;
using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbDataAdapterUpdateTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AdapterDbConnection _adapterConnection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _adapterConnection = new AdapterDbConnection(_connection);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT, Price REAL)";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private AdapterDbCommand CreateAdapterCommand(string sql)
    {
        var innerCmd = _connection.CreateCommand();
        innerCmd.CommandText = sql;
        return new AdapterDbCommand(innerCmd, _adapterConnection);
    }

    private AdapterDbDataAdapter CreateAdapter()
    {
        var selectCmd = CreateAdapterCommand("SELECT Id, Name, Price FROM Items ORDER BY Id");

        var insertCmd = CreateAdapterCommand("INSERT INTO Items (Id, Name, Price) VALUES (@Id, @Name, @Price)");
        var idParam = insertCmd.InnerCommand.CreateParameter();
        idParam.ParameterName = "@Id";
        idParam.SourceColumn = "Id";
        insertCmd.InnerCommand.Parameters.Add(idParam);
        var nameParam = insertCmd.InnerCommand.CreateParameter();
        nameParam.ParameterName = "@Name";
        nameParam.SourceColumn = "Name";
        insertCmd.InnerCommand.Parameters.Add(nameParam);
        var priceParam = insertCmd.InnerCommand.CreateParameter();
        priceParam.ParameterName = "@Price";
        priceParam.SourceColumn = "Price";
        insertCmd.InnerCommand.Parameters.Add(priceParam);

        var updateCmd = CreateAdapterCommand("UPDATE Items SET Name = @Name, Price = @Price WHERE Id = @Id");
        var updIdParam = updateCmd.InnerCommand.CreateParameter();
        updIdParam.ParameterName = "@Id";
        updIdParam.SourceColumn = "Id";
        updateCmd.InnerCommand.Parameters.Add(updIdParam);
        var updNameParam = updateCmd.InnerCommand.CreateParameter();
        updNameParam.ParameterName = "@Name";
        updNameParam.SourceColumn = "Name";
        updateCmd.InnerCommand.Parameters.Add(updNameParam);
        var updPriceParam = updateCmd.InnerCommand.CreateParameter();
        updPriceParam.ParameterName = "@Price";
        updPriceParam.SourceColumn = "Price";
        updateCmd.InnerCommand.Parameters.Add(updPriceParam);

        var deleteCmd = CreateAdapterCommand("DELETE FROM Items WHERE Id = @Id");
        var delIdParam = deleteCmd.InnerCommand.CreateParameter();
        delIdParam.ParameterName = "@Id";
        delIdParam.SourceColumn = "Id";
        deleteCmd.InnerCommand.Parameters.Add(delIdParam);

        return new AdapterDbDataAdapter(selectCmd)
        {
            InsertCommand = insertCmd,
            UpdateCommand = updateCmd,
            DeleteCommand = deleteCmd
        };
    }

    private async Task<int> GetDbRowCount()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Items";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task UpdateAsync_Inserts_Added_Rows()
    {
        var adapter = CreateAdapter();
        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(double));

        table.Rows.Add(1L, "Widget", 9.99);
        table.Rows.Add(2L, "Gadget", 19.99);

        var affected = await adapter.UpdateAsync(table);

        affected.Should().Be(2);
        (await GetDbRowCount()).Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_Updates_Modified_Rows()
    {
        // Seed data
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = "INSERT INTO Items VALUES (1, 'Widget', 9.99)";
        await seedCmd.ExecuteNonQueryAsync();

        var adapter = CreateAdapter();

        // Fill table then modify
        using var table = new AsyncDataTable("Items");
        await adapter.FillAsync(table);
        table.Rows.Count.Should().Be(1);

        table.Rows[0]["Name"] = "SuperWidget";
        table.Rows[0]["Price"] = 14.99;

        var affected = await adapter.UpdateAsync(table);

        affected.Should().Be(1);

        // Verify in DB
        await using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT Name FROM Items WHERE Id = 1";
        var name = await checkCmd.ExecuteScalarAsync();
        ((string)name!).Should().Be("SuperWidget");
    }

    [Fact]
    public async Task UpdateAsync_Deletes_Deleted_Rows()
    {
        // Seed data
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = "INSERT INTO Items VALUES (1, 'Widget', 9.99), (2, 'Gadget', 19.99)";
        await seedCmd.ExecuteNonQueryAsync();

        var adapter = CreateAdapter();

        // Fill table then delete
        using var table = new AsyncDataTable("Items");
        await adapter.FillAsync(table);
        table.Rows.Count.Should().Be(2);

        table.Rows[0].Delete();

        var affected = await adapter.UpdateAsync(table);

        affected.Should().Be(1);
        (await GetDbRowCount()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Handles_Mixed_Operations()
    {
        // Seed data
        await using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = "INSERT INTO Items VALUES (1, 'Keep', 5.00), (2, 'Modify', 10.00), (3, 'Delete', 15.00)";
        await seedCmd.ExecuteNonQueryAsync();

        var adapter = CreateAdapter();
        using var table = new AsyncDataTable("Items");
        await adapter.FillAsync(table);

        // Modify row 2
        table.Rows[1]["Name"] = "Modified";
        table.Rows[1]["Price"] = 12.00;

        // Delete row 3
        table.Rows[2].Delete();

        // Add row 4
        table.Rows.Add(4L, "Added", 20.00);

        var affected = await adapter.UpdateAsync(table);

        affected.Should().Be(3); // 1 update + 1 delete + 1 insert
        (await GetDbRowCount()).Should().Be(3); // 3 original - 1 deleted + 1 added

        // Verify the data
        await using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT Name FROM Items WHERE Id = 2";
        var name = await checkCmd.ExecuteScalarAsync();
        ((string)name!).Should().Be("Modified");

        checkCmd.CommandText = "SELECT COUNT(*) FROM Items WHERE Id = 3";
        var deletedCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        deletedCount.Should().Be(0);

        checkCmd.CommandText = "SELECT Name FROM Items WHERE Id = 4";
        var addedName = await checkCmd.ExecuteScalarAsync();
        ((string)addedName!).Should().Be("Added");
    }

    [Fact]
    public async Task UpdateAsync_AcceptChanges_When_AcceptChangesDuringUpdate_Is_True()
    {
        var adapter = CreateAdapter();
        adapter.AcceptChangesDuringUpdate = true;

        using var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(double));
        table.Rows.Add(1L, "Widget", 9.99);

        await adapter.UpdateAsync(table);

        table.Rows[0].RowState.Should().Be(DataRowState.Unchanged);
    }
}

using System.Data.Async.DataSet;
using System.Globalization;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbDataAdapter : AsyncDataAdapter
{
    public AdapterDbDataAdapter() { }
    public AdapterDbDataAdapter(IAsyncDbCommand selectCommand) => SelectCommand = selectCommand;

    public override async ValueTask<int> FillAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default)
    {
        var selectCommand = SelectCommand ?? throw new InvalidOperationException("SelectCommand is not set.");
        var connection = selectCommand.Connection ?? throw new InvalidOperationException("SelectCommand.Connection is not set.");

        bool openedConnection = false;
        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            openedConnection = true;
        }

        try
        {
            var reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (((IAsyncDisposable)reader).ConfigureAwait(false))
            {
                var count = await dataTable.LoadAsync(reader, AcceptChangesDuringFill ? LoadOption.OverwriteChanges : LoadOption.Upsert, cancellationToken).ConfigureAwait(false);
                if (AcceptChangesDuringFill)
                {
                    await dataTable.AcceptChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return count;
            }
        }
        finally
        {
            if (openedConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    public override async ValueTask<int> FillAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default)
    {
        var selectCommand = SelectCommand ?? throw new InvalidOperationException("SelectCommand is not set.");
        var connection = selectCommand.Connection ?? throw new InvalidOperationException("SelectCommand.Connection is not set.");

        bool openedConnection = false;
        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            openedConnection = true;
        }

        try
        {
            var reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (((IAsyncDisposable)reader).ConfigureAwait(false))
            {
                int totalCount = 0;
                do
                {
                    var tableIndex = dataSet.Tables.Count;
                    var tableName = tableIndex == 0 ? "Table" : "Table" + tableIndex.ToString(CultureInfo.InvariantCulture);

                    var table = new AsyncDataTable(tableName);
                    var count = await table.LoadAsync(reader, AcceptChangesDuringFill ? LoadOption.OverwriteChanges : LoadOption.Upsert, cancellationToken).ConfigureAwait(false);
                    if (AcceptChangesDuringFill)
                    {
                        await table.AcceptChangesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    dataSet.Tables.Add(table);
                    totalCount += count;
                }
                while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

                return totalCount;
            }
        }
        finally
        {
            if (openedConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    public override async ValueTask<int> UpdateAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default)
    {
        return await UpdateRowsAsync(dataTable.InnerDataTable.Rows, dataTable.Columns, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> UpdateAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default)
    {
        int totalAffected = 0;
        foreach (DataTable table in dataSet.Tables)
        {
            totalAffected += await UpdateRowsAsync(table.Rows, table.Columns, cancellationToken).ConfigureAwait(false);
        }

        return totalAffected;
    }

    private async ValueTask<int> UpdateRowsAsync(DataRowCollection rows, DataColumnCollection columns, CancellationToken cancellationToken)
    {
        int affectedRows = 0;

        // Snapshot the rows to avoid collection-modified-during-enumeration when AcceptChanges removes deleted rows
        var rowList = new DataRow[rows.Count];
        rows.CopyTo(rowList, 0);

        foreach (DataRow row in rowList)
        {
            IAsyncDbCommand? command = row.RowState switch
            {
                DataRowState.Added => InsertCommand,
                DataRowState.Modified => UpdateCommand,
                DataRowState.Deleted => DeleteCommand,
                _ => null
            };

            if (command == null)
            {
                continue;
            }

            // Set parameter values from row
            foreach (IDbDataParameter param in command.Parameters)
            {
                if (string.IsNullOrEmpty(param.SourceColumn) || !columns.Contains(param.SourceColumn))
                {
                    continue;
                }

                param.Value = row.RowState == DataRowState.Deleted
                    ? row[param.SourceColumn, DataRowVersion.Original]
                    : row[param.SourceColumn, DataRowVersion.Current];
            }

            affectedRows += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (AcceptChangesDuringUpdate)
            {
                row.AcceptChanges();
            }
        }

        return affectedRows;
    }
}

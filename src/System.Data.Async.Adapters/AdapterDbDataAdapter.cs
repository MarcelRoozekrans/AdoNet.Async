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
                    dataTable.AcceptChanges();
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
                        table.AcceptChanges();
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

    // UpdateAsync - stub for now, will be fully implemented in Task 25
    public override ValueTask<int> UpdateAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UpdateAsync will be implemented in Task 25.");

    public override ValueTask<int> UpdateAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UpdateAsync will be implemented in Task 25.");
}

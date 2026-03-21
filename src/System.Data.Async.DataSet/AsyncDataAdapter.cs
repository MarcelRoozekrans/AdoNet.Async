namespace System.Data.Async.DataSet;

public abstract class AsyncDataAdapter
{
    public IAsyncDbCommand? SelectCommand { get; set; }
    public IAsyncDbCommand? InsertCommand { get; set; }
    public IAsyncDbCommand? UpdateCommand { get; set; }
    public IAsyncDbCommand? DeleteCommand { get; set; }
    public MissingMappingAction MissingMappingAction { get; set; } = MissingMappingAction.Passthrough;
    public MissingSchemaAction MissingSchemaAction { get; set; } = MissingSchemaAction.Add;
    public bool AcceptChangesDuringFill { get; set; } = true;
    public bool AcceptChangesDuringUpdate { get; set; } = true;

    public abstract ValueTask<int> FillAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> FillAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> UpdateAsync(AsyncDataSet dataSet, CancellationToken cancellationToken = default);
    public abstract ValueTask<int> UpdateAsync(AsyncDataTable dataTable, CancellationToken cancellationToken = default);

#pragma warning disable CA2012 // Sync-over-async bridge for backward compatibility
    public int Fill(AsyncDataSet dataSet) => FillAsync(dataSet).GetAwaiter().GetResult();
    public int Fill(AsyncDataTable dataTable) => FillAsync(dataTable).GetAwaiter().GetResult();
    public int Update(AsyncDataSet dataSet) => UpdateAsync(dataSet).GetAwaiter().GetResult();
    public int Update(AsyncDataTable dataTable) => UpdateAsync(dataTable).GetAwaiter().GetResult();
#pragma warning restore CA2012
}

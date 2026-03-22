#pragma warning disable CA2012 // Use ValueTasks correctly -- guarded sync-to-async bridge

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

    // Sync -> async bridge (throws on WASM)
    public int Fill(AsyncDataSet dataSet) { ThrowIfBrowser(nameof(FillAsync)); return FillAsync(dataSet).GetAwaiter().GetResult(); }
    public int Fill(AsyncDataTable dataTable) { ThrowIfBrowser(nameof(FillAsync)); return FillAsync(dataTable).GetAwaiter().GetResult(); }
    public int Update(AsyncDataSet dataSet) { ThrowIfBrowser(nameof(UpdateAsync)); return UpdateAsync(dataSet).GetAwaiter().GetResult(); }
    public int Update(AsyncDataTable dataTable) { ThrowIfBrowser(nameof(UpdateAsync)); return UpdateAsync(dataTable).GetAwaiter().GetResult(); }

    private static void ThrowIfBrowser(string asyncMethodName)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                $"Synchronous operations are not supported on this platform. Use {asyncMethodName}() instead.");
        }
    }
}

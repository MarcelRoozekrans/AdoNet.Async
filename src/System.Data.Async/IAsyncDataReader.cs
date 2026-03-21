namespace System.Data.Async;

public interface IAsyncDataReader : IAsyncDataRecord, IAsyncEnumerable<IAsyncDataRecord>, IAsyncDisposable, IDisposable
{
    int Depth { get; }
    bool IsClosed { get; }
    int RecordsAffected { get; }
    bool HasRows { get; }

    bool Read();
    bool NextResult();
    void Close();
    DataTable GetSchemaTable();

    ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
    ValueTask<DataTable> GetSchemaTableAsync(CancellationToken cancellationToken = default);
}

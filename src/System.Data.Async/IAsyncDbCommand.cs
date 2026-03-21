namespace System.Data.Async;

public interface IAsyncDbCommand : IAsyncDisposable, IDisposable
{
    string CommandText { get; set; }
    int CommandTimeout { get; set; }
    CommandType CommandType { get; set; }
    IAsyncDbConnection? Connection { get; set; }
    IAsyncDbTransaction? Transaction { get; set; }
    IDataParameterCollection Parameters { get; }
    UpdateRowSource UpdatedRowSource { get; set; }

    IAsyncDataReader ExecuteReader();
    IAsyncDataReader ExecuteReader(CommandBehavior behavior);
    int ExecuteNonQuery();
    object? ExecuteScalar();
    void Prepare();
    void Cancel();
    IDbDataParameter CreateParameter();

    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = default);
    ValueTask<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
    ValueTask<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);
    ValueTask PrepareAsync(CancellationToken cancellationToken = default);
}

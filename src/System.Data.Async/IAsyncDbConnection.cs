namespace System.Data.Async;

public interface IAsyncDbConnection : IAsyncDisposable, IDisposable
{
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }

    IAsyncDbTransaction BeginTransaction();
    IAsyncDbTransaction BeginTransaction(IsolationLevel il);
    void ChangeDatabase(string databaseName);
    IAsyncDbCommand CreateCommand();
    void Open();
    void Close();

    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default);
    ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    ValueTask OpenAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
}

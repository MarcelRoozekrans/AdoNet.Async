namespace System.Data.Async;

public interface IAsyncDbConnection : IAsyncDisposable, IDisposable
{
    string ConnectionString { get; set; }
    int ConnectionTimeout { get; }
    string Database { get; }
    ConnectionState State { get; }

    /// <summary>
    /// True if the underlying provider supports <c>DbBatch</c> and
    /// <see cref="CreateBatch"/> can be called without throwing. Mirrors
    /// <see cref="System.Data.Common.DbConnection.CanCreateBatch"/>. Npgsql 6+,
    /// Microsoft.Data.Sqlite 9+, and SqlClient return <c>true</c>; older providers
    /// return <c>false</c>. Source-generator data-access libraries should branch on this
    /// to choose between batched and `;`-joined execution.
    /// </summary>
    bool CanCreateBatch { get; }

    IAsyncDbTransaction BeginTransaction();
    IAsyncDbTransaction BeginTransaction(IsolationLevel il);
    void ChangeDatabase(string databaseName);
    IAsyncDbCommand CreateCommand();

    /// <summary>
    /// Create a new <see cref="IAsyncDbBatch"/> bound to this connection. Throws
    /// <see cref="NotSupportedException"/> if the provider does not support batching —
    /// check <see cref="CanCreateBatch"/> first.
    /// </summary>
    IAsyncDbBatch CreateBatch();
    void Open();
    void Close();

    /// <remarks>
    /// The returned <see cref="ValueTask{TResult}"/> may be backed by a pooled source. Await it once,
    /// and do not block on it with <c>GetAwaiter().GetResult()</c> or <c>Result</c> before it completes;
    /// call <see cref="BeginTransaction()"/> to execute synchronously.
    /// </remarks>
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    /// <remarks>
    /// The returned <see cref="ValueTask{TResult}"/> may be backed by a pooled source. Await it once,
    /// and do not block on it with <c>GetAwaiter().GetResult()</c> or <c>Result</c> before it completes;
    /// call <see cref="BeginTransaction(IsolationLevel)"/> to execute synchronously.
    /// </remarks>
    ValueTask<IAsyncDbTransaction> BeginTransactionAsync(IsolationLevel il, CancellationToken cancellationToken = default);
    ValueTask ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    ValueTask OpenAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync();
}

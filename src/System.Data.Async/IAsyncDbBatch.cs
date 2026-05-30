namespace System.Data.Async;

/// <summary>
/// Async-aware mirror of <see cref="System.Data.Common.DbBatch"/>. Represents a batch
/// of one or more <see cref="IAsyncDbBatchCommand"/> statements executed against a
/// connection in a single provider round-trip.
/// </summary>
/// <remarks>
/// <para>
/// Provider support is uneven: Npgsql 6+ implements <c>DbBatch</c> natively (sends all
/// statements in one Extended-Query exchange), Microsoft.Data.Sqlite 9+ supports it,
/// SqlClient supports it via <c>SqlBatch</c>. Check
/// <see cref="IAsyncDbConnection.CanCreateBatch"/> before calling
/// <see cref="IAsyncDbConnection.CreateBatch"/> — providers that do not support batching
/// throw <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Source-generator data-access libraries (e.g. ZeroAlloc.ORM) consume this interface to
/// emit multi-statement reads (`head + lines`-shaped) as a single batch rather than the
/// `;`-joined-SQL + NextResultAsync workaround. The batch shape preserves typed
/// parameters per statement, which `;`-joined SQL does not.
/// </para>
/// </remarks>
public interface IAsyncDbBatch : IAsyncDisposable, IDisposable
{
    IList<IAsyncDbBatchCommand> BatchCommands { get; }
    IAsyncDbConnection? Connection { get; set; }
    IAsyncDbTransaction? Transaction { get; set; }
    int Timeout { get; set; }

    IAsyncDbBatchCommand CreateBatchCommand();

    IAsyncDataReader ExecuteReader();
    int ExecuteNonQuery();
    object? ExecuteScalar();
    void Prepare();
    void Cancel();

    ValueTask<IAsyncDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default);
    ValueTask<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
    ValueTask<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);
    ValueTask PrepareAsync(CancellationToken cancellationToken = default);
}

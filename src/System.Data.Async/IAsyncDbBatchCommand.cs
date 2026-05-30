namespace System.Data.Async;

/// <summary>
/// Async-aware mirror of <see cref="System.Data.Common.DbBatchCommand"/>. Represents a
/// single SQL statement (with its own parameter set) within a batch executed against
/// <see cref="IAsyncDbBatch"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="IAsyncDbCommand"/>, a batch command does not own its connection or
/// transaction — those are inherited from the parent <see cref="IAsyncDbBatch"/>. This
/// mirrors the BCL contract and keeps the wrapper a thin pass-through.
/// </remarks>
public interface IAsyncDbBatchCommand
{
    string CommandText { get; set; }
    CommandType CommandType { get; set; }
    IDataParameterCollection Parameters { get; }
    int RecordsAffected { get; }

    IDbDataParameter CreateParameter();
}

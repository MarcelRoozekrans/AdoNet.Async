namespace System.Data.Async;

public interface IAsyncDbTransaction : IAsyncDisposable, IDisposable
{
    IAsyncDbConnection Connection { get; }
    IsolationLevel IsolationLevel { get; }

    void Commit();
    void Rollback();

    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}

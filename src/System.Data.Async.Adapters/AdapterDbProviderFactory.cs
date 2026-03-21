using System.Data.Common;

namespace System.Data.Async.Adapters;

public sealed class AdapterDbProviderFactory : IAsyncDbProviderFactory
{
    private readonly DbProviderFactory _inner;

    public AdapterDbProviderFactory(DbProviderFactory inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IAsyncDbConnection CreateConnection()
        => new AdapterDbConnection(_inner.CreateConnection()!);

    public IAsyncDbCommand CreateCommand()
        => new AdapterDbCommand(_inner.CreateCommand()!, null);

    public IDbDataParameter CreateParameter()
        => _inner.CreateParameter()!;
}

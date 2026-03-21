using System.Data.Common;

namespace System.Data.Async.Adapters;

public static class DbConnectionExtensions
{
    public static IAsyncDbConnection AsAsync(this DbConnection connection)
        => new AdapterDbConnection(connection);
}

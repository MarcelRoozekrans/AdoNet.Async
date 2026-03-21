using System.Data.Common;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public interface ITestDatabaseProvider
{
    DbConnection CreateRawConnection();
    IAsyncDbConnection CreateAsyncConnection();
    string ProviderName { get; }
}

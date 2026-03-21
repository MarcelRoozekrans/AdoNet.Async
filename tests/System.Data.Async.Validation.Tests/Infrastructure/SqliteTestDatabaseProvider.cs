using System.Data.Async.Adapters;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public sealed class SqliteTestDatabaseProvider : ITestDatabaseProvider
{
    private readonly string _connectionString;

    public SqliteTestDatabaseProvider(string databaseName)
    {
        _connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
    }

    public string ProviderName => "Microsoft.Data.Sqlite";

    public DbConnection CreateRawConnection() => new SqliteConnection(_connectionString);

    public IAsyncDbConnection CreateAsyncConnection() => new SqliteConnection(_connectionString).AsAsync();
}

using System.Data.Async.Adapters;
using System.Data.Common;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;

namespace System.Data.Async.Benchmarks.Infrastructure;

public abstract class BenchmarkBase : IDisposable
{
    private SqliteConnection _keepAlive = null!;
    protected DbConnection RawConnection { get; set; } = null!;
    protected IAsyncDbConnection AsyncConnection { get; set; } = null!;

    private readonly string _dbName;
    private string ConnectionString => $"Data Source={_dbName};Mode=Memory;Cache=Shared";

    protected BenchmarkBase()
    {
        _dbName = GetType().Name;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER,
                Balance REAL,
                CreatedAt TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Product TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                Price REAL NOT NULL,
                OrderDate TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            """;
        cmd.ExecuteNonQuery();

        for (int i = 1; i <= 50; i++)
        {
            using var ins = _keepAlive.CreateCommand();
            ins.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT OR IGNORE INTO Users (Id, Name, Email, Age, Balance, CreatedAt, IsActive) VALUES ({0}, 'User{0}', 'user{0}@test.com', {1}, {2}, '{3}', {4})",
                i, 20 + (i % 40),
                (i * 100.50).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture),
                i % 3 == 0 ? 0 : 1);
            ins.ExecuteNonQuery();
        }
        for (int i = 1; i <= 200; i++)
        {
            using var ins = _keepAlive.CreateCommand();
            ins.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT OR IGNORE INTO Orders (Id, UserId, Product, Quantity, Price, OrderDate) VALUES ({0}, {1}, 'Product{0}', {2}, {3}, '{4}')",
                i, ((i - 1) % 50) + 1, 1 + (i % 10),
                (i * 9.99).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture));
            ins.ExecuteNonQuery();
        }

        RawConnection = new SqliteConnection(ConnectionString);
        RawConnection.Open();
        AsyncConnection = new SqliteConnection(ConnectionString).AsAsync();
        AsyncConnection.OpenAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            RawConnection?.Dispose();
            (AsyncConnection as IDisposable)?.Dispose();
            _keepAlive?.Dispose();
        }
    }
}

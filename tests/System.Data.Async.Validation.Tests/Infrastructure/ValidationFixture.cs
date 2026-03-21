using System.Data.Common;
using System.Globalization;
using Xunit;

namespace System.Data.Async.Validation.Tests.Infrastructure;

public sealed class ValidationFixture : IAsyncLifetime
{
    private DbConnection? _keepAlive;

    public ITestDatabaseProvider Provider { get; } = new SqliteTestDatabaseProvider("ValidationTests");

    public async Task InitializeAsync()
    {
        _keepAlive = Provider.CreateRawConnection();
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
            using var insertCmd = _keepAlive.CreateCommand();
            insertCmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Users (Name, Email, Age, Balance, CreatedAt, IsActive) VALUES ('User{0}', 'user{0}@test.com', {1}, {2}, '{3}', {4})",
                i,
                20 + (i % 40),
                (i * 100.50).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture),
                i % 3 == 0 ? 0 : 1);
            insertCmd.ExecuteNonQuery();
        }

        for (int i = 1; i <= 200; i++)
        {
            using var insertCmd = _keepAlive.CreateCommand();
            insertCmd.CommandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO Orders (UserId, Product, Quantity, Price, OrderDate) VALUES ({0}, 'Product{1}', {2}, {3}, '{4}')",
                ((i - 1) % 50) + 1,
                i,
                1 + (i % 10),
                (i * 9.99).ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.AddDays(-i).ToString("o", CultureInfo.InvariantCulture));
            insertCmd.ExecuteNonQuery();
        }

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            _keepAlive.Close();
            _keepAlive.Dispose();
        }
        await Task.CompletedTask;
    }
}

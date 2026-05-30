// NativeAOT smoke test for AdoNet.Async core packages.
//
// Goals:
//   1. Prove the consumer-side `AsAsync()` + `AddAsyncData` extension paths publish
//      cleanly under `PublishAot=true` against Microsoft.Data.Sqlite (in-memory).
//   2. Exercise the read-loop shapes a downstream source-gen ORM (ZeroAlloc.ORM)
//      would emit: parameter binding, single-result, multi-result via NextResultAsync,
//      and IAsyncEnumerable enumeration.
//   3. Fail (non-zero exit) on any unexpected behavior — the CI workflow treats
//      success purely as "trim/AOT warnings stayed at zero AND the binary ran end-to-end".
//
// Deliberately hermetic: no Docker, no disk I/O, no network. SQLite `:memory:` is
// AOT-clean per Microsoft's docs.

using System.Data;
using System.Data.Async;
using System.Data.Async.Adapters;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

await ExerciseDirectAdapterPathAsync().ConfigureAwait(false);
await ExerciseDiPathAsync().ConfigureAwait(false);
await ExerciseMultiResultSetAsync().ConfigureAwait(false);

Console.WriteLine("AOT smoke test passed.");
return 0;

static async ValueTask ExerciseDirectAdapterPathAsync()
{
    using var raw = new SqliteConnection("Data Source=:memory:");
    IAsyncDbConnection connection = raw.AsAsync();
    await connection.OpenAsync().ConfigureAwait(false);

    await using (var ddl = connection.CreateCommand())
    {
        ddl.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Price NUMERIC NOT NULL);";
        await ddl.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    await using (var insert = connection.CreateCommand())
    {
        insert.CommandText = "INSERT INTO Items (Name, Price) VALUES (@name, @price);";
        var pName = insert.CreateParameter();
        pName.ParameterName = "@name";
        pName.Value = "widget";
        insert.Parameters.Add(pName);
        var pPrice = insert.CreateParameter();
        pPrice.ParameterName = "@price";
        pPrice.Value = 19.95m;
        insert.Parameters.Add(pPrice);

        var rows = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
        if (rows != 1)
        {
            throw new InvalidOperationException($"Expected 1 inserted row, got {rows}.");
        }
    }

    await using (var select = connection.CreateCommand())
    {
        select.CommandText = "SELECT Id, Name, Price FROM Items WHERE Name = @name;";
        var pName = select.CreateParameter();
        pName.ParameterName = "@name";
        pName.Value = "widget";
        select.Parameters.Add(pName);

        await using var reader = await select.ExecuteReaderAsync().ConfigureAwait(false);
        var count = 0;
        // Cast disambiguates ConfigureAwait — IAsyncDataReader implements both
        // IAsyncDisposable AND IAsyncEnumerable<IAsyncDataRecord>, so the bare call
        // is CS0121 ambiguous.
        await foreach (var record in ((IAsyncEnumerable<IAsyncDataRecord>)reader).ConfigureAwait(false))
        {
            var id = record.GetInt32(0);
            var name = record.GetString(1);
            var price = record.GetDecimal(2);
            if (id <= 0 || !string.Equals(name, "widget", StringComparison.Ordinal) || price != 19.95m)
            {
                throw new InvalidOperationException($"Unexpected row: ({id}, {name}, {price}).");
            }
            count++;
        }
        if (count != 1)
        {
            throw new InvalidOperationException($"Expected 1 row, read {count}.");
        }
    }

    await connection.CloseAsync().ConfigureAwait(false);
}

static async ValueTask ExerciseDiPathAsync()
{
    var services = new ServiceCollection();
    services.AddAsyncData(SqliteFactory.Instance);
    await using var provider = services.BuildServiceProvider();

    var factory = provider.GetRequiredService<IAsyncDbProviderFactory>();
    using var raw = new SqliteConnection("Data Source=:memory:");
    IAsyncDbConnection connection = raw.AsAsync();
    await connection.OpenAsync().ConfigureAwait(false);

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT 1;";
    var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
    var asLong = Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture);
    if (asLong != 1L)
    {
        throw new InvalidOperationException($"Expected scalar 1, got {asLong}.");
    }
    if (factory is null)
    {
        throw new InvalidOperationException("IAsyncDbProviderFactory not resolved from DI.");
    }
}

static async ValueTask ExerciseMultiResultSetAsync()
{
    using var raw = new SqliteConnection("Data Source=:memory:");
    IAsyncDbConnection connection = raw.AsAsync();
    await connection.OpenAsync().ConfigureAwait(false);

    await using (var setup = connection.CreateCommand())
    {
        setup.CommandText =
            "CREATE TABLE Heads (Id INTEGER PRIMARY KEY, Total NUMERIC NOT NULL);" +
            "CREATE TABLE Lines (Id INTEGER PRIMARY KEY, HeadId INTEGER NOT NULL, Sku TEXT NOT NULL);" +
            "INSERT INTO Heads (Id, Total) VALUES (1, 42.00);" +
            "INSERT INTO Lines (HeadId, Sku) VALUES (1, 'SKU-A'), (1, 'SKU-B');";
        await setup.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    await using var read = connection.CreateCommand();
    read.CommandText =
        "SELECT Id, Total FROM Heads WHERE Id = @id;" +
        "SELECT Sku FROM Lines WHERE HeadId = @id;";
    var pId = read.CreateParameter();
    pId.ParameterName = "@id";
    pId.Value = 1L;
    read.Parameters.Add(pId);

    await using var reader = await read.ExecuteReaderAsync().ConfigureAwait(false);

    if (!await reader.ReadAsync().ConfigureAwait(false))
    {
        throw new InvalidOperationException("Missing head row in first result set.");
    }
    var headId = reader.GetInt32(0);
    var total = reader.GetDecimal(1);
    if (headId != 1 || total != 42m)
    {
        throw new InvalidOperationException($"Unexpected head row: ({headId}, {total}).");
    }

    if (!await reader.NextResultAsync().ConfigureAwait(false))
    {
        throw new InvalidOperationException("Second result set not present.");
    }

    var skus = new List<string>();
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        skus.Add(reader.GetString(0));
    }
    if (skus.Count != 2 ||
        !string.Equals(skus[0], "SKU-A", StringComparison.Ordinal) ||
        !string.Equals(skus[1], "SKU-B", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Unexpected lines: [{string.Join(", ", skus)}].");
    }
}

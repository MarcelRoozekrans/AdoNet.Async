using System.Data.Async.Validation.Tests.Infrastructure;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class TransactionParityTests
{
    private readonly ValidationFixture _fixture;

    public TransactionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Commit_Persists_Data_Same_As_Raw()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCreate = raw.CreateCommand();
        rawCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxCommitRaw (Id INTEGER PRIMARY KEY, Val TEXT)";
        rawCreate.ExecuteNonQuery();
        using var rawTx = raw.BeginTransaction();
        using var rawIns = raw.CreateCommand();
        rawIns.Transaction = rawTx;
        rawIns.CommandText = "INSERT INTO TxCommitRaw VALUES (1, 'committed')";
        rawIns.ExecuteNonQuery();
        rawTx.Commit();
        using var rawCheck = raw.CreateCommand();
        rawCheck.CommandText = "SELECT COUNT(*) FROM TxCommitRaw";
        var rawCount = Convert.ToInt64(rawCheck.ExecuteScalar(), CultureInfo.InvariantCulture);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCreate = async_.CreateCommand();
        asyncCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxCommitAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await asyncCreate.ExecuteNonQueryAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.Transaction = asyncTx;
        asyncIns.CommandText = "INSERT INTO TxCommitAsync VALUES (1, 'committed')";
        await asyncIns.ExecuteNonQueryAsync();
        await asyncTx.CommitAsync();
        var asyncCheck = async_.CreateCommand();
        asyncCheck.CommandText = "SELECT COUNT(*) FROM TxCommitAsync";
        var asyncCount = Convert.ToInt64(await asyncCheck.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        asyncCount.Should().Be(rawCount);
    }

    [Fact]
    public async Task Rollback_Reverts_Data_Same_As_Raw()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawCreate = raw.CreateCommand();
        rawCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxRollbackRaw (Id INTEGER PRIMARY KEY, Val TEXT)";
        rawCreate.ExecuteNonQuery();
        using var rawTx = raw.BeginTransaction();
        using var rawIns = raw.CreateCommand();
        rawIns.Transaction = rawTx;
        rawIns.CommandText = "INSERT INTO TxRollbackRaw VALUES (1, 'rolled-back')";
        rawIns.ExecuteNonQuery();
        rawTx.Rollback();
        using var rawCheck = raw.CreateCommand();
        rawCheck.CommandText = "SELECT COUNT(*) FROM TxRollbackRaw";
        var rawCount = Convert.ToInt64(rawCheck.ExecuteScalar(), CultureInfo.InvariantCulture);

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        var asyncCreate = async_.CreateCommand();
        asyncCreate.CommandText = "CREATE TABLE IF NOT EXISTS TxRollbackAsync (Id INTEGER PRIMARY KEY, Val TEXT)";
        await asyncCreate.ExecuteNonQueryAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIns = async_.CreateCommand();
        asyncIns.Transaction = asyncTx;
        asyncIns.CommandText = "INSERT INTO TxRollbackAsync VALUES (1, 'rolled-back')";
        await asyncIns.ExecuteNonQueryAsync();
        await asyncTx.RollbackAsync();
        var asyncCheck = async_.CreateCommand();
        asyncCheck.CommandText = "SELECT COUNT(*) FROM TxRollbackAsync";
        var asyncCount = Convert.ToInt64(await asyncCheck.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        asyncCount.Should().Be(rawCount);
        asyncCount.Should().Be(0);
    }

    [Fact]
    public async Task IsolationLevel_Matches()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.Open();
        using var rawTx = raw.BeginTransaction();
        var rawIso = rawTx.IsolationLevel;
        rawTx.Rollback();

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        await async_.OpenAsync();
        await using var asyncTx = await async_.BeginTransactionAsync();
        var asyncIso = asyncTx.IsolationLevel;
        await asyncTx.RollbackAsync();

        asyncIso.Should().Be(rawIso);
    }
}

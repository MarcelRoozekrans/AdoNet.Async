using System.Data.Async.Adapters.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

/// <summary>
/// An adapter's synchronous members call the provider's own synchronous members, whatever static
/// type the caller uses. Through the base class or an interface they used to fall back to the
/// base class's sync-over-async bridge, which blocks on the provider's async member and throws on
/// the browser even when the provider has a native synchronous one.
/// </summary>
public class AdapterSyncCallTests
{
    public static TheoryData<string> Views() => ["adapter", "base class", "interface"];

    [Theory]
    [MemberData(nameof(Views))]
    public void Reader_sync_members_call_the_provider_sync_members(string view)
    {
        var inner = new SyncRecordingDbDataReader();
        var adapter = new AdapterDbDataReader(inner);

        switch (view)
        {
            case "adapter":
                adapter.Read().Should().BeTrue();
                adapter.NextResult().Should().BeTrue();
                adapter.Close();
                break;
            case "base class":
                var reader = (AsyncDbDataReader)adapter;
                reader.Read().Should().BeTrue();
                reader.NextResult().Should().BeTrue();
                reader.Close();
                break;
            default:
                ((IAsyncDataReader)adapter).Read().Should().BeTrue();
                ((IAsyncDataReader)adapter).NextResult().Should().BeTrue();
                ((IAsyncDataReader)adapter).Close();
                break;
        }

        inner.ReadCalls.Should().Be(1);
        inner.NextResultCalls.Should().Be(1);
        inner.CloseCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Connection_sync_members_call_the_provider_sync_members(string view)
    {
        var inner = new SyncRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);

        switch (view)
        {
            case "adapter":
                adapter.Open();
                adapter.Close();
                break;
            case "base class":
                ((AsyncDbConnection)adapter).Open();
                ((AsyncDbConnection)adapter).Close();
                break;
            default:
                ((IAsyncDbConnection)adapter).Open();
                ((IAsyncDbConnection)adapter).Close();
                break;
        }

        inner.OpenCalls.Should().Be(1);
        inner.CloseCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Transaction_sync_members_call_the_provider_sync_members(string view)
    {
        var connection = new SyncRecordingDbConnection();
        var inner = new SyncRecordingDbTransaction(connection);
        var adapter = new AdapterDbTransaction(inner, new AdapterDbConnection(connection));

        switch (view)
        {
            case "adapter":
                adapter.Commit();
                adapter.Rollback();
                break;
            case "base class":
                ((AsyncDbTransaction)adapter).Commit();
                ((AsyncDbTransaction)adapter).Rollback();
                break;
            default:
                ((IAsyncDbTransaction)adapter).Commit();
                ((IAsyncDbTransaction)adapter).Rollback();
                break;
        }

        inner.CommitCalls.Should().Be(1);
        inner.RollbackCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Command_sync_members_call_the_provider_sync_members(string view)
    {
        var inner = new SyncRecordingDbCommand();
        var adapter = new AdapterDbCommand(inner, connection: null);
        IAsyncDataReader first;
        IAsyncDataReader second;

        switch (view)
        {
            case "adapter":
                first = adapter.ExecuteReader();
                second = adapter.ExecuteReader(CommandBehavior.SingleRow);
                adapter.ExecuteNonQuery().Should().Be(1);
                adapter.ExecuteScalar().Should().Be("scalar");
                adapter.Prepare();
                break;
            case "base class":
                var command = (AsyncDbCommand)adapter;
                first = command.ExecuteReader();
                second = command.ExecuteReader(CommandBehavior.SingleRow);
                command.ExecuteNonQuery().Should().Be(1);
                command.ExecuteScalar().Should().Be("scalar");
                command.Prepare();
                break;
            default:
                first = ((IAsyncDbCommand)adapter).ExecuteReader();
                second = ((IAsyncDbCommand)adapter).ExecuteReader(CommandBehavior.SingleRow);
                ((IAsyncDbCommand)adapter).ExecuteNonQuery().Should().Be(1);
                ((IAsyncDbCommand)adapter).ExecuteScalar().Should().Be("scalar");
                ((IAsyncDbCommand)adapter).Prepare();
                break;
        }

        first.Should().BeOfType<AdapterDbDataReader>();
        second.Should().BeOfType<AdapterDbDataReader>();
        inner.ExecuteReaderCalls.Should().Be(2);
        inner.LastBehavior.Should().Be(CommandBehavior.SingleRow);
        inner.ExecuteNonQueryCalls.Should().Be(1);
        inner.ExecuteScalarCalls.Should().Be(1);
        inner.PrepareCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Connection_transaction_and_database_members_call_the_provider_sync_members(string view)
    {
        var inner = new SyncRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);
        IAsyncDbTransaction first;
        IAsyncDbTransaction second;

        switch (view)
        {
            case "adapter":
                first = adapter.BeginTransaction();
                second = adapter.BeginTransaction(IsolationLevel.Serializable);
                adapter.ChangeDatabase("other");
                break;
            case "base class":
                var connection = (AsyncDbConnection)adapter;
                first = connection.BeginTransaction();
                second = connection.BeginTransaction(IsolationLevel.Serializable);
                connection.ChangeDatabase("other");
                break;
            default:
                first = ((IAsyncDbConnection)adapter).BeginTransaction();
                second = ((IAsyncDbConnection)adapter).BeginTransaction(IsolationLevel.Serializable);
                ((IAsyncDbConnection)adapter).ChangeDatabase("other");
                break;
        }

        first.Should().BeOfType<AdapterDbTransaction>().Which.Connection.Should().BeSameAs(adapter);
        second.Should().BeOfType<AdapterDbTransaction>();
        inner.BeginTransactionCalls.Should().Be(2);
        inner.LastIsolationLevel.Should().Be(IsolationLevel.Serializable);
        inner.LastDatabase.Should().Be("other");
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Batch_sync_members_call_the_provider_sync_members(string view)
    {
        var inner = new SyncRecordingDbBatch();
        var adapter = new AdapterDbBatch(inner, connection: null);
        IAsyncDataReader reader;

        switch (view)
        {
            case "adapter":
                reader = adapter.ExecuteReader();
                adapter.ExecuteNonQuery().Should().Be(7);
                adapter.ExecuteScalar().Should().Be("scalar");
                adapter.Prepare();
                break;
            case "base class":
                var batch = (AsyncDbBatch)adapter;
                reader = batch.ExecuteReader();
                batch.ExecuteNonQuery().Should().Be(7);
                batch.ExecuteScalar().Should().Be("scalar");
                batch.Prepare();
                break;
            default:
                reader = ((IAsyncDbBatch)adapter).ExecuteReader();
                ((IAsyncDbBatch)adapter).ExecuteNonQuery().Should().Be(7);
                ((IAsyncDbBatch)adapter).ExecuteScalar().Should().Be("scalar");
                ((IAsyncDbBatch)adapter).Prepare();
                break;
        }

        reader.Should().BeOfType<AdapterDbDataReader>();
        inner.ExecuteReaderCalls.Should().Be(1);
        inner.ExecuteNonQueryCalls.Should().Be(1);
        inner.ExecuteScalarCalls.Should().Be(1);
        inner.PrepareCalls.Should().Be(1);
    }
}

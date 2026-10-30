using System.Data.Async.Adapters.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

/// <summary>
/// Disposing an adapter has to dispose the provider object it wraps, whatever static type the
/// caller disposes it through. Generated data-access code disposes through the interfaces, so a
/// dispose that only reaches the concrete adapter type leaves the provider object undisposed.
/// </summary>
public class AdapterDisposeTests
{
    public static TheoryData<string> ReaderViews() => ["adapter", "base class", "IAsyncDataReader", "interface"];

    public static TheoryData<string> ConnectionViews() => ["adapter", "base class", "IAsyncDbConnection", "interface"];

    [Theory]
    [MemberData(nameof(ReaderViews))]
    public async Task Reader_DisposeAsync_disposes_the_provider_reader(string view)
    {
        var inner = new DisposeRecordingDbDataReader();
        var adapter = new AdapterDbDataReader(inner);

        await (view switch
        {
            "adapter" => adapter.DisposeAsync(),
            "base class" => ((AsyncDbDataReader)adapter).DisposeAsync(),
            "IAsyncDataReader" => ((IAsyncDataReader)adapter).DisposeAsync(),
            _ => ((IAsyncDisposable)adapter).DisposeAsync(),
        });

        inner.DisposeAsyncCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ReaderViews))]
    public void Reader_Dispose_disposes_the_provider_reader(string view)
    {
        var inner = new DisposeRecordingDbDataReader();
        var adapter = new AdapterDbDataReader(inner);

        switch (view)
        {
            case "adapter": adapter.Dispose(); break;
            case "base class": ((AsyncDbDataReader)adapter).Dispose(); break;
            case "IAsyncDataReader": ((IAsyncDataReader)adapter).Dispose(); break;
            default: ((IDisposable)adapter).Dispose(); break;
        }

        inner.DisposeCalls.Should().Be(1);
        inner.DisposeAsyncCalls.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(ConnectionViews))]
    public async Task Connection_DisposeAsync_disposes_the_provider_connection(string view)
    {
        var inner = new DisposeRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);

        await (view switch
        {
            "adapter" => adapter.DisposeAsync(),
            "base class" => ((AsyncDbConnection)adapter).DisposeAsync(),
            "IAsyncDbConnection" => ((IAsyncDbConnection)adapter).DisposeAsync(),
            _ => ((IAsyncDisposable)adapter).DisposeAsync(),
        });

        inner.DisposeAsyncCalls.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ConnectionViews))]
    public void Connection_Dispose_disposes_the_provider_connection(string view)
    {
        var inner = new DisposeRecordingDbConnection();
        var adapter = new AdapterDbConnection(inner);

        switch (view)
        {
            case "adapter": adapter.Dispose(); break;
            case "base class": ((AsyncDbConnection)adapter).Dispose(); break;
            case "IAsyncDbConnection": ((IAsyncDbConnection)adapter).Dispose(); break;
            default: ((IDisposable)adapter).Dispose(); break;
        }

        inner.DisposeCalls.Should().Be(1);
        inner.DisposeAsyncCalls.Should().Be(0);
    }
}

using System.Data.Async.Validation.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Validation.Tests;

[Collection(ValidationCollection.Name)]
public class ConnectionParityTests
{
    private readonly ValidationFixture _fixture;

    public ConnectionParityTests(ValidationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Open_Close_State_Transitions_Match()
    {
        // Raw
        using var raw = _fixture.Provider.CreateRawConnection();
        raw.State.Should().Be(ConnectionState.Closed);
        raw.Open();
        raw.State.Should().Be(ConnectionState.Open);
        var rawDbName = raw.Database;
        var rawConnString = raw.ConnectionString;
        raw.Close();
        raw.State.Should().Be(ConnectionState.Closed);

        // Async
        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        async_.State.Should().Be(ConnectionState.Closed);
        await async_.OpenAsync();
        async_.State.Should().Be(ConnectionState.Open);
        async_.Database.Should().Be(rawDbName);
        async_.ConnectionString.Should().Be(rawConnString);
        await async_.CloseAsync();
        async_.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task Repeated_Open_Close_Cycles_Match()
    {
        var rawStates = new List<ConnectionState>();
        var asyncStates = new List<ConnectionState>();

        using var raw = _fixture.Provider.CreateRawConnection();
        for (int i = 0; i < 3; i++)
        {
            raw.Open();
            rawStates.Add(raw.State);
            raw.Close();
            rawStates.Add(raw.State);
        }

        await using var async_ = _fixture.Provider.CreateAsyncConnection();
        for (int i = 0; i < 3; i++)
        {
            await async_.OpenAsync();
            asyncStates.Add(async_.State);
            await async_.CloseAsync();
            asyncStates.Add(async_.State);
        }

        asyncStates.Should().BeEquivalentTo(rawStates, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task ConnectionTimeout_Property_Matches()
    {
        using var raw = _fixture.Provider.CreateRawConnection();
        await using var async_ = _fixture.Provider.CreateAsyncConnection();

        async_.ConnectionTimeout.Should().Be(raw.ConnectionTimeout);
    }
}

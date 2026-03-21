using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAsyncData_With_DbProviderFactory_Registers_IAsyncDbProviderFactory()
    {
        var services = new ServiceCollection();

        services.AddAsyncData(SqliteFactory.Instance);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAsyncDbProviderFactory>();
        factory.Should().BeOfType<AdapterDbProviderFactory>();
    }

    [Fact]
    public void AddAsyncData_With_IAsyncDbProviderFactory_Registers_Instance()
    {
        var services = new ServiceCollection();
        var factory = new AdapterDbProviderFactory(SqliteFactory.Instance);

        services.AddAsyncData(factory);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IAsyncDbProviderFactory>();
        resolved.Should().BeSameAs(factory);
    }
}

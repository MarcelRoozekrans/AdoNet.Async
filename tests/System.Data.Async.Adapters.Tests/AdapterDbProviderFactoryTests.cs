using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbProviderFactoryTests
{
    [Fact]
    public void CreateConnection_Returns_AdapterDbConnection()
    {
        var factory = new AdapterDbProviderFactory(SqliteFactory.Instance);

        IAsyncDbConnection connection = factory.CreateConnection();

        connection.Should().BeOfType<AdapterDbConnection>();
    }

    [Fact]
    public void CreateCommand_Returns_AdapterDbCommand()
    {
        var factory = new AdapterDbProviderFactory(SqliteFactory.Instance);

        IAsyncDbCommand command = factory.CreateCommand();

        command.Should().BeOfType<AdapterDbCommand>();
    }

    [Fact]
    public void CreateParameter_Returns_Parameter()
    {
        var factory = new AdapterDbProviderFactory(SqliteFactory.Instance);

        IDbDataParameter parameter = factory.CreateParameter();

        parameter.Should().NotBeNull();
    }
}

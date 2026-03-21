using Xunit;

namespace System.Data.Async.Tests;

public class IAsyncDbProviderFactoryContractTests
{
    [Fact]
    public void CreateConnection_Returns_IAsyncDbConnection()
    {
        var method = typeof(IAsyncDbProviderFactory).GetMethod("CreateConnection");
        Assert.NotNull(method);
        Assert.Equal(typeof(IAsyncDbConnection), method.ReturnType);
    }

    [Fact]
    public void CreateCommand_Returns_IAsyncDbCommand()
    {
        var method = typeof(IAsyncDbProviderFactory).GetMethod("CreateCommand");
        Assert.NotNull(method);
        Assert.Equal(typeof(IAsyncDbCommand), method.ReturnType);
    }

    [Fact]
    public void CreateParameter_Returns_IDbDataParameter()
    {
        var method = typeof(IAsyncDbProviderFactory).GetMethod("CreateParameter");
        Assert.NotNull(method);
        Assert.Equal(typeof(IDbDataParameter), method.ReturnType);
    }

    [Fact]
    public void IAsyncDbProviderFactory_Is_Interface()
    {
        Assert.True(typeof(IAsyncDbProviderFactory).IsInterface);
    }
}

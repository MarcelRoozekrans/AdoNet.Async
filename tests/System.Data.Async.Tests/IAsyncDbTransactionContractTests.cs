using Xunit;

namespace System.Data.Async.Tests;

public class IAsyncDbTransactionContractTests
{
    [Fact]
    public void IAsyncDbTransaction_Extends_IAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAsyncDbTransaction)));
    }

    [Fact]
    public void IAsyncDbTransaction_Extends_IDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IAsyncDbTransaction)));
    }

    [Fact]
    public void CommitAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbTransaction).GetMethod("CommitAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }

    [Fact]
    public void RollbackAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbTransaction).GetMethod("RollbackAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }
}

using Xunit;

namespace System.Data.Async.Tests;

public class IAsyncDbConnectionContractTests
{
    [Fact]
    public void IAsyncDbConnection_Extends_IAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAsyncDbConnection)));
    }

    [Fact]
    public void IAsyncDbConnection_Extends_IDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IAsyncDbConnection)));
    }

    [Fact]
    public void BeginTransactionAsync_Returns_ValueTask_IAsyncDbTransaction()
    {
        var method = typeof(IAsyncDbConnection).GetMethod("BeginTransactionAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<IAsyncDbTransaction>), method.ReturnType);
    }

    [Fact]
    public void BeginTransactionAsync_WithIsolationLevel_Returns_ValueTask_IAsyncDbTransaction()
    {
        var method = typeof(IAsyncDbConnection).GetMethod("BeginTransactionAsync", [typeof(IsolationLevel), typeof(CancellationToken)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<IAsyncDbTransaction>), method.ReturnType);
    }

    [Fact]
    public void OpenAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbConnection).GetMethod("OpenAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }

    [Fact]
    public void CloseAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbConnection).GetMethod("CloseAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }

    [Fact]
    public void ChangeDatabaseAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbConnection).GetMethod("ChangeDatabaseAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }
}

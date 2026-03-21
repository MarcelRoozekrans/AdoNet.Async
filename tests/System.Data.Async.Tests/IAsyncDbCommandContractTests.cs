using Xunit;

namespace System.Data.Async.Tests;

public class IAsyncDbCommandContractTests
{
    [Fact]
    public void IAsyncDbCommand_Extends_IAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAsyncDbCommand)));
    }

    [Fact]
    public void IAsyncDbCommand_Extends_IDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IAsyncDbCommand)));
    }

    [Fact]
    public void ExecuteReaderAsync_Returns_ValueTask_IAsyncDataReader()
    {
        var method = typeof(IAsyncDbCommand).GetMethod("ExecuteReaderAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<IAsyncDataReader>), method.ReturnType);
    }

    [Fact]
    public void ExecuteReaderAsync_WithBehavior_Returns_ValueTask_IAsyncDataReader()
    {
        var method = typeof(IAsyncDbCommand).GetMethod("ExecuteReaderAsync", [typeof(CommandBehavior), typeof(CancellationToken)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<IAsyncDataReader>), method.ReturnType);
    }

    [Fact]
    public void ExecuteNonQueryAsync_Returns_ValueTask_Int()
    {
        var method = typeof(IAsyncDbCommand).GetMethod("ExecuteNonQueryAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<int>), method.ReturnType);
    }

    [Fact]
    public void ExecuteScalarAsync_Returns_ValueTask_NullableObject()
    {
        var method = typeof(IAsyncDbCommand).GetMethod("ExecuteScalarAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<object?>), method.ReturnType);
    }

    [Fact]
    public void PrepareAsync_Returns_ValueTask()
    {
        var method = typeof(IAsyncDbCommand).GetMethod("PrepareAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask), method.ReturnType);
    }
}

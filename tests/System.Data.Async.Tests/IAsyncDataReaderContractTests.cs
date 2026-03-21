using Xunit;

namespace System.Data.Async.Tests;

public class IAsyncDataReaderContractTests
{
    [Fact]
    public void IAsyncDataReader_Extends_IAsyncDataRecord()
    {
        Assert.True(typeof(IAsyncDataRecord).IsAssignableFrom(typeof(IAsyncDataReader)));
    }

    [Fact]
    public void IAsyncDataReader_Extends_IAsyncEnumerable()
    {
        Assert.True(typeof(IAsyncEnumerable<IAsyncDataRecord>).IsAssignableFrom(typeof(IAsyncDataReader)));
    }

    [Fact]
    public void IAsyncDataReader_Extends_IAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAsyncDataReader)));
    }

    [Fact]
    public void IAsyncDataReader_Extends_IDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IAsyncDataReader)));
    }

    [Fact]
    public void ReadAsync_Returns_ValueTask_Bool()
    {
        var method = typeof(IAsyncDataReader).GetMethod("ReadAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<bool>), method.ReturnType);
    }

    [Fact]
    public void NextResultAsync_Returns_ValueTask_Bool()
    {
        var method = typeof(IAsyncDataReader).GetMethod("NextResultAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Threading.Tasks.ValueTask<bool>), method.ReturnType);
    }

    [Fact]
    public void GetFieldValueAsync_Exists_On_IAsyncDataRecord()
    {
        var method = typeof(IAsyncDataRecord).GetMethod("GetFieldValueAsync");
        Assert.NotNull(method);
        Assert.True(method.IsGenericMethod);
    }
}

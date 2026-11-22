#pragma warning disable ZA0501 // test data boxing is irrelevant
using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Tests;

public class AsyncXmlBufferingTests
{
    private static async Task<AsyncDataTable> NewTable(int rows)
    {
        var table = new AsyncDataTable("Items");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        for (var i = 0; i < rows; i++)
        {
            await table.Rows.AddAsync(new object?[] { i, "name-" + i });
        }

        return table;
    }

    private static AsyncDataSet NewDataSet(int rows)
    {
        var ds = new AsyncDataSet("Store");
        var t = new DataTable("Items");
        t.Columns.Add("Id", typeof(int));
        t.Columns.Add("Name", typeof(string));
        for (var i = 0; i < rows; i++)
        {
            t.Rows.Add(i, "name-" + i);
        }

        ds.Tables.Add(t);
        return ds;
    }

    [Fact]
    public async Task Table_ReadXmlAsync_never_reads_synchronously()
    {
        var table = (await NewTable(3));
        using var source = new MemoryStream();
        await table.WriteXmlAsync(source);
        var bytes = source.ToArray();

        await using var stream = new AsyncOnlyStream(bytes);
        var result = new AsyncDataTable();
        using var schema = new MemoryStream();
        await table.WriteXmlSchemaAsync(schema);
        schema.Position = 0;
        result.ReadXmlSchema(schema);
        await result.ReadXmlAsync(stream);

        result.Rows.Count.Should().Be(3);
        stream.AsyncReads.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Table_ReadXmlSchemaAsync_never_reads_synchronously()
    {
        var table = (await NewTable(1));
        using var source = new MemoryStream();
        await table.WriteXmlSchemaAsync(source);

        await using var stream = new AsyncOnlyStream(source.ToArray());
        var result = new AsyncDataTable();
        await result.ReadXmlSchemaAsync(stream);

        result.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task DataSet_ReadXmlAsync_and_schema_never_read_synchronously()
    {
        var ds = NewDataSet(2);
        using var data = new MemoryStream();
        using var schema = new MemoryStream();
        await ds.WriteXmlAsync(data);
        await ds.WriteXmlSchemaAsync(schema);

        var result = new AsyncDataSet();
        await using (var s = new AsyncOnlyStream(schema.ToArray()))
        {
            await result.ReadXmlSchemaAsync(s);
        }

        await using (var d = new AsyncOnlyStream(data.ToArray()))
        {
            await result.ReadXmlAsync(d);
        }

        result.Tables["Items"]!.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Writes_never_write_synchronously()
    {
        await using var target = new AsyncOnlyStream([]);
        await (await NewTable(5)).WriteXmlAsync(target);
        await (await NewTable(5)).WriteXmlSchemaAsync(target);
        await NewDataSet(5).WriteXmlAsync(target);
        await NewDataSet(5).WriteXmlSchemaAsync(target);

        target.AsyncWrites.Should().Be(4);
        target.AsyncFlushes.Should().Be(4);
        target.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reads_honour_cancellation_during_buffering()
    {
        using var cts = new CancellationTokenSource();
        await using var stream = new AsyncOnlyStream(new byte[100_000]) { OnRead = cts.Cancel };

        var act = async () => await new AsyncDataTable().ReadXmlAsync(stream, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        var set = async () => await new AsyncDataSet().ReadXmlSchemaAsync(
            new AsyncOnlyStream(new byte[10]), new CancellationToken(true));
        await set.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Writes_honour_cancellation()
    {
        using var target = new MemoryStream();
        var source = await NewTable(2);
        var table = async () => await source.WriteXmlAsync(target, new CancellationToken(true));
        await table.Should().ThrowAsync<OperationCanceledException>();
        var ds = async () => await NewDataSet(2).WriteXmlSchemaAsync(target, new CancellationToken(true));
        await ds.Should().ThrowAsync<OperationCanceledException>();
        target.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(5000)]
    public async Task Table_roundtrip_matches_sync_methods(int rows)
    {
        var table = (await NewTable(rows));
        using var asyncData = new MemoryStream();
        using var syncData = new MemoryStream();
        using var asyncSchema = new MemoryStream();
        using var syncSchema = new MemoryStream();
        await table.WriteXmlAsync(asyncData);
        await table.WriteXmlSchemaAsync(asyncSchema);
        table.WriteXml(syncData);
        table.WriteXmlSchema(syncSchema);


        var viaAsync = new AsyncDataTable();
        asyncSchema.Position = 0;
        await viaAsync.ReadXmlSchemaAsync(asyncSchema);
        asyncData.Position = 0;
        await viaAsync.ReadXmlAsync(asyncData);

        var viaSync = new AsyncDataTable();
        syncSchema.Position = 0;
        viaSync.ReadXmlSchema(syncSchema);
        syncData.Position = 0;
        viaSync.ReadXml(syncData);

        viaAsync.Rows.Count.Should().Be(rows);
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        viaAsync.WriteXml(a);
        viaSync.WriteXml(b);
        AsText(a).Should().Be(AsText(b));
    }

    [Fact]
    public async Task DataSet_roundtrip_matches_sync_methods()
    {
        var ds = NewDataSet(50);
        using var asyncData = new MemoryStream();
        using var syncData = new MemoryStream();
        await ds.WriteXmlAsync(asyncData);
        ds.WriteXml(syncData);
        asyncData.Position = 0;
        syncData.Position = 0;
        var fromAsync = new AsyncDataSet();
        var fromSync = new AsyncDataSet();
        using var schema0 = new MemoryStream();
        await ds.WriteXmlSchemaAsync(schema0);
        schema0.Position = 0;
        fromAsync.ReadXmlSchema(schema0);
        schema0.Position = 0;
        fromSync.ReadXmlSchema(schema0);
        fromAsync.ReadXml(asyncData);
        fromSync.ReadXml(syncData);
        fromAsync.GetXml().Should().Be(fromSync.GetXml());

        using var schema = new MemoryStream();
        await ds.WriteXmlSchemaAsync(schema);
        var result = new AsyncDataSet();
        schema.Position = 0;
        await result.ReadXmlSchemaAsync(schema);
        asyncData.Position = 0;
        await result.ReadXmlAsync(asyncData);
        result.GetXml().Should().Be(ds.GetXml());
    }

    [Fact]
    public async Task Failing_source_stream_propagates_and_leaves_table_unchanged()
    {
        await using var stream = new AsyncOnlyStream(new byte[10]) { OnRead = () => throw new IOException("boom") };
        var table = new AsyncDataTable();
        var act = async () => await table.ReadXmlAsync(stream);
        await act.Should().ThrowAsync<IOException>();
        table.Rows.Count.Should().Be(0);
    }

    private static string AsText(MemoryStream ms) => System.Text.Encoding.UTF8.GetString(ms.ToArray());

    /// <summary>A stream whose synchronous Read and Write throw; only the async members work.</summary>
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public AsyncOnlyStream(byte[] bytes)
        {
            _inner = new MemoryStream();
            _inner.Write(bytes);
            _inner.Position = 0;
        }

        public int AsyncReads { get; private set; }
        public int AsyncWrites { get; private set; }
        public int AsyncFlushes { get; private set; }
        public Action? OnRead { get; init; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("synchronous Read");

        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("synchronous Write");

        public override void Flush() => throw new InvalidOperationException("synchronous Flush");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncReads++;
            OnRead?.Invoke();
            return new ValueTask<int>(_inner.Read(buffer.Span));
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncWrites++;
            _inner.Write(buffer.Span);
            return default;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            AsyncFlushes++;
            return Task.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

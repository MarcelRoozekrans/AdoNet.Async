using System.Buffers;
using System.Xml;

namespace System.Data.Async.DataSet;

/// <summary>
/// A growable in-memory byte buffer backed by <see cref="ArrayPool{T}"/>. The async XML methods use it to
/// hold a whole document, so the stream I/O can be awaited while the (synchronous) DataSet serializer
/// only ever touches memory. Disposing returns the rented array to the pool.
/// </summary>
internal sealed class XmlBuffer : Stream
{
    private const int InitialCapacity = 4096;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(InitialCapacity);
    private int _length;
    private int _position;
    private bool _disposed;

    /// <summary>Copies <paramref name="source"/> to the end into a new buffer, asynchronously.</summary>
    internal static async ValueTask<XmlBuffer> ReadFromAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new XmlBuffer();
        try
        {
            await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer._position = 0;
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Parses synchronously from the buffered bytes.</summary>
    internal XmlReader CreateReader() => XmlReader.Create(this, new XmlReaderSettings());

    /// <summary>Serializes synchronously into the buffer.</summary>
    internal static XmlWriter CreateWriter(XmlBuffer target) => XmlWriter.Create(target, new XmlWriterSettings());

    /// <summary>Writes the buffered bytes to <paramref name="destination"/> and flushes it, asynchronously.</summary>
    internal async ValueTask CopyToAndFlushAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await destination.WriteAsync(_buffer.AsMemory(0, _length), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => !_disposed;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _length);
            _position = (int)value;
        }
    }

    public override void Flush()
    {
        // Nothing to flush: the data lives in memory.
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var n = Math.Min(buffer.Length, _length - _position);
        _buffer.AsSpan(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        Position = target;
        return target;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var required = _position + buffer.Length;
        if (required > _buffer.Length)
        {
            Grow(required);
        }

        buffer.CopyTo(_buffer.AsSpan(_position));
        _position = required;
        if (required > _length)
        {
            _length = required;
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return default;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Grow(int required)
    {
        var newSize = (int)Math.Min(Math.Max((long)_buffer.Length * 2, required), Array.MaxLength);
        var bigger = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _length).CopyTo(bigger);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = bigger;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = [];
            }
        }

        base.Dispose(disposing);
    }
}

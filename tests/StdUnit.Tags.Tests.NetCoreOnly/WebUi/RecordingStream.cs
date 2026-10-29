using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>线程安全的只写流：把 SSE 字节记录下来供断言。</summary>
internal sealed class RecordingStream : Stream
{
    private readonly object _gate = new();
    private readonly MemoryStream _inner = new();

    public string Text
    {
        get
        {
            lock (this._gate)
            {
                return Encoding.UTF8.GetString(this._inner.GetBuffer(), 0, (int)this._inner.Length);
            }
        }
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (this._gate)
        {
            this._inner.Write(buffer, offset, count);
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (this._gate)
        {
            this._inner.Write(buffer);
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        this.Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.Write(buffer.Span);
        return default;
    }
}

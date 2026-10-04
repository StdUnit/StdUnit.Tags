using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7ByteDirectTagTests
{
    [Fact]
    public void BufferSize_ShouldBeOne()
    {
        var tag = CreateByteTag(out _, out _, new byte[1]);
        Assert.Equal(1, tag.BufferSize);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnCorrectByte()
    {
        const byte expected = 0xAB;
        var payload = new byte[] { expected };
        var tag = CreateByteTag(out var fake, out _, payload);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
        Assert.Equal(1, fake.LastReadLength);
    }

    [Fact]
    public async Task ReadAsync_MinValue_ShouldWork()
    {
        const byte expected = 0x00;
        var payload = new byte[] { expected };
        var tag = CreateByteTag(out var fake, out _, payload);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
    }

    [Fact]
    public async Task ReadAsync_MaxValue_ShouldWork()
    {
        const byte expected = 0xFF;
        var payload = new byte[] { expected };
        var tag = CreateByteTag(out var fake, out _, payload);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expected, tag.Value);
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteCorrectByte()
    {
        const byte value = 0xCD;
        var tag = CreateByteTag(out var fake, out _, new byte[1]);

        tag.Value = value;
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(fake.LastWriteBuffer);
        var buf = Assert.Single(fake.LastWriteBuffer!);
        Assert.Equal(value, buf);
    }

    [Fact]
    public async Task WriteAsync_ShouldSetIsDirtyFalse()
    {
        var tag = CreateByteTag(out var fake, out _, new byte[1]);

        tag.Value = 0x42;
        Assert.True(tag.IsDirty);

        await tag.WriteAsync(CancellationToken.None);

        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task ReadAsync_ShouldSetTimestamp()
    {
        var tag = CreateByteTag(out var fake, out _, new byte[] { 0x7F });

        var before = tag.Timestamp;
        await Task.Delay(10);
        await tag.ReadAsync(CancellationToken.None);

        Assert.True(tag.Timestamp > before);
    }

    private static ByteDirectTag CreateByteTag(out FakeContinuousBytesChannel fake, out ITagGrp grp, byte[] payload)
    {
        fake = new FakeContinuousBytesChannel(payload);
        grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var descriptor = new TagDescriptor()
        {
            TagName = "direct-byte",
            TagKind = BuiltinTagKinds.BYTE,
            RawAddress = "DB1.50",
        };
        return new ByteDirectTag(descriptor, thisChannel: null, grp.IntoTagContainer());
    }

    private sealed class FakeContinuousBytesChannel : S7TagChannel
    {
        public FakeContinuousBytesChannel(byte[] payload)
            : base(
            new S7TagChannelDescriptor { Name = "FakeContinuousBytesChannel", },
            new LoggerFactory().CreateLogger<S7TagChannel>()
        )
        {
            LastWriteBuffer = (byte[])payload.Clone();
        }

        public int LastReadLength { get; private set; }
        public byte[] LastWriteBuffer { get; private set; }

        public override Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public override Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public override Task<byte[]> ReadAsync(string address, int count, CancellationToken ct)
        {
            if (count > LastWriteBuffer.Length)
            {
                throw new System.InvalidOperationException($"Unexpected read length={count}");
            }
            LastReadLength = count;
            return Task.FromResult(LastWriteBuffer.AsSpan(0, count).ToArray());
        }

        public override Task WriteAsync(string address, byte[] bytes, CancellationToken ct)
        {
            LastWriteBuffer = (byte[])bytes.Clone();
            return Task.CompletedTask;
        }
    }
}

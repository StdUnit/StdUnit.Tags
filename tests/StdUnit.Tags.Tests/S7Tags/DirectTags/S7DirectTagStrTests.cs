using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7DirectTagStrTests
{

    [Fact]
    public async Task DirectStrTag_ReadAsync_ReadsHeaderAndPayload()
    {
        const byte maxLen = 10;
        var payload = new byte[] { maxLen, 5, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O', 0, 0, 0, 0, 0 };
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateStrDirectTag(maxLen, fake, grp.IntoTagContainer());

        await tag.ReadAsync(CancellationToken.None);
        Assert.Equal(maxLen, tag.Maxlen);
        Assert.Equal(maxLen + 2, fake.LastReadLength);
        Assert.Equal("HELLO", tag.Value);
    }

    [Fact]
    public async Task DirectStrTag_WriteAsync_WritesHeaderAndAscii()
    {
        const byte maxLen = 6;
        var fake = new FakeContinuousBytesChannel(new byte[maxLen + 2]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateStrDirectTag(maxLen, fake, grp.IntoTagContainer());

        tag.Value = "ABC";
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(fake.LastWriteBuffer);
        Assert.Equal(maxLen, tag.Maxlen);
        Assert.Equal(maxLen + 2, fake.LastWriteBuffer!.Length);
        Assert.Equal(maxLen, fake.LastWriteBuffer[0]);
        Assert.Equal(3, fake.LastWriteBuffer[1]);
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', (byte)'C' }, fake.LastWriteBuffer.AsSpan(2, 3).ToArray());
    }

    [Fact]
    public async Task DirectStrTag_WriteAsync_Throws_WhenInputExceedsMaxLen()
    {
        const byte maxLen = 4;
        var fake = new FakeContinuousBytesChannel(new byte[maxLen + 2]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateStrDirectTag(maxLen, fake, grp.IntoTagContainer());

        tag.Value = "ABCDE";
        await Assert.ThrowsAsync<ArgumentException>(() => tag.WriteAsync(CancellationToken.None));
    }


    private static StrDirectTag CreateStrDirectTag(byte maxLen, IContinuousBytesBasedTagChannel channel, TagContainer container)
    {
        var descriptor = new TagDescriptor()
        {
            TagName = "direct-str",
            TagKind = BuiltinTagKinds.STR,
            RawAddress = "DB1.300",
        };

        return new StrDirectTag(descriptor, thisChannel: null, parent: container, maxLen: maxLen);
    }

    private sealed class FakeContinuousBytesChannel : S7TagChannel
    {
        public FakeContinuousBytesChannel(byte[] payload)
            : base(
                 new S7TagChannelDescriptor() { Name = "fake" },
                 new LoggerFactory().CreateLogger<FakeContinuousBytesChannel>()
            )
        {
            this.LastWriteBuffer = (byte[])payload.Clone();
        }

        public int LastReadLength { get; private set; }

        public byte[] LastWriteBuffer { get; private set; }



        public override Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public override Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public override Task<byte[]> ReadAsync(string address, int count, CancellationToken ct)
        {
            if (count > this.LastWriteBuffer.Length)
            {
                throw new InvalidOperationException($"Unexpected read length={count}");
            }
            this.LastReadLength = count;
            return Task.FromResult(this.LastWriteBuffer.AsSpan(0, count).ToArray());
        }

        public override Task WriteAsync(string address, byte[] bytes, CancellationToken ct)
        {
            this.LastWriteBuffer = (byte[])bytes.Clone();
            return Task.CompletedTask;
        }

    }
}

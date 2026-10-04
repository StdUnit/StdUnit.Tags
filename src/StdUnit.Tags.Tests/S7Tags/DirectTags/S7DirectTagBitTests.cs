using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7DirectTagBitTests
{

    [Theory]
    [InlineData((byte)0, 1, new byte[] { 0b00000001 }, true)]
    [InlineData((byte)7, 1, new byte[] { 0b10000000 }, true)]
    [InlineData((byte)8, 2, new byte[] { 0b00000000, 0b00000001 }, true)]
    [InlineData((byte)9, 2, new byte[] { 0b00000000, 0b00000010 }, true)]
    public async Task DirectBitTag_AutoBufferSize_ReadsEnoughBytes(byte nthBit, int expectedLength, byte[] payload, bool expectedValue)
    {
        var fake = new FakeContinuousBytesChannel(payload);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateBitDirectTag(nthBit, fake, grp);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(expectedLength, fake.LastReadLength);
        Assert.IsType<bool>(tag.Value);
        Assert.Equal(expectedValue, tag.Value);
    }

    [Theory]
    [InlineData((byte)0, true, new byte[] { 0b00000001 })]
    [InlineData((byte)7, true, new byte[] { 0b10000000 })]
    [InlineData((byte)8, true, new byte[] { 0b00000000, 0b00000001 })]
    [InlineData((byte)9, true, new byte[] { 0b00000000, 0b00000010 })]
    [InlineData((byte)0, false, new byte[] { 0b00000000 })]
    [InlineData((byte)8, false, new byte[] { 0b00000000, 0b00000000 })]
    public async Task DirectBitTag_WriteAsync_WritesExpectedFlags(byte nthBit, bool val, byte[] expected)
    {
        var fake = new FakeContinuousBytesChannel(new byte[expected.Length]);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateBitDirectTag(nthBit, fake, grp);

        tag.Value = val;
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(fake.LastWriteBuffer);
        Assert.Equal(expected.Length, fake.LastWriteBuffer!.Length);
        Assert.Equal(expected, fake.LastWriteBuffer);
    }

    #region 测试写操作时，其他位的值是否被正确保留
    [Fact]
    public async Task DirectBitTag_WriteAsync_PreservesOtherBits_ClearOneBit()
    {
        var initial = new byte[] { 0b11111111 };
        var fake = new FakeContinuousBytesChannel(initial);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateBitDirectTag(0, fake, grp); // clear LSB

        tag.Value = false;
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(fake.LastWriteBuffer);
        Assert.Equal(new byte[] { 0b11111110 }, fake.LastWriteBuffer);
    }

    [Fact]
    public async Task DirectBitTag_WriteAsync_PreservesOtherBits_SetOneBit()
    {
        var initial = new byte[] { 0b11111110 };
        var fake = new FakeContinuousBytesChannel(initial);
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test-grp", IsEntry = false }, fake);
        var tag = CreateBitDirectTag(0, fake, grp); // set LSB

        tag.Value = true;
        await tag.WriteAsync(CancellationToken.None);

        Assert.NotNull(fake.LastWriteBuffer);
        Assert.Equal(new byte[] { 0b11111111 }, fake.LastWriteBuffer);
    }
    #endregion

    private static ITag CreateBitDirectTag(byte nthBit, IContinuousBytesBasedTagChannel channel, ITagGrp grp)
    {
        var descriptor = new TagDescriptor()
        {
            TagName = "direct-bit",
            TagKind = BuiltinTagKinds.BIT,
            RawAddress = $"DB1.100.{nthBit}",
        };

        var tag = new BitDirectTag(descriptor, thisChannel: null, grp.IntoTagContainer(), nthBit: nthBit, bufferSize: 0);
        return tag;
    }

    private sealed class FakeContinuousBytesChannel : S7TagChannel
    {
        public FakeContinuousBytesChannel(byte[] payload)
            : base(
                 new S7TagChannelDescriptor() { Name = "fake" },
                 new LoggerFactory().CreateLogger<S7TagChannel>()
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

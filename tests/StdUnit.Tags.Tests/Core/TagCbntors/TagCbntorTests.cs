using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbntors;

/// <summary>
/// 测试 TagCbntor 在 <see cref="S7TagChannel"/> 上的读写行为
/// </summary>
public class TagCbntorContinuousChannelTests
{
    private static (TestByteTagCbnt cbnt, FakeS7TagChannel channel, S7ByteTagCbntor tag) CreateContext()
    {
        var channel = new FakeS7TagChannel();
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0.0" })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(10);
        var descriptor = new TagDescriptor
        {
            TagName = "byte1",
            RawAddress = "0.0",
            TagKind = BuiltinTagKinds.BYTE,
            TagSize = 1,
        };
        var tag = new S7ByteTagCbntor(descriptor, cbnt, 0);
        return (cbnt, channel, tag);
    }

    [Fact]
    public async Task ReadAsync_ReadsFromChannelAndCopiesToCache()
    {
        var (cbnt, channel, tag) = CreateContext();

        channel.ReadData = [0x42, 0x00];
        cbnt.ResizeCache(10);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal("0.0", channel.LastReadAddress);
        Assert.Equal(1, channel.LastReadCount);
        Assert.Equal(0x42, cbnt.Cache.Span[0]);
    }

    [Fact]
    public async Task ReadAsync_FiresOnTagReadEvent()
    {
        var (_, _, tag) = CreateContext();
        var eventFired = false;

        tag.OnTagRead += (sender, args) =>
        {
            eventFired = true;
        };

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(eventFired);
    }

    [Fact]
    public async Task WriteAsync_WritesToChannel()
    {
        var (_, channel, tag) = CreateContext();

        tag.Value = (byte)0x99;
        await tag.WriteAsync(CancellationToken.None);

        Assert.Equal("0.0", channel.LastWrittenAddress);
        Assert.NotNull(channel.LastWrittenBytes);
        Assert.Single(channel.LastWrittenBytes);
        Assert.Equal(0x99, channel.LastWrittenBytes[0]);
    }

    [Fact]
    public async Task WriteAsync_FiresOnTagWrittenEvent()
    {
        var (_, _, tag) = CreateContext();
        var eventFired = false;

        tag.OnTagWritten += (sender, args) =>
        {
            eventFired = true;
        };

        tag.Value = (byte)0x99;
        await tag.WriteAsync(CancellationToken.None);

        Assert.True(eventFired);
    }

    [Fact]
    public async Task WriteAsync_ClearsIsDirty()
    {
        var (_, _, tag) = CreateContext();

        tag.Value = (byte)0x99;
        tag.IsDirty = true;
        await tag.WriteAsync(CancellationToken.None);

        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task ReadAsync_NormalizedAddressIsUsed()
    {
        var (cbnt, channel, _) = CreateContext();

        var descriptor = new TagDescriptor
        {
            TagName = "byte2",
            RawAddress = "2.0",
            TagKind = BuiltinTagKinds.BYTE,
            TagSize = 2,
        };
        var tag2 = new S7ByteTagCbntor(descriptor, cbnt, 2);
        cbnt.ResizeCache(10);

        await tag2.ReadAsync(CancellationToken.None);

        Assert.Equal("2.0", channel.LastReadAddress);
        Assert.Equal(2, channel.LastReadCount);
    }
}

using StdUnit.Tags.S7;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

/// <summary>
/// S7 测点组合（<c>S7TagCbnt</c>）的整体读写：读写直接打在 <see cref="S7TagChannel"/> 上，
/// 缓冲区就是组合缓存本身（不再先读进临时 byte[] 再拷回）。
/// </summary>
public class S7TagCbntTests
{
    [Fact]
    public async Task ReadAsync_ShouldReadWholeCacheFromChannel()
    {
        var channel = new FakeS7TagChannel { ReadData = new byte[] { 0x11, 0x22, 0x33, 0x44 } };
        var cbnt = CreateCbnt(channel, cacheSize: 4);

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Equal("DB200.100", channel.LastReadAddress);
        Assert.Equal(4, channel.LastReadCount);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, cbnt.Cache.ToArray());
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteWholeCacheToChannel()
    {
        var channel = new FakeS7TagChannel();
        var cbnt = CreateCbnt(channel, cacheSize: 4);
        var expected = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        expected.AsSpan().CopyTo(cbnt.Cache.Span);

        await cbnt.WriteAsync(CancellationToken.None);

        Assert.Equal("DB200.100", channel.LastWrittenAddress);
        Assert.Equal(expected, channel.LastWrittenBytes);
    }

    [Fact]
    public async Task ReadAsync_WhenChannelIsNotS7TagChannel_ShouldThrow()
    {
        var cbnt = CreateCbnt(new NonS7Channel(), cacheSize: 4);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => cbnt.ReadAsync(CancellationToken.None));
        Assert.Contains(nameof(S7TagChannel), ex.Message);
        Assert.Contains(nameof(NonS7Channel), ex.Message);
    }

    [Fact]
    public async Task WriteAsync_WhenChannelIsNotS7TagChannel_ShouldThrow()
    {
        var cbnt = CreateCbnt(new NonS7Channel(), cacheSize: 4);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => cbnt.WriteAsync(CancellationToken.None));
        Assert.Contains(nameof(S7TagChannel), ex.Message);
        Assert.Contains(nameof(NonS7Channel), ex.Message);
    }

    private static S7TagCbnt CreateCbnt(ITagChannel channel, int cacheSize)
    {
        var cbnt = new S7TagCbnt(new TagCbntDescriptor { Name = "cbnt1", StartAddress = "DB200.100" })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(cacheSize);
        return cbnt;
    }

    /// <summary>
    /// 仅实现 ITagChannel，不具备 <see cref="S7TagChannel"/> 的字节读写能力
    /// </summary>
    private sealed class NonS7Channel : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor { Name = "NonS7Channel", Driver = "MOCK" };

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose() { }
    }
}

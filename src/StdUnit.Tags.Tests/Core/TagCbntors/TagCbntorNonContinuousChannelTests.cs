using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbntors;

/// <summary>
/// 默认是<see cref="TagCbntor"/>的读写行为是基于<see cref="IContinuousBytesBasedTagChannel"/>的，
/// 为了避免滥用，如果通道不是连续字节通道，会应抛出异常。
/// 本测试是保证应该在接口类型错误时，要抛出异常来提醒开发者。
/// </summary>
public class TagCbntorNonContinuousChannelTests
{
    /// <summary>
    /// 仅实现 ITagChannel，不实现 IContinuousBytesBasedTagChannel
    /// </summary>
    private class NonContinuousChannel : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "NonContinuous",
            Driver = "MOCK",
        };
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public async Task ReadAsync_ThrowsNotImplementedException()
    {
        var channel = new NonContinuousChannel();
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0.0" })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(4);
        var descriptor = new TagDescriptor
        {
            TagName = "b",
            RawAddress = "0.0",
            TagKind = BuiltinTagKinds.BYTE,
            TagSize = 1,
        };
        var tag = new S7ByteTagCbntor(descriptor, cbnt, 0);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => tag.ReadAsync(CancellationToken.None));
        Assert.Contains(nameof(IContinuousBytesBasedTagChannel), ex.Message);
    }

    [Fact]
    public async Task WriteAsync_ThrowsNotImplementedException()
    {
        var channel = new NonContinuousChannel();
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", StartAddress = "0.0" })
        {
            Channel = channel,
        };
        cbnt.ResizeCache(4);
        var descriptor = new TagDescriptor
        {
            TagName = "b",
            RawAddress = "0.0",
            TagKind = BuiltinTagKinds.BYTE,
            TagSize = 1,
        };
        var tag = new S7ByteTagCbntor(descriptor, cbnt, 0);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() => tag.WriteAsync(CancellationToken.None));
        Assert.Contains(nameof(IContinuousBytesBasedTagChannel), ex.Message);
    }
}

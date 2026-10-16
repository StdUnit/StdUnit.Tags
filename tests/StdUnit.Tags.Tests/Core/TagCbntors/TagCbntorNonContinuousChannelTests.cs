using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbntors;

/// <summary>
/// <see cref="S7TagCbntorBase"/> 的默认读写实现依赖 <see cref="S7TagChannel"/> 提供的字节读写能力，
/// 为了避免滥用，如果通道不是 <see cref="S7TagChannel"/>，就会抛出异常。
/// 本测试是保证应该在通道类型错误时，要抛出异常来提醒开发者。
/// </summary>
public class TagCbntorNonContinuousChannelTests
{
    /// <summary>
    /// 仅实现 ITagChannel，不具备 <see cref="S7TagChannel"/> 的字节读写能力
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
        Assert.Contains(nameof(S7TagChannel), ex.Message);
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
        Assert.Contains(nameof(S7TagChannel), ex.Message);
    }
}

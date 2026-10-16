using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.S7;

namespace StdUnit.Tags.Tests;

/// <summary>
/// 测试用 <see cref="TagCbnt{T}"/>（T=byte）具体实现：
/// 基于 <see cref="S7TagChannel"/> 的直接字节读写，用于各类 Cbntor 的容器测试。
/// 与生产代码中的 S7TagCbnt / ModbusTcpTagCbnt 行为一致。
/// </summary>
internal class TestByteTagCbnt : TagCbnt<byte>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal TestByteTagCbnt(TagCbntDescriptor descriptor) : base(descriptor)
    {
    }

    /// <inheritdoc/>
    public override Task ReadAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        channel.Read(this.StartAddress, this.Cache.Span, ct);
        this.NotifyChildrenRead();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        channel.Write(this.StartAddress, this.Cache.Span, ct);
        this.NotifyChildrenWritten();
        return Task.CompletedTask;
    }

    private S7TagChannel GetByteChannel()
    {
        var channel0 = this.SearchRequiredChannel();
        var channel = channel0 as S7TagChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"通道组合({nameof(TestByteTagCbnt)})依赖于通道{nameof(S7TagChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

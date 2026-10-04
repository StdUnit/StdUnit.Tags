using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;

namespace StdUnit.Tags.Tests;

/// <summary>
/// 测试用 <see cref="TagCbnt{T}"/>（T=byte）具体实现：
/// 基于 <see cref="IContinuousBytesBasedTagChannel"/> 的直接字节读写，用于各类 Cbntor 的容器测试。
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
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        var bytes = await channel.ReadAsync(this.StartAddress, this.CacheSize, ct);
        this.Cache = bytes.AsMemory();
        this.NotifyChildrenRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        var bytes = this.Cache.ToArray();
        await channel.WriteAsync(this.StartAddress, bytes, ct);
        this.NotifyChildrenWritten();
    }

    private IContinuousBytesBasedTagChannel GetByteChannel()
    {
        var channel0 = this.SearchRequiredChannel();
        var channel = channel0 as IContinuousBytesBasedTagChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"通道组合({nameof(TestByteTagCbnt)})依赖于通道{nameof(IContinuousBytesBasedTagChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

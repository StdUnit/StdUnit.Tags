using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7 测点组合：缓存 = PLC 内存原始字节（T=byte），
/// 读写基于 <see cref="IContinuousBytesBasedTagChannel"/>（即 <see cref="S7TagChannel"/>）。
/// 西门子 PLC 内存为大端字节序，子测点按其 <c>EndianKind</c> 自行解读缓存，本类不做任何字节序转换。
/// </summary>
internal class S7TagCbnt : TagCbnt<byte>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal S7TagCbnt(TagCbntDescriptor descriptor) : base(descriptor)
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
            throw new NotImplementedException($"通道组合({nameof(S7TagCbnt)})依赖于通道{nameof(IContinuousBytesBasedTagChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

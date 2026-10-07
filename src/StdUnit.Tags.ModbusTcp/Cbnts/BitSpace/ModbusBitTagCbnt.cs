using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 位空间（线圈/离散输入）测点组合：缓存 = bool 数组（T=bool，每元素 = 一个地址位的状态）。<br/>
/// 读写基于 <see cref="IModbusBitsChannel"/>（即 <see cref="ModbusTcpChannel"/>），bool[] 直接赋给缓存、零转换。<br/>
/// 位空间地址本身是 bit，无字节序概念。
/// </summary>
internal class ModbusBitTagCbnt : TagCbnt<bool>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal ModbusBitTagCbnt(TagCbntDescriptor descriptor) : base(descriptor)
    {
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = GetBitsChannel();
        var bits = await channel.ReadBitsAsync(this.StartAddress, this.CacheSize, ct);
        this.Cache = bits;
        this.NotifyChildrenRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel = GetBitsChannel();
        await channel.WriteBitsAsync(this.StartAddress, this.Cache.ToArray(), ct);
        this.NotifyChildrenWritten();
    }

    private IModbusBitsChannel GetBitsChannel()
    {
        var channel0 = this.SearchRequiredChannel();
        var channel = channel0 as IModbusBitsChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"通道组合({nameof(ModbusBitTagCbnt)})依赖于通道{nameof(IModbusBitsChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 位空间（线圈 0x / 离散输入 1x）组合子基类：构造时强类型绑定 <see cref="TagCbnt{T}"/>（T=bool）。<br/>
/// <see cref="ReadAsync"/> / <see cref="WriteAsync"/> 基于 <see cref="IModbusBitsChannel"/>（即 <see cref="ModbusTcpChannel"/>）的位读写模板，
/// 子类只需实现 <c>Value</c> 的解读。<br/>
/// 位空间缓存 = bool 数组（每元素 = 一个地址位的状态），读时 bool[] 直接写入缓存、写时缓存直接送出——零转换。
/// </summary>
public abstract class ModbusBitSpaceTagCbntorBase : TagCbntor
{
    private readonly TagCbnt<bool> _cbnt;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">必须是 <see cref="TagCbnt{T}"/>（T=bool）</param>
    /// <param name="tagOffset"></param>
    /// <param name="cacheOffset"></param>
    internal ModbusBitSpaceTagCbntorBase(TagDescriptor tagDescriptor, TagCbnt<bool> tagCbnt, int tagOffset, int cacheOffset)
        : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
    {
        this._cbnt = tagCbnt;
    }

    /// <summary>
    /// 所属组合的强类型缓存（bool 数组）
    /// </summary>
    protected Memory<bool> Cache => this._cbnt.Cache;

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = GetBitsChannel();
        var bits = await channel.ReadBitsAsync(this.NormalizedAddress(), this.TagSize(), ct);
        bits.CopyTo(this.Cache.Slice(this.CacheOffset, bits.Length));
        this.NotifyTagRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel = GetBitsChannel();
        await channel.WriteBitsAsync(this.NormalizedAddress(), this.Cache.Slice(this.CacheOffset, this.TagSize()).ToArray(), ct);
        this.NotifyTagWritten();
        this.IsDirty = false;
    }

    private IModbusBitsChannel GetBitsChannel()
    {
        var channel0 = this.TagCbnt.SearchRequiredChannel();
        var channel = channel0 as IModbusBitsChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"Modbus 位空间组合子默认实现依赖于通道{nameof(IModbusBitsChannel)}，但当前实际通道是{channel0.GetType().Name}。当前测点组合子名称={this.TagName()}");
        }
        return channel;
    }
}

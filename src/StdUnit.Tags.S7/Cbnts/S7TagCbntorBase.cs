using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7 组合子基类：构造时强类型绑定 <see cref="TagCbnt{T}"/>（T=byte），
/// 组合子明确知道自己解读的缓存是"PLC 内存原始字节"（西门子大端，按 EndianKind 直读）。<br/>
/// <see cref="ReadAsync"/> / <see cref="WriteAsync"/> 走 <see cref="S7TagChannel"/> 的同步 Span 字节读写模板
/// （直接读写本组合的缓存，零额外分配），子类只需实现 <c>Value</c> 的解读。<br/>
/// 类型不匹配（构造时传入非 <see cref="TagCbnt{T}"/>（T=byte）组合）立即抛出可诊断错误。
/// </summary>
public abstract class S7TagCbntorBase : TagCbntor
{
    private readonly TagCbnt<byte> _cbnt;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">必须是 <see cref="TagCbnt{T}"/>（T=byte）</param>
    /// <param name="tagOffset"></param>
    /// <param name="cacheOffset"></param>
    internal S7TagCbntorBase(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int tagOffset, int cacheOffset)
        : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
    {
        this._cbnt = tagCbnt;
    }

    /// <summary>
    /// 所属组合的强类型缓存（原始字节）
    /// </summary>
    protected Memory<byte> Cache => this._cbnt.Cache;

    /// <inheritdoc/>
    public override Task ReadAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        channel.Read(this.NormalizedAddress(), this.Cache.Span.Slice(this.CacheOffset, this.TagSize()), ct);
        this.NotifyTagRead();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(CancellationToken ct)
    {
        var channel = GetByteChannel();
        channel.Write(this.NormalizedAddress(), this.Cache.Span.Slice(this.CacheOffset, this.TagSize()), ct);
        this.NotifyTagWritten();
        this.IsDirty = false;
        return Task.CompletedTask;
    }

    private S7TagChannel GetByteChannel()
    {
        var channel0 = this.TagCbnt.SearchRequiredChannel();
        var channel = channel0 as S7TagChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"S7 组合子默认实现依赖于通道{nameof(S7TagChannel)}，但当前实际通道是{channel0.GetType().Name}。当前测点组合子名称={this.TagName()}");
        }
        return channel;
    }
}

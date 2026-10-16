using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7 测点组合：缓存 = PLC 内存原始字节（T=byte），
/// 读写直接打在 <see cref="S7TagChannel"/> 上（同步 Span 字节读写，缓冲区就是本组合的缓存，零额外分配）。
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
            throw new NotImplementedException($"通道组合({nameof(S7TagCbnt)})依赖于通道{nameof(S7TagChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

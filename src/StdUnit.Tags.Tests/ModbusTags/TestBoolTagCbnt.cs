using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 测试用 <see cref="TagCbnt{T}"/>（T=bool）具体实现：
/// 基于 <see cref="IModbusBitsChannel"/> 的 bool[] 直通读写，用于位空间 Cbntor 的容器测试。
/// </summary>
internal class TestBoolTagCbnt : TagCbnt<bool>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal TestBoolTagCbnt(TagCbntDescriptor descriptor) : base(descriptor)
    {
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel0 = this.SearchRequiredChannel();
        if (channel0 is not IModbusBitsChannel bits)
        {
            throw new NotImplementedException($"通道组合({nameof(TestBoolTagCbnt)})依赖于通道{nameof(IModbusBitsChannel)}，但当前实际通道是{channel0.GetType().Name}");
        }
        this.Cache = await bits.ReadBitsAsync(this.StartAddress, this.CacheSize, ct);
        this.NotifyChildrenRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel0 = this.SearchRequiredChannel();
        if (channel0 is not IModbusBitsChannel bits)
        {
            throw new NotImplementedException($"通道组合({nameof(TestBoolTagCbnt)})依赖于通道{nameof(IModbusBitsChannel)}，但当前实际通道是{channel0.GetType().Name}");
        }
        await bits.WriteBitsAsync(this.StartAddress, this.Cache.ToArray(), ct);
        this.NotifyChildrenWritten();
    }
}

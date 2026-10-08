using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（寄存器）组合子基类。<br/>
/// <br/>
/// <b>偏移语义</b>：<see cref="TagCbntor.TagOffset"/> = 字节偏移（与 <see cref="ITagCbntor"/> 契约一致，供布局计算）；
/// <see cref="TagCbntor.CacheOffset"/> = 寄存器索引（= TagOffset / 2），是读取 <see cref="TagCbnt{T}"/>（T=ushort）缓存的下标。<br/>
/// <br/>
/// <b>字节序模型</b>：缓存元素即寄存器数值（NModbus 已按协议解析成数值），因此 <c>endian</c> 描述<b>每个 16 位单元
/// （寄存器）内部两个字节的顺序</b>（见 <see cref="ApplyEndian"/>）；32/64 位把多个寄存器拼成一个数值时，
/// <b>寄存器之间</b>的顺序由可选的 <c>interpret</c> 描述（见 <see cref="ModbusInterpret"/>）。
/// 这与直接测点（<see cref="MultipleBytesDirectTag{T}"/>）以及 S7 驱动的语义一致（Modbus 多出 <c>interpret</c>
/// 这个自由度）——<b>同一份 XML 无论写成直接测点还是组合成员，都得到同一个物理值</b>。<b>主机端序不参与</b>：
/// NModbus 已把链路上那几个寄存器的大端字节还原成数值，完整说明见项目根目录的 Notes.md。<br/>
/// <br/>
/// <b>可写性</b>：输入寄存器只读，通过 <see cref="IsReadOnly"/> 表达，写入抛 <see cref="NotSupportedException"/>。<br/>
/// </summary>
internal abstract class ModbusRegisterCbntorBase : TagCbntor
{
    private readonly TagCbnt<ushort> _cbnt;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 字空间组合（寄存器缓存）</param>
    /// <param name="tagOffset">字节偏移（必须为偶数）</param>
    /// <param name="isReadOnly">是否只读（输入寄存器）</param>
    internal ModbusRegisterCbntorBase(TagDescriptor tagDescriptor, TagCbnt<ushort> tagCbnt, int tagOffset, bool isReadOnly)
        : base(tagDescriptor, tagCbnt, tagOffset, tagOffset / 2)
    {
        if (tagOffset % 2 != 0)
        {
            throw new TagsProjectConfigurationException(
                $"寄存器组合子要求字节偏移必须为偶数，当前 offset={tagOffset}（测点地址={tagDescriptor.RawAddress}）",
                $"Tag({tagDescriptor.TagName})");
        }
        this._cbnt = tagCbnt;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    /// 是否只读（输入寄存器）。只读组合子的写入抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// 寄存器缓存视图（强类型，每元素 = 一个寄存器值）
    /// </summary>
    protected Memory<ushort> RegCache => this._cbnt.Cache;

    /// <summary>
    /// 本测点起始寄存器索引
    /// </summary>
    protected int RegOffset => this.CacheOffset;

    /// <summary>
    /// 本测点占用寄存器数（= TagSize 字节数 / 2）
    /// </summary>
    protected int RegCount => this.TagSize() / 2;

    /// <summary>
    /// 按 <c>endian</c> 解读/写入单个寄存器内的两个字节：<br/>
    /// <c>BigEndian</c>（高位在前）原样返回；<c>LittleEndian</c>（默认，低位在前）交换两个字节。<br/>
    /// <br/>
    /// 供 16 位测点使用——它们只占一个寄存器，没有"寄存器之间"可言，<c>endian</c> 表达的就是寄存器内部字节序。
    /// 这样组合成员与直接测点（<see cref="MultipleBytesDirectTag{T}"/>）、以及 S7 驱动的 16 位测点语义一致。
    /// </summary>
    protected ushort ApplyEndian(ushort reg) => this.TagEndian() == EndianKinds.BigEndian
        ? reg
        : (ushort)((reg >> 8) | (reg << 8));

    /// <summary>
    /// 校验可写性；只读时抛 <see cref="NotSupportedException"/>
    /// </summary>
    protected void EnsureWritable()
    {
        if (this.IsReadOnly)
        {
            throw new NotSupportedException($"输入寄存器点不可写入({this.TagName()})");
        }
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = GetRegisterChannel();
        var regs = await channel.ReadRegistersAsync(this.NormalizedAddress(), this.RegCount, ct);
        regs.CopyTo(this.RegCache.Slice(this.RegOffset, this.RegCount));
        this.NotifyTagRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        this.EnsureWritable();
        var channel = GetRegisterChannel();
        await channel.WriteRegistersAsync(this.NormalizedAddress(), this.RegCache.Slice(this.RegOffset, this.RegCount), ct);
        this.NotifyTagWritten();
        this.IsDirty = false;
    }

    private IModbusRegisterChannel GetRegisterChannel()
    {
        var channel0 = this.TagCbnt.SearchRequiredChannel();
        var channel = channel0 as IModbusRegisterChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"寄存器组合子({nameof(ModbusRegisterCbntorBase)})依赖于通道{nameof(IModbusRegisterChannel)}，但当前实际通道是{channel0.GetType().Name}。当前测点名称={this.TagName()}");
        }
        return channel;
    }
}

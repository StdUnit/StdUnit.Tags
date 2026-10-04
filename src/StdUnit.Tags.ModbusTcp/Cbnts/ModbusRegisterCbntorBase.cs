using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（寄存器）组合子基类。<br/>
/// <br/>
/// <b>偏移语义</b>：<see cref="TagCbntor.TagOffset"/> = 字节偏移（与 <see cref="ITagCbntor"/> 契约一致，供布局计算）；
/// <see cref="TagCbntor.CacheOffset"/> = 寄存器索引（= TagOffset / 2），是读取 <see cref="TagCbnt{T}"/>（T=ushort）缓存的下标。<br/>
/// <br/>
/// <b>字节序模型</b>：缓存元素即寄存器数值（NModbus 已按线序解析），16 位测点直接取值、无寄存器内部字节序问题；
/// 32/64 位测点由 EndianKind 描述<b>寄存器顺序</b>（BigEndian = 高寄存器在前，Modbus 惯例默认）。<br/>
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
            throw new ArgumentException($"寄存器组合子要求字节偏移必须为偶数，当前 offset={tagOffset}");
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

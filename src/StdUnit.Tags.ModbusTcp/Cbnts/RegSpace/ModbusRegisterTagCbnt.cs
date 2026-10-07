using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（保持寄存器/输入寄存器）测点组合：缓存 = 寄存器数组（T=ushort，每元素 = 一个寄存器值）。<br/>
/// 读写基于 <see cref="IModbusRegisterChannel"/>（即 <see cref="ModbusTcpChannel"/>），直接收发 ushort[]，
/// 无字节展平转换。<br/>
/// 寄存器数值由 NModbus 按协议解析成数值，所以组合子层面的排布由 <c>endian</c>（每个寄存器内部两个字节）
/// 与 <c>interpret</c>（32/64 位里各寄存器之间的顺序）描述。
/// </summary>
internal class ModbusRegisterTagCbnt : TagCbnt<ushort>
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal ModbusRegisterTagCbnt(TagCbntDescriptor descriptor) : base(descriptor)
    {
    }

    /// <inheritdoc/>
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = GetRegisterChannel();
        this.Cache = await channel.ReadRegistersAsync(this.StartAddress, this.CacheSize / 2, ct);
        this.NotifyChildrenRead();
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel = GetRegisterChannel();
        await channel.WriteRegistersAsync(this.StartAddress, this.Cache, ct);
        this.NotifyChildrenWritten();
    }

    private IModbusRegisterChannel GetRegisterChannel()
    {
        var channel0 = this.SearchRequiredChannel();
        var channel = channel0 as IModbusRegisterChannel;
        if (channel is null)
        {
            throw new NotImplementedException($"通道组合({nameof(ModbusRegisterTagCbnt)})依赖于通道{nameof(IModbusRegisterChannel)}，但当前实际通道是{channel0.GetType().Name}，如有必要，请考虑提供自己的通道组合实现。当前测点组合名称={this.TagName()}");
        }
        return channel;
    }
}

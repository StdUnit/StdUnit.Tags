using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 字空间（保持寄存器/输入寄存器）通道接口：直接读写 ushort[] 寄存器数组。<br/>
/// 寄存器数值由 NModbus 按协议解析成数值（与 CPU 端无关），因此通道层不参与任何字节序调整，
/// 组合层只需处理多寄存器数值的排布（<c>endian</c> + <c>interpret</c>，见 <see cref="ModbusValueInterpreter{T}"/>）。
/// 相比走 <see cref="IContinuousBytesBasedTagChannel"/> 的
/// byte 展平再解析，本接口消除两次无谓的字节转换与分配。
/// </summary>
public interface IModbusRegisterChannel : ITagChannel
{
    /// <summary>
    /// 读取寄存器（保持寄存器 FC03 / 输入寄存器 FC04）。<br/>
    /// 返回数组每元素 = 一个寄存器值（NModbus 已按协议解析成数值）。
    /// </summary>
    /// <param name="address">寄存器地址（如 "40001" / "30001"）</param>
    /// <param name="registerCount">寄存器数量</param>
    /// <param name="ct"></param>
    Task<ushort[]> ReadRegistersAsync(string address, int registerCount, CancellationToken ct);

    /// <summary>
    /// 写寄存器（FC16，仅保持寄存器）。<br/>
    /// 数组每元素 = 一个寄存器值。
    /// </summary>
    /// <param name="address">寄存器地址（如 "40001"）</param>
    /// <param name="registers">寄存器值（只读内存视图，避免调用方拷贝）</param>
    /// <param name="ct"></param>
    Task WriteRegistersAsync(string address, ReadOnlyMemory<ushort> registers, CancellationToken ct);
}

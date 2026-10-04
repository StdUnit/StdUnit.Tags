using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 位空间（线圈 0x / 离散输入 1x）通道接口：直接读写 bool[]（NModbus 原生语义，每元素 = 一个地址位的开合状态）。<br/>
/// 位空间地址本身就是 bit（非寄存器），无字节序概念，因此相比走 <see cref="IContinuousBytesBasedTagChannel"/>
/// 的"每 bit 1 字节"打包，本接口更贴近协议且消除无谓转换。
/// </summary>
public interface IModbusBitsChannel : ITagChannel
{
    /// <summary>
    /// 读取线圈/离散输入（FC01/FC02）。<br/>
    /// 返回数组每元素 = 一个地址位的状态（true = 闭合/1）。
    /// </summary>
    /// <param name="address">位地址（如 "00001" / "10001"）</param>
    /// <param name="bitCount">位数</param>
    /// <param name="ct"></param>
    Task<bool[]> ReadBitsAsync(string address, int bitCount, CancellationToken ct);

    /// <summary>
    /// 写线圈（FC05 单点 / FC15 多点，仅 OutputCoils）。<br/>
    /// 数组每元素 = 一个地址位的目标状态。
    /// </summary>
    /// <param name="address">位地址（如 "00001"）</param>
    /// <param name="bits">位状态数组</param>
    /// <param name="ct"></param>
    Task WriteBitsAsync(string address, bool[] bits, CancellationToken ct);
}

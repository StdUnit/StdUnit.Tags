using StdUnit.Tags.ModbusTcp;
using System.Numerics;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// 针脚地址到 Modbus 地址串的转换（格式为 <c>从站号~地址</c>）。
/// </summary>
public static class PinAddrExtensions
{
    /// <summary>
    /// DI 针脚 → Modbus 地址串：输入触点区基址 + 针脚偏移。
    /// </summary>
    /// <param name="pin">DI 针脚</param>
    /// <param name="slave">Modbus 从站号</param>
    /// <returns>形如 <c>1~10001</c> 的地址串（DI1）</returns>
    public static string ToModbusTcpAddr(this DIPinAddr pin, byte slave)
    {
        var addr = ModbusTcpAddress.INPUT_CONTACTS_BASE + (ushort)pin;
        var repr = $"{slave}~{addr:d5}";
        return repr.ToString();
    }

    /// <summary>
    /// DO 针脚 → Modbus 地址串：输出线圈区基址 + 针脚偏移。
    /// </summary>
    /// <param name="pin">DO 针脚</param>
    /// <param name="slave">Modbus 从站号</param>
    /// <returns>形如 <c>1~00017</c> 的地址串（DO1）</returns>
    public static string ToModbusTcpAddr(this DOPinAddr pin, byte slave)
    {
        var addr = ModbusTcpAddress.OUTPUT_COILS_BASE + (ushort)pin;
        var repr = $"{slave}~{addr:d5}";
        return repr;
    }

}

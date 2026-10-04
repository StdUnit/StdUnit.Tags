namespace StdUnit.Tags.Hjzk;

/// <summary>
/// 针脚地址扩展方法
/// </summary>
public static class PinAddrExtensions
{
    /// <summary>
    /// 转成 Modbus TCP 地址表示
    /// </summary>
    /// <param name="pin"></param>
    /// <param name="slave"></param>
    /// <returns></returns>
    public static string ToModbusTcpAddr(this DIPinAddr pin, byte slave)
    {
        var addr = HjzkAddrDefines.BASE_DI + (ushort)pin;
        var repr = $"{slave}~1{addr:d4}";
        return repr;
    }

    /// <summary>
    /// 转成 Modbus TCP 地址表示
    /// </summary>
    /// <param name="pin"></param>
    /// <param name="slave"></param>
    /// <returns></returns>
    public static string ToModbusTcpAddr(this DOPinAddr pin, byte slave)
    {
        var addr = HjzkAddrDefines.BASE_DO + (ushort)pin;
        var repr = $"{slave}~0{addr:d4}";
        return repr;
    }

}

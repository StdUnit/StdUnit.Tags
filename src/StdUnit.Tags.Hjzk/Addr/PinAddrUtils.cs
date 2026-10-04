namespace StdUnit.Tags.Hjzk;

/// <summary>
/// 针脚地址工具类
/// </summary>
public static class PinAddrUtils
{
    /// <summary>
    /// 解析 DI 针脚地址
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="pin"></param>
    /// <returns></returns>
    public static bool TryParseDI(string addr, out DIPinAddr pin)
    {
        var parsed = Enum.TryParse(addr, out pin);
        return parsed;
    }

    /// <summary>
    /// 解析 DO 针脚地址
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="pin"></param>
    /// <returns></returns>
    public static bool TryParseDO(string addr, out DOPinAddr pin)
    {
        var parsed = Enum.TryParse(addr, out pin);
        return parsed;
    }
}
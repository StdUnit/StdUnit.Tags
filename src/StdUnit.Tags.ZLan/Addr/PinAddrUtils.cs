namespace StdUnit.Tags.ZLan;

/// <summary>
/// 针脚名（如 <c>DI1</c>、<c>DO3</c>）到 <see cref="DIPinAddr"/> / <see cref="DOPinAddr"/> 的解析。
/// </summary>
public static class PinAddrUtils
{
    /// <summary>
    /// 解析 DI 针脚名。
    /// </summary>
    /// <param name="pinName">针脚名，与 <see cref="DIPinAddr"/> 的成员同名</param>
    /// <returns>对应的 DI 针脚</returns>
    /// <exception cref="ArgumentException"><paramref name="pinName"/> 不是合法的 DI 针脚名</exception>
    public static DIPinAddr ParseDI(string pinName)
    {
        // net472 无泛型 Enum.Parse<TEnum>(string)（.NET Core 2.0+），用非泛型重载
        return (DIPinAddr)Enum.Parse(typeof(DIPinAddr), pinName);
    }

    /// <summary>
    /// 解析 DO 针脚名。
    /// </summary>
    /// <param name="pinName">针脚名，与 <see cref="DOPinAddr"/> 的成员同名</param>
    /// <returns>对应的 DO 针脚</returns>
    /// <exception cref="ArgumentException"><paramref name="pinName"/> 不是合法的 DO 针脚名</exception>
    public static DOPinAddr ParseDO(string pinName)
    {
        return (DOPinAddr)Enum.Parse(typeof(DOPinAddr), pinName);
    }
}

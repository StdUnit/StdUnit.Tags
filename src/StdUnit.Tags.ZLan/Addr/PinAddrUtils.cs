namespace StdUnit.Tags.ZLan;

public static class PinAddrUtils
{
    public static DIPinAddr ParseDI(string pinName)
    {
        // net472 无泛型 Enum.Parse<TEnum>(string)（.NET Core 2.0+），用非泛型重载
        return (DIPinAddr)Enum.Parse(typeof(DIPinAddr), pinName);
    }

    public static DOPinAddr ParseDO(string pinName)
    {
        return (DOPinAddr)Enum.Parse(typeof(DOPinAddr), pinName);
    }
}

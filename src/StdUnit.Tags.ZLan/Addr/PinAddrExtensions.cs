using StdUnit.Tags.ModbusTcp;
using System.Numerics;

namespace StdUnit.Tags.ZLan;

public static class PinAddrExtensions
{
    public static string ToModbusTcpAddr(this DIPinAddr pin, byte slave)
    {
        var addr = ModbusTcpAddress.INPUT_CONTACTS_BASE + (ushort)pin;
        var repr = $"{slave}~{addr:d5}";
        return repr.ToString();
    }

    public static string ToModbusTcpAddr(this DOPinAddr pin, byte slave)
    {
        var addr = ModbusTcpAddress.OUTPUT_COILS_BASE + (ushort)pin;
        var repr = $"{slave}~{addr:d5}";
        return repr;
    }

}

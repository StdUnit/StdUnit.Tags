using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

public class ZLanDOCbntBuilder : ZLanCbntBuilderBase
{
    const ushort u_DO_START_ADDRESS = ModbusTcpAddress.OUTPUT_COILS_BASE + (ushort)DOPinAddr.DO1;
    static string DO_START_ADDR = $"{u_DO_START_ADDRESS:d5}";

    public ZLanDOCbntBuilder() : base()
    {
    }
    public override string AreaStartAddr => DO_START_ADDR;
}


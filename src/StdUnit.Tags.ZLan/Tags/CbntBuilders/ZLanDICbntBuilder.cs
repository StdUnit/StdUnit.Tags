using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

public class ZLanDICbntBuilder : ZLanCbntBuilderBase
{
    const ushort u_DI_START_ADDRESS = ModbusTcpAddress.INPUT_CONTACTS_BASE + (ushort)DIPinAddr.DI1;
    static string DI_START_ADDR = $"{u_DI_START_ADDRESS:d5}";


    public ZLanDICbntBuilder() : base()
    {
    }


    /// <summary>
    /// 区域
    /// </summary>
    public override string? Area { get; protected set; }

    /// <summary>
    /// 区域起始地址
    /// </summary>
    public override string AreaStartAddr => DI_START_ADDR;

}


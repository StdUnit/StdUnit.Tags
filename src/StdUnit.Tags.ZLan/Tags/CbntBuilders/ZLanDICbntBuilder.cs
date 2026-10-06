using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan DI（数字量输入）测点组合构建器：区域起始地址为输入触点区基址 + <see cref="DIPinAddr.DI1"/>。
/// </summary>
public class ZLanDICbntBuilder : ZLanCbntBuilderBase
{
    const ushort u_DI_START_ADDRESS = ModbusTcpAddress.INPUT_CONTACTS_BASE + (ushort)DIPinAddr.DI1;
    static string DI_START_ADDR = $"{u_DI_START_ADDRESS:d5}";


    /// <summary>
    /// c'tor
    /// </summary>
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


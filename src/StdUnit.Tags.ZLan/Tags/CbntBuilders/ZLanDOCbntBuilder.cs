using StdUnit.Tags.ModbusTcp;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan DO（数字量输出）测点组合构建器：区域起始地址为输出线圈区基址 + <see cref="DOPinAddr.DO1"/>。
/// </summary>
public class ZLanDOCbntBuilder : ZLanCbntBuilderBase
{
    const ushort u_DO_START_ADDRESS = ModbusTcpAddress.OUTPUT_COILS_BASE + (ushort)DOPinAddr.DO1;
    static string DO_START_ADDR = $"{u_DO_START_ADDRESS:d5}";

    /// <summary>
    /// c'tor
    /// </summary>
    public ZLanDOCbntBuilder() : base()
    {
    }
    /// <summary>
    /// 区域起始地址
    /// </summary>
    public override string AreaStartAddr => DO_START_ADDR;
}


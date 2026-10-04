
namespace StdUnit.Tags.Hjzk;

/// <summary>
/// Hjzk DO 测点组构建器
/// </summary>
public class HjzkDOCbntBuilder : HjzkCbntBuilderBase
{
    /// <summary>
    /// Modbus地址解析器会自动将这里的地址-1, 这里需要人为加1
    /// </summary>
    const ushort u_DO_START_ADDRESS = HjzkAddrDefines.BASE_DO + 1;
    static string DO_START_ADDR = $"{u_DO_START_ADDRESS:d5}";


    /// <summary>
    /// c'tor
    /// </summary>
    public HjzkDOCbntBuilder() : base()
    {
    }

    /// <inheritdoc/>
    public override string AreaStartAddr => DO_START_ADDR;
}


namespace StdUnit.Tags.S7;

/// <summary>
/// 西门子PLC项
/// </summary>
public class S7PlcItem
{
    /// <summary>
    /// IP 地址
    /// </summary>
    public string IpAddr { get; set; } = "127.0.0.1";
    /// <summary>
    /// 机架号
    /// </summary>
    public short Rack { get; set; } = 0;
    /// <summary>
    /// 槽号
    /// </summary>
    public short Slot { get; set; } = 1;

    /// <summary>
    /// 连接类型
    /// </summary>
    public ushort ConnectionType { get; set; } = 3;
}
namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// ModbusTcp 通道配置项
/// </summary>
public class ModbusTcpItem
{
    /// <summary>
    /// Ip 地址，默认值为 localhost
    /// </summary>
    public string IpAddr { get; set; } = "localhost";

    /// <summary>
    /// 端口，默认值为 502
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// 读超时
    /// </summary>
    public int ReadTimeout { get; set; } = 10000;

    /// <summary>
    /// 写超时
    /// </summary>
    public int WriteTimeout { get; set; } = 10000;

    /// <summary>
    /// 连接超时
    /// </summary>
    public int ConnTimeout { get; set; } = 1000;

    /// <summary>
    /// 单帧最多写入的寄存器数量(FC16)。null 表示使用协议默认值(123)。<br/>
    /// 某些设备的单帧上限小于协议理论值，可通过此项配置更小的值。<br/>
    /// </summary>
    public ushort? MaxWriteRegisters { get; set; }

    /// <summary>
    /// 单帧最多读取的寄存器数量(FC03/FC04)。null 表示使用协议默认值(125)。<br/>
    /// 某些设备的单帧上限小于协议理论值，可通过此项配置更小的值。<br/>
    /// </summary>
    public ushort? MaxReadRegisters { get; set; }

    /// <summary>
    /// 单帧最多读取的位数/点数(FC01/FC02)。null 表示使用协议默认值(2000)。<br/>
    /// 某些设备的单帧上限小于协议理论值，可通过此项配置更小的值。<br/>
    /// </summary>
    public ushort? MaxReadBits { get; set; }
}

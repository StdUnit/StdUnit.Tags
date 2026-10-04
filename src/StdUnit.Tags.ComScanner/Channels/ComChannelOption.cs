using System.IO.Ports;

namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// 串口通道选项
/// </summary>
public class ComChannelOption
{

    /// <summary>
    /// 串口号。默认值为 COM1。<br/>
    /// </summary>
    public string Port { get; set; } = "COM1";

    /// <summary>
    /// 波特率
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// Parity
    /// </summary>
    public Parity Parity { get; set; }

    /// <summary>
    /// 数据位
    /// </summary>
    public int DataBits { get; set; }

    /// <summary>
    /// 停止位
    /// </summary>
    public StopBits StopBits { get; set; }

    /// <summary>
    /// 通道元素数量
    /// </summary>
    public int ChannelCapacity { get; set; } = 1;

    /// <summary>
    /// 读取整行？<br/>
    /// </summary>
    public bool ReadEntireLine { get; set; } = true;

    /// <summary>
    /// 换行符。null表示使用系统默认的换行符。<br/>
    /// </summary>
    public string? NewLine { get; set; }

    /// <summary>
    /// 脚本
    /// </summary>
    public string? ReadScript { get; set; }

    /// <summary>
    /// 脚本调试开关，开启后会将脚本内容写入临时文件，并在编译时附加调试信息，以便在调试器中查看脚本代码
    /// </summary>
    public bool ReadScriptDebugInformationEnabled { get; set; } = false;
}

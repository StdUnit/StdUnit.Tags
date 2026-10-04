namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// 串口操作行为的抽象接口。
/// 仅定义 Open/ReadLine/Write 等行为，不暴露 <see cref="System.IO.Ports.SerialPort"/> 类型。
/// 测试场景可通过此接口直接实现 Mock，无需涉及物理串口。
/// <see cref="SerialPortAdapter"/> 是包装真实 SerialPort 的默认实现。
/// </summary>
public interface ISerialPortHandle : IDisposable
{
    /// <summary>
    /// 串口是否已打开
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// 换行符
    /// </summary>
    string NewLine { get; set; }

    /// <summary>
    /// 打开串口
    /// </summary>
    void Open();

    /// <summary>
    /// 关闭串口
    /// </summary>
    void Close();

    /// <summary>
    /// 读取一行数据
    /// </summary>
    string ReadLine();

    /// <summary>
    /// 读取缓冲区中的现有数据
    /// </summary>
    string ReadExisting();

    /// <summary>
    /// 写入字符串
    /// </summary>
    void Write(string str);

    /// <summary>
    /// 写入字节数组
    /// </summary>
    void Write(byte[] buffer, int offset, int count);
}

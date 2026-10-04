using System.IO.Ports;

namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// <see cref="ISerialPortHandle"/> 的默认实现，包装真实的 <see cref="SerialPort"/>。
/// 所有方法委托给内部的 <see cref="SerialPort"/> 实例。
/// </summary>
/// <remarks>
/// 注意：<see cref="InnerPort"/> 属性不在 <see cref="ISerialPortHandle"/> 接口中，
/// 因此接口层不泄露 <see cref="SerialPort"/> 类型。
/// <see cref="ScriptBasedComChannel{T}"/> 可通过向下转型获取真实端口用于脚本引擎。
/// </remarks>
public sealed class SerialPortAdapter : ISerialPortHandle
{
    private readonly SerialPort _port;

    /// <summary>
    /// 获取包装的真实 <see cref="SerialPort"/> 实例。
    /// </summary>
    public SerialPort InnerPort => _port;

    /// <summary>
    /// c'tor
    /// </summary>
    public SerialPortAdapter(SerialPort port)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
    }

    /// <inheritdoc/>
    public bool IsOpen => _port.IsOpen;

    /// <inheritdoc/>
    public string NewLine { get => _port.NewLine; set => _port.NewLine = value; }

    /// <inheritdoc/>
    public void Open() => _port.Open();

    /// <inheritdoc/>
    public void Close() => _port.Close();

    /// <inheritdoc/>
    public string ReadLine() => _port.ReadLine();

    /// <inheritdoc/>
    public string ReadExisting() => _port.ReadExisting();

    /// <inheritdoc/>
    public void Write(string str) => _port.Write(str);

    /// <inheritdoc/>
    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    /// <inheritdoc/>
    public void Dispose() => _port.Dispose();
}

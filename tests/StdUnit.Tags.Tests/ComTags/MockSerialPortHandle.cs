using StdUnit.Tags.ComScanner.Channels;
using System;
using System.Collections.Generic;

namespace StdUnit.Tags.Tests.ComTags;

/// <summary>
/// 用于单元测试的 Mock 串口句柄。
/// 直接实现 <see cref="ISerialPortHandle"/>，无需涉及物理串口。
/// </summary>
public class MockSerialPortHandle : ISerialPortHandle
{
    /// <summary>
    /// ReadLine() 返回的队列。按先进先出顺序返回。
    /// </summary>
    public Queue<string> ReadLineQueue { get; } = new();

    /// <summary>
    /// 用于设置 ReadExisting() 返回的值。
    /// </summary>
    public string ReadExistingReturnValue { get; set; } = string.Empty;

    /// <summary>
    /// Open() 被调用的次数
    /// </summary>
    public int OpenCallCount { get; private set; }

    /// <summary>
    /// Close() 被调用的次数
    /// </summary>
    public int CloseCallCount { get; private set; }

    /// <summary>
    /// Write(string) 被调用的次数
    /// </summary>
    public int WriteStringCallCount { get; private set; }

    /// <summary>
    /// 最近一次 Write(string) 写入的字符串
    /// </summary>
    public string? LastWrittenString { get; private set; }

    /// <summary>
    /// Write(byte[], int, int) 被调用的次数
    /// </summary>
    public int WriteBytesCallCount { get; private set; }

    /// <summary>
    /// 串口是否已打开
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// 换行符
    /// </summary>
    public string NewLine { get; set; } = "\n";

    public void Open()
    {
        OpenCallCount++;
        IsOpen = true;
    }

    public void Close()
    {
        CloseCallCount++;
        IsOpen = false;
    }

    /// <summary>
    /// ReadLine 空队列时的兜底行为：返回默认值而非抛异常。
    /// 设为 true 时（默认），空队列返回 string.Empty 防止后台轮询线程崩溃；
    /// 设为 false 时，空队列抛 InvalidOperationException。
    /// 在需要精确验证读取逻辑的测试中可设为 false。
    /// </summary>
    public bool FallbackToDefaultOnEmptyQueue { get; set; } = true;

    public string ReadLine()
    {
        // 不能写成 Queue<T>.TryDequeue —— 那是 .NET Core 2.0+ 的 API，net472 没有
        if (ReadLineQueue.Count > 0)
            return ReadLineQueue.Dequeue();
        if (FallbackToDefaultOnEmptyQueue)
            return string.Empty;
        throw new InvalidOperationException("MockSerialPortHandle: ReadLineQueue 为空。");
    }

    public string ReadExisting()
    {
        return ReadExistingReturnValue;
    }

    public void Write(string str)
    {
        WriteStringCallCount++;
        LastWrittenString = str;
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        WriteBytesCallCount++;
    }

    public void Dispose()
    {
        IsOpen = false;
    }
}

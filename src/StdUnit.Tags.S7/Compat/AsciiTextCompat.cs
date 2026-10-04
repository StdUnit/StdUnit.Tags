using System;
using System.Text;

namespace StdUnit.Tags.S7.Compat;

/// <summary>
/// ASCII 文本与字节序列的互转（span 友好）。<br/>
/// <br/>
/// 存在的理由：net472 没有 <c>Encoding.GetString(ReadOnlySpan&lt;byte&gt;)</c> 与
/// <c>Encoding.GetBytes(string, Span&lt;byte&gt;)</c>（.NET Core 2.1+ 才加入）。<br/>
/// net8.0 直接转发到 <see cref="Encoding.ASCII"/>（有标准库就用标准）；
/// net472 用 <c>ToArray()</c> / 临时数组做等价实现。<br/>
/// <br/>
/// 方法签名与 <see cref="Encoding"/> 上的对应重载保持一致，便于对照与将来替换。<br/>
/// <br/>
/// 契约差异提醒：<see cref="GetBytes"/> 在目标缓冲区不足时**抛 <see cref="ArgumentException"/>**
/// （与标准库行为一致，不做截断）。两个框架的异常**消息**不同
/// （net8.0 由 Encoder 抛出，net472 由 <c>Array.CopyTo</c> 抛出），
/// 因此调用方只应依赖异常类型，不要依赖消息文本。
/// </summary>
internal static class AsciiTextCompat
{
    /// <summary>
    /// 把字节序列按 ASCII 解码为字符串。
    /// </summary>
    internal static string GetString(ReadOnlySpan<byte> bytes)
    {
#if NETFRAMEWORK
        // net472 无 Span 重载，先拷贝成数组
        return Encoding.ASCII.GetString(bytes.ToArray());
#else
        return Encoding.ASCII.GetString(bytes);
#endif
    }

    /// <summary>
    /// 把字符串按 ASCII 编码写入目标缓冲区，返回写入的字节数。
    /// </summary>
    internal static int GetBytes(string text, Span<byte> destination)
    {
#if NETFRAMEWORK
        // net472 无 Span 重载，先编码成数组再拷贝
        var tmp = Encoding.ASCII.GetBytes(text);
        tmp.CopyTo(destination);
        return tmp.Length;
#else
        return Encoding.ASCII.GetBytes(text, destination);
#endif
    }
}

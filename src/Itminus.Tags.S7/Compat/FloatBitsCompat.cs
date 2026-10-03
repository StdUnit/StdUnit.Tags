using System;
using System.Buffers.Binary;
#if NETFRAMEWORK
using System.Runtime.CompilerServices;
#endif

namespace Itminus.Tags.S7.Compat;

/// <summary>
/// 单精度浮点与字节序列的互转（按指定字节序）。<br/>
/// <br/>
/// 存在的理由：net472 没有 <c>BinaryPrimitives.Read/WriteSingleBigEndian/LittleEndian</c>
/// （.NET 5+ 才加入）。这里把该差异集中到一处，调用点无需铺 <c>#if</c>。<br/>
/// net8.0 直接用标准库实现（有标准库就用标准）；
/// net472 用「int32 大/小端读写 + 按位重解释」的等价实现，零分配。
/// </summary>
internal static class FloatBitsCompat
{
    /// <summary>
    /// 按指定字节序从 4 字节中读出 float。
    /// </summary>
    internal static float Read(ReadOnlySpan<byte> span, bool bigEndian)
    {
#if NETFRAMEWORK
        var raw = bigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(span)
            : BinaryPrimitives.ReadInt32LittleEndian(span);
        return Unsafe.As<int, float>(ref raw);
#else
        return bigEndian
            ? BinaryPrimitives.ReadSingleBigEndian(span)
            : BinaryPrimitives.ReadSingleLittleEndian(span);
#endif
    }

    /// <summary>
    /// 按指定字节序把 float 写入 4 字节。
    /// </summary>
    internal static void Write(Span<byte> span, float value, bool bigEndian)
    {
#if NETFRAMEWORK
        var raw = Unsafe.As<float, int>(ref value);
        if (bigEndian)
        {
            BinaryPrimitives.WriteInt32BigEndian(span, raw);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(span, raw);
        }
#else
        if (bigEndian)
        {
            BinaryPrimitives.WriteSingleBigEndian(span, value);
        }
        else
        {
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
        }
#endif
    }
}

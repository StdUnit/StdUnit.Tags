using System;
#if NETFRAMEWORK
using System.Runtime.CompilerServices;
#endif

namespace Itminus.Tags.ModbusTcp.Compat;

/// <summary>
/// float 与其 IEEE754 位模式（uint）的互转。<br/>
/// <br/>
/// 存在的理由：net472 没有 <c>BitConverter.SingleToUInt32Bits</c> / <c>UInt32BitsToSingle</c>
/// （.NET Core 2.0+ 才加入）。这里把该差异集中到一处，调用点无需铺 <c>#if</c>。<br/>
/// net8.0 直接用标准库实现（有标准库就用标准）；
/// net472 用 <c>Unsafe</c> 按位重解释，零分配。
/// </summary>
internal static class FloatBitsCompat
{
    /// <summary>
    /// 位模式 → float。
    /// </summary>
    internal static float ToSingle(uint bits)
    {
#if NETFRAMEWORK
        return Unsafe.As<uint, float>(ref bits);
#else
        return BitConverter.UInt32BitsToSingle(bits);
#endif
    }

    /// <summary>
    /// float → 位模式。
    /// </summary>
    internal static uint ToBits(float value)
    {
#if NETFRAMEWORK
        return Unsafe.As<float, uint>(ref value);
#else
        return BitConverter.SingleToUInt32Bits(value);
#endif
    }
}

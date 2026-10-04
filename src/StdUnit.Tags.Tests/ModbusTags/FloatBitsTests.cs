using StdUnit.Tags.ModbusTcp.Compat;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// 钉住 <c>StdUnit.Tags.ModbusTcp.Compat.FloatBitsCompat</c> 的语义：float ↔ uint 的 <b>位重解释</b>
/// （bit reinterpretation），而不是数值转换（numeric conversion）。<br/>
/// <br/>
/// 为什么值得单独测：net472 分支用 <c>Unsafe.As&lt;uint, float&gt;(ref bits)</c> 实现，
/// 名字听起来像“转换”，容易让人怀疑它做的是 <c>(float)bits</c> 那种数值转换。
/// 实际上 <c>Unsafe.As</c> 只是把<b>引用</b>重新标注为另一种类型，内存原封不动，
/// 因此读出来的就是对同一段 4 字节按 float 解读——与标准库的
/// <c>BitConverter.UInt32BitsToSingle</c> 完全等价。<br/>
/// <br/>
/// 用例全部采用「字面量进 → 字面量出」，<b>不借助 <c>BitConverter.GetBytes</c> 之类的参照实现</b>：
/// 一来基准更直白，二来避免把这个「虽然 API 统一、但有堆分配」的写法带进代码库——
/// 该类型服务于轮询热路径，生产实现是零分配的。
/// <br/>
/// 这些用例在两个目标框架下都必须通过；若哪天有人把实现改成数值转换，这里会立刻报警。
/// </summary>
public class FloatBitsTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(-2.5f)]
    [InlineData(1.5f)]
    [InlineData(0f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    public void Roundtrip(float value)
    {
        var bits = FloatBitsCompat.ToBits(value);
        var parsed = FloatBitsCompat.ToSingle(bits);
        Assert.Equal(value, parsed);

        Assert.Equal(value, FloatBitsCompat.ToSingle(bits));
    }

    [Theory]
    [InlineData(1.0f, 0x3F800000u)]
    [InlineData(-1.0f, 0xBF800000u)]
    [InlineData(-2.5f, 0xC0200000u)]
    [InlineData(1.5f, 0x3FC00000u)]
    [InlineData(0f, 0x00000000u)]
    [InlineData(float.MaxValue, 0x7F7FFFFFu)]
    [InlineData(float.MinValue, 0xFF7FFFFFu)]
    [InlineData(float.Epsilon, 0x00000001u)]
    public void ToBits_ReinterpretsBits(float value, uint expected)
    {
        var actual = FloatBitsCompat.ToBits(value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0x3F800000u, 1.0f)]
    [InlineData(0xBF800000u, -1.0f)]
    [InlineData(0xC0200000u, -2.5f)]
    [InlineData(0x3FC00000u, 1.5f)]
    [InlineData(0x00000000u, 0f)]
    [InlineData(0x7F7FFFFFu, float.MaxValue)]
    [InlineData(0xFF7FFFFFu, float.MinValue)]
    [InlineData(0x00000001u, float.Epsilon)]
    public void ToSingle_ReinterpretsBits(uint bits, float expected)
    {
        var actual = FloatBitsCompat.ToSingle(bits);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToSingle_IsBitReinterpretation_NotNumericConversion()
    {
        // 关键反例：若实现误用数值转换（(float)bits），1065353216 会变成 1.06535322E9f。
        // 位重解释必须得到 1.0f。
        var actual = FloatBitsCompat.ToSingle(1065353216u); // == 0x3F800000

        Assert.Equal(1.0f, actual);
        Assert.NotEqual(1065353216f, actual);
    }

    [Fact]
    public void ToBits_ThenToSingle_RoundTrips()
    {
        foreach (var value in new[] { 0f, 1.0f, -1.0f, 3.1415927f, float.MaxValue, float.Epsilon })
        {
            Assert.Equal(value, FloatBitsCompat.ToSingle(FloatBitsCompat.ToBits(value)));
        }
    }
}

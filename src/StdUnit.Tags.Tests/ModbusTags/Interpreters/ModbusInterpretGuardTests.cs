using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// <see cref="ModbusInterpret"/> 的边界守卫：只有 4/8 字节的多寄存器数值才有"寄存器之间"的排布可枚举，
/// 其它字节数（2 字节的 16 位、以及任何越界值）必须直接报错。
/// </summary>
public class ModbusInterpretGuardTests
{
    /// <summary>4 字节 = 2 个 16 位单元 ⇒ 2 种单元排列 × 2 种 endian = 4 种；8 字节 = 4! × 2 = 48 种</summary>
    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 48)]
    public void EnumeratePackings_SupportedByteCount(int byteCount, int expectedCount)
    {
        Assert.Equal(expectedCount, ModbusInterpret.EnumeratePackings(byteCount).Length);
    }

    /// <summary>其它字节数没有排布可言</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(16)]
    public void EnumeratePackings_UnsupportedByteCount_Throws(int byteCount)
    {
        var ex = Assert.Throws<TagsProjectConfigurationException>(
            () => ModbusInterpret.EnumeratePackings(byteCount));

        Assert.Contains("4/8", ex.Message);
    }
}

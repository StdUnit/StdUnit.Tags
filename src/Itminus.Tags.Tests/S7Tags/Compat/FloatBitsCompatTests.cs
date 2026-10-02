using System;
using Itminus.Tags.S7.Compat;
using Xunit;

namespace Itminus.Tags.Tests.S7Tags.Compat;

/// <summary>
/// <see cref="FloatBitsCompat"/> 的测试：按指定字节序在 4 字节与单精度浮点之间互转。<br/>
/// <br/>
/// 为什么值得直接测：net472 分支是「int32 大/小端读写 + <c>Unsafe.As</c> 位重解释」，
/// net8.0 分支是标准库的 <c>BinaryPrimitives.Read/WriteSingle*</c>，两条实现路径必须产出**逐字节相同**的结果。
/// 字节序搞反在这里是静默的（数值仍然是一个合法 float，只是错的），所以用显式字节数组钉死布局。
/// </summary>
public class FloatBitsCompatTests
{
    // 1.0f 的位模式是 0x3F800000
    private const float One = 1.0f;

    [Fact]
    public void Write_BigEndian_ProducesHighByteFirst()
    {
        var buffer = new byte[4];

        FloatBitsCompat.Write(buffer, One, bigEndian: true);

        Assert.Equal(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, buffer);
    }

    [Fact]
    public void Write_LittleEndian_ProducesLowByteFirst()
    {
        var buffer = new byte[4];

        FloatBitsCompat.Write(buffer, One, bigEndian: false);

        Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, buffer);
    }

    [Fact]
    public void Read_BigEndian_DecodesHighByteFirst()
    {
        var bytes = new byte[] { 0x3F, 0x80, 0x00, 0x00 };

        var value = FloatBitsCompat.Read(bytes, bigEndian: true);

        Assert.Equal(One, value);
    }

    [Fact]
    public void Read_LittleEndian_DecodesLowByteFirst()
    {
        var bytes = new byte[] { 0x00, 0x00, 0x80, 0x3F };

        var value = FloatBitsCompat.Read(bytes, bigEndian: false);

        Assert.Equal(One, value);
    }

    [Fact]
    public void BigEndian_And_LittleEndian_AreByteReverses()
    {
        var big = new byte[4];
        var little = new byte[4];

        FloatBitsCompat.Write(big, One, bigEndian: true);
        FloatBitsCompat.Write(little, One, bigEndian: false);

        big.AsSpan().Reverse();
        Assert.Equal(big, little);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(1.5f)]
    [InlineData(-2.5f)]
    [InlineData(3.1415927f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(float.Epsilon)]
    public void RoundTrip_BothEndians_PreserveValue(float value)
    {
        foreach (var bigEndian in new[] { true, false })
        {
            var buffer = new byte[4];

            FloatBitsCompat.Write(buffer, value, bigEndian);
            var actual = FloatBitsCompat.Read(buffer, bigEndian);

            Assert.Equal(value, actual);
        }
    }

    [Fact]
    public void Read_MismatchedEndian_IsNotEqual()
    {
        // 反例保护：大端写、小端读必须得到不同的数（否则说明 bigEndian 参数被忽略）
        var buffer = new byte[4];
        FloatBitsCompat.Write(buffer, One, bigEndian: true);

        var mismatched = FloatBitsCompat.Read(buffer, bigEndian: false);

        Assert.NotEqual(One, mismatched);
    }

    [Fact]
    public void Write_FromOffset_OnlyTouchesFourBytes()
    {
        // span 通常是缓存切出来的片段，写入不能越界污染相邻字节
        var buffer = new byte[8];
        buffer.AsSpan().Fill(0xAA);

        FloatBitsCompat.Write(buffer.AsSpan(2), One, bigEndian: true);

        Assert.Equal(new byte[] { 0xAA, 0xAA, 0x3F, 0x80, 0x00, 0x00, 0xAA, 0xAA }, buffer);
    }

    [Fact]
    public void Read_FromOffset_ReadsOnlyFourBytes()
    {
        var buffer = new byte[] { 0xAA, 0xAA, 0x3F, 0x80, 0x00, 0x00, 0xAA, 0xAA };

        var value = FloatBitsCompat.Read(buffer.AsSpan(2), bigEndian: true);

        Assert.Equal(One, value);
    }

    [Fact]
    public void Read_WithSpanLongerThanFourBytes_IgnoresTrailingBytes()
    {
        var buffer = new byte[] { 0x3F, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };

        var value = FloatBitsCompat.Read(buffer, bigEndian: true);

        Assert.Equal(One, value);
    }
}

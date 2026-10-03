using System;
using System.Text;
using Itminus.Tags.S7.Compat;
using Xunit;

namespace Itminus.Tags.Tests.S7Tags.Compat;

/// <summary>
/// <see cref="AsciiTextCompat"/> 的测试：ASCII 文本与字节序列的互转（span 友好）。<br/>
/// <br/>
/// 为什么值得直接测：net472 用 <c>ToArray()</c> / 临时数组绕开缺失的 span 重载，
/// net8.0 直接转发到 <see cref="Encoding.ASCII"/>。两条实现必须在**边界情形**
/// （空串、切片、目标缓冲区不足）下表现一致——否则同一个测点在两个框架上行为不同。
/// </summary>
public class AsciiTextCompatTests
{
    [Fact]
    public void GetString_DecodesAscii()
    {
        var bytes = Encoding.ASCII.GetBytes("ABC");

        var text = AsciiTextCompat.GetString(bytes);

        Assert.Equal("ABC", text);
    }

    [Fact]
    public void GetString_Empty_ReturnsEmpty()
    {
        var text = AsciiTextCompat.GetString(ReadOnlySpan<byte>.Empty);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void GetString_FromOffset_IgnoresSurroundingBytes()
    {
        // 缓存切片场景：前后有噪声字节，只解码中间片段
        var buffer = new byte[] { 0xAA, (byte)'H', (byte)'i', 0xBB };

        var text = AsciiTextCompat.GetString(buffer.AsSpan(1, 2));

        Assert.Equal("Hi", text);
    }

    [Fact]
    public void GetString_NonAsciiBytes_AreReplaced()
    {
        // Encoding.ASCII 的替换字符是 '?'，两个框架必须一致
        var text = AsciiTextCompat.GetString(new byte[] { 0xFF });

        Assert.Equal("?", text);
    }

    [Fact]
    public void GetBytes_EncodesAscii_AndReturnsCount()
    {
        var destination = new byte[8];

        var written = AsciiTextCompat.GetBytes("ABC", destination);

        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0, 0, 0, 0, 0 }, destination);
    }

    [Fact]
    public void GetBytes_Empty_ReturnsZero()
    {
        var destination = new byte[4];

        var written = AsciiTextCompat.GetBytes(string.Empty, destination);

        Assert.Equal(0, written);
    }

    [Fact]
    public void GetBytes_FromOffset_DoesNotTouchPrefix()
    {
        // 组合测点的字符串区前两字节是元数据（maxlen / strlen），编码不能写进去
        var destination = new byte[8];
        destination[0] = 0xEE;
        destination[1] = 0xEE;

        var written = AsciiTextCompat.GetBytes("ABC", destination.AsSpan(2));

        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0x41, 0x42, 0x43, 0, 0, 0 }, destination);
    }

    [Fact]
    public void GetString_ThenGetBytes_RoundTrips()
    {
        var source = new byte[8];
        var written = AsciiTextCompat.GetBytes("Hello", source);

        var text = AsciiTextCompat.GetString(source.AsSpan(0, written));

        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetBytes_WhenDestinationTooSmall_ThrowsArgumentException()
    {
        // 与标准库 Encoding.GetBytes(string, Span<byte>) 一致：塞不下时**抛异常**（不做截断）。
        // 两个框架的实现路径不同，但异常类型一致：
        //   net8.0 → Encoder 抛 "output byte buffer is too small ..."
        //   net472 → Array.CopyTo 抛 "Destination is too short."
        // 因此这里只断言异常类型，**不断言消息**（消息在两个框架下确实不同）。
        var destination = new byte[3];

        Assert.Throws<ArgumentException>(() => AsciiTextCompat.GetBytes("ABCDE", destination));
    }

    [Fact]
    public void GetBytes_WhenDestinationExactlyFits_Succeeds()
    {
        // 边界：正好装得下时必须成功（调用点依赖这一点——它们先用 Maxlen 判长再调用）
        var destination = new byte[5];

        var written = AsciiTextCompat.GetBytes("ABCDE", destination);

        Assert.Equal(5, written);
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 }, destination);
    }
}

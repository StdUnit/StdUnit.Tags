using StdUnit.Tags;
using StdUnit.Tags.ModbusTcp;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

/// <summary>
/// Modbus 地址解析的边界与非法输入：参考号从 1 开始（x0000 没有对应的点）、各区域的参考号上下界、
/// 从站号 0~255、位号 0~15、以及各种"不像地址"的输入——都必须在加载期抛
/// <see cref="TagsProjectAddressException"/> 并给出可定位的原因。
/// </summary>
public class ModbusAddressBoundaryTests
{
    /// <summary>每个区域的参考号下界（x0001）与上界（x9999）都落在 0 起算的协议地址（<c>StartPoint</c>）上</summary>
    [Theory]
    [InlineData("00001", RegisterKinds.OutputCoils, 0)]
    [InlineData("09999", RegisterKinds.OutputCoils, 9998)]
    [InlineData("10001", RegisterKinds.InputContacts, 0)]
    [InlineData("19999", RegisterKinds.InputContacts, 9998)]
    [InlineData("30001", RegisterKinds.InputRegisters, 0)]
    [InlineData("39999", RegisterKinds.InputRegisters, 9998)]
    [InlineData("40001", RegisterKinds.HoldingRegisters, 0)]
    [InlineData("49999", RegisterKinds.HoldingRegisters, 9998)]
    public void Parse_AreaBoundaries(string address, RegisterKinds area, ushort startPoint)
    {
        var addr = ModBusTcpAddressParser.Parse(address);

        Assert.Equal(area, addr.Area);
        Assert.Equal(startPoint, addr.StartPoint);
        Assert.False(addr.UseBit);
        Assert.Equal((byte)1, addr.SlaveAddress);
    }

    /// <summary>
    /// 参考号 0（x0000）没有对应的点：<c>start -= 1</c> 会下溢成 65535，必须拒绝而不是静默回绕。
    /// </summary>
    [Theory]
    [InlineData("00000")]
    [InlineData("10000")]
    [InlineData("30000")]
    [InlineData("40000")]
    [InlineData("1~40000")]
    [InlineData("00000.3")]
    public void Parse_ReferenceNumberZero_Throws(string address)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(address));

        Assert.Contains("参考号必须 >= 1", ex.Message);
        Assert.Contains(address, ex.Message);
    }

    /// <summary>从站号超出 0~255</summary>
    [Theory]
    [InlineData("256~40001")]
    [InlineData("9999~40001")]
    public void Parse_SlaveOutOfRange_Throws(string address)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(address));

        Assert.Contains("0~255", ex.Message);
    }

    /// <summary>位号超出 0~15（含"数值大到装不进 byte"的情形）</summary>
    [Theory]
    [InlineData("40001.16")]
    [InlineData("40001.255")]
    [InlineData("40001.300")]
    public void Parse_NthBitOutOfRange_Throws(string address)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(address));

        Assert.Contains("0~15", ex.Message);
    }

    /// <summary>不像地址的输入一律拒绝，并回显原文</summary>
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4")]
    [InlineData("50001")]
    [InlineData("20001")]
    [InlineData("40001.1.2")]
    [InlineData("-40001")]
    [InlineData("1~")]
    public void Parse_IllegalAddress_Throws(string address)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(address));

        Assert.Contains(address, ex.Message);
    }

    /// <summary>
    /// 形状对、内容非法时必须报告真正的原因（"位号越界"），而不是另一套写法的"未能匹配模式"。
    /// </summary>
    [Fact]
    public void Parse_InvalidNthBit_ReportsRealReason()
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse("1~40001.16"));

        Assert.DoesNotContain("未能匹配模式", ex.Message);
        Assert.Contains("0~15", ex.Message);
    }

    /// <summary>合法地址里的位号仍按 UseBit 表达（0~15 都可用）</summary>
    [Theory]
    [InlineData("40001.0", 0)]
    [InlineData("40001.15", 15)]
    public void Parse_NthBitBoundaries_Succeed(string address, byte nthBit)
    {
        var addr = ModBusTcpAddressParser.Parse(address);

        Assert.True(addr.UseBit);
        Assert.Equal(nthBit, addr.NthBit);
        Assert.Equal((ushort)0, addr.StartPoint);
    }

    /// <summary>地址为 null（不是空串）也要给出加载期异常，而不是裸 ArgumentNullException</summary>
    [Fact]
    public void Parse_NullAddress_ThrowsAddressException()
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(null!));

        Assert.Contains("null", ex.Message);
    }

    /// <summary>参考号写不进 <c>int</c>/<c>ushort</c>（5 位数 > 65535）也要点名报错</summary>
    [Theory]
    [InlineData("065536")]
    [InlineData("065536.3")]
    public void Parse_ReferenceNumberTooLarge_Throws(string address)
    {
        var ex = Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(address));

        Assert.Contains("参考号必须是 1~65535", ex.Message);
    }

    /// <summary>区域值非法（枚举里没有的取值）时，格式化地址要报错而不是给出一个假地址</summary>
    [Fact]
    public void ToString_WithUnknownArea_Throws()
    {
        var addr = new ModbusTcpAddress { Area = (RegisterKinds)9, StartPoint = 0 };

        var ex = Assert.Throws<TagsProjectAddressException>(() => addr.ToString());

        Assert.Contains("Area=9", ex.Message);
    }
}

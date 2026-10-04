using StdUnit.Tags.ModbusTcp;
using System;
using Xunit;

namespace StdUnit.Tags.Tests.ModbusTags;

public class TestModbusAddressParsing
{
    [Theory]
    [InlineData("1~40001.0", 1, RegisterKinds.HoldingRegisters, 0, true, 0)]
    [InlineData("2~40001.1", 2, RegisterKinds.HoldingRegisters, 0, true, 1)]
    [InlineData("3~40011.0", 3, RegisterKinds.HoldingRegisters, 10, true, 0)]
    [InlineData("14~40001", 14, RegisterKinds.HoldingRegisters, 0, false, 0)]
    [InlineData("15~40003", 15, RegisterKinds.HoldingRegisters, 2, false, 0)]

    [InlineData("40001.0", 1, RegisterKinds.HoldingRegisters, 0, true, 0)]
    [InlineData("40001.1", 1, RegisterKinds.HoldingRegisters, 0, true, 1)]
    [InlineData("40011.0", 1, RegisterKinds.HoldingRegisters, 10, true, 0)]
    [InlineData("40001", 1, RegisterKinds.HoldingRegisters, 0, false, 0)]
    [InlineData("40003", 1, RegisterKinds.HoldingRegisters, 2, false, 0)]
    public void TestPattern(string addr, byte slave, RegisterKinds area, ushort startpoint, bool useBit, byte nthBit)
    {

        var mAddr = ModBusTcpAddressParser.Parse(addr);

        // test parsing
        Assert.Equal(slave, mAddr.SlaveAddress);
        Assert.Equal(area, mAddr.Area);
        Assert.Equal(startpoint, mAddr.StartPoint);
        Assert.Equal(useBit, mAddr.UseBit);
        Assert.Equal(nthBit, mAddr.NthBit);

        // test ToString()
        Assert.EndsWith(addr, mAddr.ToString());
    }



    [Theory]
    [InlineData("50001.0")]
    [InlineData("20001.0")]
    [InlineData("60001.0")]
    public void TestPattern_WrongArea(string addr)
    {
        Assert.Throws<Exception>(() => ModBusTcpAddressParser.Parse(addr));
    }
}
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
    [InlineData(1, RegisterKinds.OutputCoils, 0, false, 0, "1~00001")]
    [InlineData(1, RegisterKinds.OutputCoils, 16, false, 0, "1~00017")]
    [InlineData(2, RegisterKinds.OutputCoils, 9998, false, 0, "2~09999")]
    [InlineData(1, RegisterKinds.InputContacts, 0, false, 0, "1~10001")]
    [InlineData(3, RegisterKinds.InputRegisters, 10, false, 0, "3~30011")]
    [InlineData(4, RegisterKinds.HoldingRegisters, 10, true, 3, "4~40011.3")]
    public void TestToString_RoundTrip(byte slave, RegisterKinds area, ushort startpoint, bool useBit, byte nthBit, string expected)
    {
        var addr = new ModbusTcpAddress
        {
            SlaveAddress = slave,
            Area = area,
            StartPoint = startpoint,
            UseBit = useBit,
            NthBit = nthBit,
        };

        Assert.Equal(expected, addr.ToString());

        var parsed = ModBusTcpAddressParser.Parse(addr.ToString());
        Assert.Equal(addr.SlaveAddress, parsed.SlaveAddress);
        Assert.Equal(addr.Area, parsed.Area);
        Assert.Equal(addr.StartPoint, parsed.StartPoint);
        Assert.Equal(addr.UseBit, parsed.UseBit);
        Assert.Equal(addr.NthBit, parsed.NthBit);
    }

    [Theory]
    [InlineData("50001.0")]
    [InlineData("20001.0")]
    [InlineData("60001.0")]
    public void TestPattern_WrongArea(string addr)
    {
        Assert.Throws<TagsProjectAddressException>(() => ModBusTcpAddressParser.Parse(addr));
    }
}
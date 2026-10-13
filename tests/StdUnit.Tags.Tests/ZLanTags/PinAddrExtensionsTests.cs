using StdUnit.Tags.ZLan;
using Xunit;

namespace StdUnit.Tags.Tests.ZLanTags;

public class PinAddrExtensionsTests
{
    [Theory]
    [InlineData(DIPinAddr.DI1, (byte)1, "1~10001")]
    [InlineData(DIPinAddr.DI2, (byte)3, "3~10002")]
    public void ToModbusTcpAddr_DI_ShouldIncludeSlavePrefixAndAddress(DIPinAddr pin, byte slave, string expected)
    {
        var normalized = pin.ToModbusTcpAddr(slave);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(DOPinAddr.DO1, (byte)1, "1~00017")]
    [InlineData(DOPinAddr.DO4, (byte)2, "2~00020")]
    public void ToModbusTcpAddr_DO_ShouldIncludeSlavePrefixAndAddress(DOPinAddr pin, byte slave, string expected)
    {
        var normalized = pin.ToModbusTcpAddr(slave);
        Assert.Equal(expected, normalized);
    }
}

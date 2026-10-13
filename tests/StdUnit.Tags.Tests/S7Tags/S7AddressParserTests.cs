using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7AddressParserTests
{
    [Fact]
    public void ParseRelativeAddress_WithoutBit_ShouldKeepRelativeMetadata()
    {
        var addr = S7AddressParser.Parse("$$104");

        Assert.Equal(AreaKinds.None, addr.Area);
        Assert.False(addr.BlockSpecified);
        Assert.Equal(0, addr.BlockNumber);
        Assert.Equal(104, addr.StartAddress);
        Assert.False(addr.UseBit);
        Assert.Equal(0, addr.NthBit);
        Assert.Equal("$$104", addr.Format());
    }

    [Fact]
    public void ParseRelativeAddress_WithBit_ShouldParseSuccessfully()
    {
        var addr = S7AddressParser.Parse("$$104.3");

        Assert.Equal(AreaKinds.None, addr.Area);
        Assert.False(addr.BlockSpecified);
        Assert.Equal(0, addr.BlockNumber);
        Assert.Equal(104, addr.StartAddress);
        Assert.True(addr.UseBit);
        Assert.Equal(3, addr.NthBit);
        Assert.Equal("$$104.3", addr.Format());
    }
}

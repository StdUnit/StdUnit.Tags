using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7PlcItemTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var item = new S7PlcItem();
        Assert.Equal("127.0.0.1", item.IpAddr);
        Assert.Equal(0, item.Rack);
        Assert.Equal(1, item.Slot);
        Assert.Equal((ushort)3, item.ConnectionType);
    }

    [Fact]
    public void SetProperties_ShouldPersist()
    {
        var item = new S7PlcItem
        {
            IpAddr = "10.0.0.1",
            Rack = 2,
            Slot = 3,
            ConnectionType = 4
        };
        Assert.Equal("10.0.0.1", item.IpAddr);
        Assert.Equal(2, item.Rack);
        Assert.Equal(3, item.Slot);
        Assert.Equal((ushort)4, item.ConnectionType);
    }
}

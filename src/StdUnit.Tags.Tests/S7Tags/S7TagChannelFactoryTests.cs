using StdUnit.Tags.S7;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7TagChannelFactoryTests
{
    [Fact]
    public void Create_ShouldMapConnectionType()
    {
        var factory = new S7TagChannelFactory(new LoggerFactory());
        var descriptor = new TagChannelDescriptor
        {
            Driver = S7Names.DriverName,
            Name = "S7-test",
            Extras = new Dictionary<string, XElement>
            {
                [nameof(S7TagChannelDescriptor.ConnectionType)] = new XElement(nameof(S7TagChannelDescriptor.ConnectionType), "2")
            }
        };
        var channel = (S7TagChannel)factory.Create(descriptor);
        Assert.Equal((ushort)2, channel.PlcItem.ConnectionType);
    }

    [Fact]
    public void Create_ShouldMapAllProperties()
    {
        var factory = new S7TagChannelFactory(new LoggerFactory());
        var descriptor = new TagChannelDescriptor
        {
            Driver = S7Names.DriverName,
            Name = "S7-full",
            Extras = new Dictionary<string, XElement>
            {
                [nameof(S7TagChannelDescriptor.IpAddr)] = new XElement(nameof(S7TagChannelDescriptor.IpAddr), "10.0.0.5"),
                [nameof(S7TagChannelDescriptor.Rack)] = new XElement(nameof(S7TagChannelDescriptor.Rack), "2"),
                [nameof(S7TagChannelDescriptor.Slot)] = new XElement(nameof(S7TagChannelDescriptor.Slot), "3"),
                [nameof(S7TagChannelDescriptor.ConnectionType)] = new XElement(nameof(S7TagChannelDescriptor.ConnectionType), "4"),
            }
        };
        var channel = (S7TagChannel)factory.Create(descriptor);
        Assert.Equal("10.0.0.5", channel.PlcItem.IpAddr);
        Assert.Equal(2, channel.PlcItem.Rack);
        Assert.Equal(3, channel.PlcItem.Slot);
        Assert.Equal((ushort)4, channel.PlcItem.ConnectionType);
    }

    [Fact]
    public void Create_WithEmptyExtras_ShouldUseDefaults()
    {
        var factory = new S7TagChannelFactory(new LoggerFactory());
        var descriptor = new TagChannelDescriptor
        {
            Driver = S7Names.DriverName,
            Name = "S7-defaults",
            Extras = new Dictionary<string, XElement>()
        };
        var channel = (S7TagChannel)factory.Create(descriptor);
        Assert.Equal("localhost", channel.PlcItem.IpAddr);
        Assert.Equal(0, channel.PlcItem.Rack);
        Assert.Equal(1, channel.PlcItem.Slot);
        Assert.Equal((ushort)3, channel.PlcItem.ConnectionType);
    }
}

using StdUnit.Tags.S7;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

public class S7TagChannelDescriptorTests
{
    #region ToS7TagChannelDescriptor (From Base TagChannelDescriptor via Extras)

    [Fact]
    public void ToS7TagChannelDescriptor_FromBaseType_ReadsExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "s7-extras",
            Driver = S7Names.DriverName,
        };
        baseDesc.Extras["IpAddr"] = new XElement("IpAddr", "10.0.0.1");
        baseDesc.Extras["Rack"] = new XElement("Rack", "2");
        baseDesc.Extras["Slot"] = new XElement("Slot", "3");
        baseDesc.Extras["ConnectionType"] = new XElement("ConnectionType", "2");

        var result = baseDesc.ToS7TagChannelDescriptor();

        Assert.Equal("10.0.0.1", result.IpAddr);
        Assert.Equal(2, result.Rack);
        Assert.Equal(3, result.Slot);
        Assert.Equal(2, result.ConnectionType);
    }

    [Fact]
    public void ToS7TagChannelDescriptor_FromBaseType_DefaultValues()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "s7-defaults",
            Driver = S7Names.DriverName,
        };

        var result = baseDesc.ToS7TagChannelDescriptor();

        Assert.Equal("localhost", result.IpAddr);
        Assert.Equal(0, result.Rack);
        Assert.Equal(1, result.Slot);
        Assert.Equal(3, result.ConnectionType);
    }

    [Fact]
    public void ToS7TagChannelDescriptor_WhenAlreadyS7Descriptor_ReturnsSame()
    {
        var descriptor = new S7TagChannelDescriptor
        {
            Name = "s7-already",
            IpAddr = "10.0.0.1",
            Rack = 0,
            Slot = 1,
        };

        var result = descriptor.ToS7TagChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToS7TagChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "ModbusTcp",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => descriptor.ToS7TagChannelDescriptor());
        Assert.Contains(S7Names.DriverName, ex.Message);
    }

    [Fact]
    public void ToS7TagChannelDescriptor_InvalidRack_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "s7-bad-rack",
            Driver = S7Names.DriverName,
        };
        baseDesc.Extras["Rack"] = new XElement("Rack", "not-a-number");

        Assert.Throws<ArgumentException>(() => baseDesc.ToS7TagChannelDescriptor());
    }

    [Fact]
    public void ToS7TagChannelDescriptor_InvalidSlot_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "s7-bad-slot",
            Driver = S7Names.DriverName,
        };
        baseDesc.Extras["Slot"] = new XElement("Slot", "not-a-number");

        Assert.Throws<ArgumentException>(() => baseDesc.ToS7TagChannelDescriptor());
    }

    [Fact]
    public void ToS7TagChannelDescriptor_PreservesExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "s7-extra-keys",
            Driver = S7Names.DriverName,
        };
        baseDesc.Extras["CustomKey"] = new XElement("CustomKey", "CustomValue");

        var result = baseDesc.ToS7TagChannelDescriptor();

        Assert.True(result.Extras.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", result.Extras["CustomKey"].Value);
    }

    #endregion
}

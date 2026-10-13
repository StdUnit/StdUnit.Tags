using StdUnit.Tags.Hjzk;
using StdUnit.Tags.ModbusTcp;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.HjzkTags;

public class HjzkTagChannelDescriptorTests
{
    #region ToXElement / XML Roundtrip

    [Fact]
    public void ToXElement_Roundtrips()
    {
        var descriptor = new HjzkTagChannelDescriptor
        {
            Name = "hjzk-1",
            IpAddr = "192.168.1.100",
            Port = 503,
        };

        var xml = descriptor.ToXElement();
        var baseDesc = xml.ToTagChannelDescriptor();
        var restored = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.Equal("hjzk-1", restored.Name);
        Assert.Equal(HjzkNames.DriverName, restored.Driver);
        Assert.Equal("192.168.1.100", restored.IpAddr);
        Assert.Equal(503, restored.Port);
    }

    [Fact]
    public void ToXElement_WithMaxWriteRegisters_Roundtrips()
    {
        var descriptor = new HjzkTagChannelDescriptor
        {
            Name = "hjzk-batch",
            IpAddr = "10.0.0.1",
            Port = 502,
            MaxWriteRegisters = 20,
        };

        var xml = descriptor.ToXElement();
        var baseDesc = xml.ToTagChannelDescriptor();
        var restored = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.Equal((ushort)20, restored.MaxWriteRegisters);
    }

    [Fact]
    public void ToXElement_ContainsExpectedElements()
    {
        var descriptor = new HjzkTagChannelDescriptor
        {
            Name = "hjzk-xml",
            IpAddr = "10.0.0.5",
            Port = 502,
        };

        var xml = descriptor.ToXElement().ToString();

        Assert.Contains("IpAddr", xml);
        Assert.Contains("10.0.0.5", xml);
        Assert.Contains("Port", xml);
    }

    #endregion

    #region ToHjzkTagChannelDescriptor (From Base TagChannelDescriptor via Extras)

    [Fact]
    public void ToHjzkTagChannelDescriptor_FromBaseType_ReadsExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-extras",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["IpAddr"] = new XElement("IpAddr", "10.0.0.1");
        baseDesc.Extras["Port"] = new XElement("Port", "502");

        var result = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.Equal("10.0.0.1", result.IpAddr);
        Assert.Equal(502, result.Port);
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_FromBaseType_DefaultValues()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-defaults",
            Driver = HjzkNames.DriverName,
        };

        var result = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.Equal("localhost", result.IpAddr);
        Assert.Equal(502, result.Port);
        Assert.Null(result.MaxWriteRegisters);
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_WhenAlreadyHjzkDescriptor_ReturnsSame()
    {
        var descriptor = new HjzkTagChannelDescriptor
        {
            Name = "hjzk-already",
            IpAddr = "10.0.0.1",
            Port = 502,
        };

        var result = descriptor.ToHjzkTagChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "S7",
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => descriptor.ToHjzkTagChannelDescriptor());
        Assert.Contains(HjzkNames.DriverName, ex.Message);
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_InvalidPort_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-bad-port",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["Port"] = new XElement("Port", "not-a-number");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToHjzkTagChannelDescriptor());
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_InvalidMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-bad-batch",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "not-a-number");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToHjzkTagChannelDescriptor());
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_ZeroMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-zero-batch",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "0");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToHjzkTagChannelDescriptor());
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_ExceedsMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-big-batch",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "124");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToHjzkTagChannelDescriptor());
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_MaxWriteRegisters_ReadsFromExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-batch-extras",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "100");

        var result = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.Equal((ushort)100, result.MaxWriteRegisters);
    }

    [Fact]
    public void ToHjzkTagChannelDescriptor_PreservesExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "hjzk-extra-keys",
            Driver = HjzkNames.DriverName,
        };
        baseDesc.Extras["CustomKey"] = new XElement("CustomKey", "CustomValue");

        var result = baseDesc.ToHjzkTagChannelDescriptor();

        Assert.True(result.Extras.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", result.Extras["CustomKey"].Value);
    }

    #endregion
}

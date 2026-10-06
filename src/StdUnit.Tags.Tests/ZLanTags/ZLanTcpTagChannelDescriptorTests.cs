using StdUnit.Tags.ZLan;
using StdUnit.Tags.ModbusTcp;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ZLanTags;

public class ZLanTcpTagChannelDescriptorTests
{
    #region ToXElement / XML Roundtrip

    [Fact]
    public void ToXElement_Roundtrips()
    {
        var descriptor = new ZLanTcpTagChannelDescriptor
        {
            Name = "zlan-1",
            IpAddr = "192.168.1.200",
            Port = 503,
        };

        var xml = descriptor.ToXElement();
        var baseDesc = xml.ToTagChannelDescriptor();
        var restored = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.Equal("zlan-1", restored.Name);
        Assert.Equal(ZLanTcpNames.DriverName, restored.Driver);
        Assert.Equal("192.168.1.200", restored.IpAddr);
        Assert.Equal(503, restored.Port);
    }

    [Fact]
    public void ToXElement_WithMaxWriteRegisters_Roundtrips()
    {
        var descriptor = new ZLanTcpTagChannelDescriptor
        {
            Name = "zlan-batch",
            IpAddr = "10.0.0.1",
            Port = 502,
            MaxWriteRegisters = 30,
        };

        var xml = descriptor.ToXElement();
        var baseDesc = xml.ToTagChannelDescriptor();
        var restored = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.Equal((ushort)30, restored.MaxWriteRegisters);
    }

    [Fact]
    public void ToXElement_ContainsExpectedElements()
    {
        var descriptor = new ZLanTcpTagChannelDescriptor
        {
            Name = "zlan-xml",
            IpAddr = "10.0.0.5",
            Port = 502,
        };

        var xml = descriptor.ToXElement().ToString();

        Assert.Contains("IpAddr", xml);
        Assert.Contains("10.0.0.5", xml);
        Assert.Contains("Port", xml);
    }

    #endregion

    #region ToZLanTcpTagChannelDescriptor (From Base TagChannelDescriptor via Extras)

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_FromBaseType_ReadsExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-extras",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["IpAddr"] = new XElement("IpAddr", "10.0.0.1");
        baseDesc.Extras["Port"] = new XElement("Port", "502");

        var result = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.Equal("10.0.0.1", result.IpAddr);
        Assert.Equal(502, result.Port);
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_FromBaseType_DefaultValues()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-defaults",
            Driver = ZLanTcpNames.DriverName,
        };

        var result = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.Equal("localhost", result.IpAddr);
        Assert.Equal(502, result.Port);
        Assert.Null(result.MaxWriteRegisters);
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_WhenAlreadyZLanDescriptor_ReturnsSame()
    {
        var descriptor = new ZLanTcpTagChannelDescriptor
        {
            Name = "zlan-already",
            IpAddr = "10.0.0.1",
            Port = 502,
        };

        var result = descriptor.ToZLanTcpTagChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "S7",
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => descriptor.ToZLanTcpTagChannelDescriptor());
        Assert.Contains(ZLanTcpNames.DriverName, ex.Message);
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_InvalidPort_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-bad-port",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["Port"] = new XElement("Port", "not-a-number");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToZLanTcpTagChannelDescriptor());
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_InvalidMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-bad-batch",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "not-a-number");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToZLanTcpTagChannelDescriptor());
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_ZeroMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-zero-batch",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "0");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToZLanTcpTagChannelDescriptor());
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_ExceedsMaxWriteRegisters_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-big-batch",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "124");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToZLanTcpTagChannelDescriptor());
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_MaxWriteRegisters_ReadsFromExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-batch-extras",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["MaxWriteRegisters"] = new XElement("MaxWriteRegisters", "50");

        var result = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.Equal((ushort)50, result.MaxWriteRegisters);
    }

    [Fact]
    public void ToZLanTcpTagChannelDescriptor_PreservesExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "zlan-extra-keys",
            Driver = ZLanTcpNames.DriverName,
        };
        baseDesc.Extras["CustomKey"] = new XElement("CustomKey", "CustomValue");

        var result = baseDesc.ToZLanTcpTagChannelDescriptor();

        Assert.True(result.Extras.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", result.Extras["CustomKey"].Value);
    }

    #endregion
}

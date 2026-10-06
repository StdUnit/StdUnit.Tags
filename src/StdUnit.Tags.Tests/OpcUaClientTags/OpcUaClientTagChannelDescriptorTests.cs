using StdUnit.Tags.OpcUaClient;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

public class OpcUaClientTagChannelDescriptorTests
{
    #region ToOpcUaClientTagChannelDescriptor (From Base TagChannelDescriptor via Extras)

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_FromBaseType_ReadsExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-extras",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["ClientName"] = new XElement("ClientName", "my-client");
        var srv = new XElement("ServerOpt",
            new XElement("DiscoveryUrl", "opc.tcp://10.0.0.1:4840"),
            new XElement("UsePassword", "true"),
            new XElement("UserName", "admin"),
            new XElement("Password", "pwd123")
        );
        baseDesc.Extras["ServerOpt"] = srv;

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.Equal("my-client", result.OpcUaTagChannelOpt.ClientName);
        Assert.Equal("opc.tcp://10.0.0.1:4840", result.OpcUaTagChannelOpt.ServerOpt.DiscoveryUrl);
        Assert.True(result.OpcUaTagChannelOpt.ServerOpt.UsePassword);
        Assert.Equal("admin", result.OpcUaTagChannelOpt.ServerOpt.UserName);
        Assert.Equal("pwd123", result.OpcUaTagChannelOpt.ServerOpt.Password);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_FromBaseType_DefaultValues()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-defaults",
            Driver = OpcUaClientNames.DriverName,
        };

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.Equal("StdUnitTagsOpcUaClient", result.OpcUaTagChannelOpt.ClientName);
        Assert.NotNull(result.OpcUaTagChannelOpt.ServerOpt);
        Assert.Empty(result.OpcUaTagChannelOpt.ServerOpt.DiscoveryUrl);
        Assert.True(result.OpcUaTagChannelOpt.ServerOpt.UsePassword);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenAlreadyOpcUaDescriptor_ReturnsSame()
    {
        var descriptor = new OpcUaClientTagChannelDescriptor
        {
            Name = "opcua-already",
            OpcUaTagChannelOpt = new OpcUaClientTagChannelOpt
            {
                ClientName = "my-client",
                ServerOpt = new OpcUaServerOpt { DiscoveryUrl = "opc.tcp://localhost:4840" },
            },
        };

        var result = descriptor.ToOpcUaClientTagChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "ModbusTcp",
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => descriptor.ToOpcUaClientTagChannelDescriptor());
        Assert.Contains(OpcUaClientNames.DriverName, ex.Message);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_PreservesExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-extra-keys",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["CustomKey"] = new XElement("CustomKey", "CustomValue");

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.True(result.Extras.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", result.Extras["CustomKey"].Value);
    }

    #endregion
}

using StdUnit.Tags.ModbusTcp;
using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.S7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Channels;

public class ChannelDescriptorTests
{
    [Fact]
    public void TestLoadFromXml()
    {
        var xml =
@"
<Channel name='S7-3' driver='S7' >
    <IpAddr>192.168.1.13</IpAddr>
    <Rack>3</Rack>
    <Slot>4</Slot>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor = element.ToTagChannelDescriptor();
        Assert.Equal("S7-3", descriptor.Name);
        Assert.Equal("S7", descriptor.Driver);
        Assert.NotEmpty(descriptor.Extras);
        Assert.Equal(3, descriptor.Extras.Count);
        Assert.Equal("192.168.1.13", descriptor.Extras["IpAddr"].Value);
        Assert.Equal("3", descriptor.Extras["Rack"].Value);
        Assert.Equal("4", descriptor.Extras["Slot"].Value);

        var element2 = descriptor.ToXElement();
        Assert.Equal(element.ToString(), element2.ToString());
    }


    [Fact]
    public void TestLoadS7TagChannelDescriptorFromXml()
    {
        var xml =
@"
<Channel name='S7-3' driver='S7' >
    <IpAddr>192.168.1.13</IpAddr>
    <Rack>3</Rack>
    <Slot>4</Slot>
    <ConnectionType>2</ConnectionType>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        var descriptor1 = descriptor0.ToS7TagChannelDescriptor();
        Assert.Equal("192.168.1.13", descriptor1.IpAddr);
        Assert.Equal(3, descriptor1.Rack);
        Assert.Equal(4, descriptor1.Slot);
        Assert.Equal(2, descriptor1.ConnectionType);

        var element2 = descriptor1.ToXElement();
        Assert.Equal(element.ToString(), element2.ToString());
    }

    [Fact]
    public void TestLoadS7TagChannelDescriptorFromXml_EmptyConnectionType()
    {
        var xml =
@"
<Channel name='S7-3' driver='S7' >
    <IpAddr>192.168.1.13</IpAddr>
    <Rack>3</Rack>
    <Slot>4</Slot>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        var descriptor1 = descriptor0.ToS7TagChannelDescriptor();
        Assert.Equal("192.168.1.13", descriptor1.IpAddr);
        Assert.Equal(3, descriptor1.Rack);
        Assert.Equal(4, descriptor1.Slot);
        Assert.Equal(3, descriptor1.ConnectionType);
    }


    [Fact]
    public void TestLoadS7TagChannelDescriptorFromXml_InvalidConnectionType_ShouldThrow()
    {
        var xml =
@"
<Channel name='S7-3' driver='S7' >
    <IpAddr>192.168.1.13</IpAddr>
    <Rack>3</Rack>
    <Slot>4</Slot>
    <ConnectionType>invalid</ConnectionType>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        Assert.Throws<ArgumentException>(() => descriptor0.ToS7TagChannelDescriptor());
    }

    [Fact]
    public void TestLoadModbusTcpTagChannelDescriptorFromXml()
    {
        var xml =
@"
<Channel name='ModbusTcp-1' driver='ModbusTcp' >
    <IpAddr>192.168.1.13</IpAddr>
    <Port>503</Port>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        var descriptor1 = descriptor0.ToModbusTcpTagChannelDescriptor();
        Assert.Equal("192.168.1.13", descriptor1.IpAddr);
        Assert.Equal(503, descriptor1.Port);

        var element2 = descriptor1.ToXElement();
        Assert.Equal(element.ToString(), element2.ToString());
    }


    [Fact]
    public void TestLoadModbusTcpTagChannelDescriptorFromXml_WithDefaultPort()
    {
        var xml =
@"
<Channel name='ModbusTcp-1' driver='ModbusTcp' >
    <IpAddr>192.168.1.13</IpAddr>
    <Port>503</Port>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        var descriptor1 = descriptor0.ToModbusTcpTagChannelDescriptor();
        Assert.Equal("192.168.1.13", descriptor1.IpAddr);
        Assert.Equal(503, descriptor1.Port);

        var element2 = descriptor1.ToXElement();
        var xml1 = element.ToString();
        var xml2 = element2.ToString();
        Assert.Equal(xml1, xml2);
    }


    [Fact]
    public void TestLoadOpcUaClientTagChannelDescriptorFromXml()
    {
        var xml =
@"
<Channel name='OpcUaClient-2' driver='OpcUaClient'>
	<ClientName>client-name-1</ClientName>
	<ServerOpt>
		<DiscoveryUrl>opc.tcp://DESKTOP-FM4T292:4840</DiscoveryUrl>
		<UsePassword>false</UsePassword>
		<UserName>user</UserName>
		<Password>1</Password>
	</ServerOpt>
</Channel>
";
        var element = XElement.Parse(xml);
        var descriptor0 = element.ToTagChannelDescriptor();
        var descriptor1 = descriptor0.ToOpcUaClientTagChannelDescriptor();
        Assert.Equal(OpcUaClientNames.DriverName, descriptor1.Driver);
        Assert.Equal("OpcUaClient-2", descriptor1.Name);
        Assert.Equal("client-name-1", descriptor1.OpcUaTagChannelOpt.ClientName);
        var serverOpt = descriptor1.OpcUaTagChannelOpt.ServerOpt;
        Assert.Equal("opc.tcp://DESKTOP-FM4T292:4840", serverOpt.DiscoveryUrl);
        Assert.False(serverOpt.UsePassword);
        Assert.Equal("user", serverOpt.UserName);
        Assert.Equal("1", serverOpt.Password);


        var element2 = descriptor1.ToXElement();
        var xml1 = element.ToString();
        var xml2 = element2.ToString();
        Assert.Equal(xml1, xml2);
    }
}

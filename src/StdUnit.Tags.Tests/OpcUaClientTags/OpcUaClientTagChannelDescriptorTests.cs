using StdUnit.Tags.OpcUaClient;
using System;
using System.Linq;
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
    public void ToOpcUaClientTagChannelDescriptor_ReadsSecurityOptFromExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-security",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["SecurityOpt"] = new XElement("SecurityOpt",
            new XElement("AutoAcceptUntrustedCertificates", "false"),
            new XElement("RejectSHA1SignedCertificates", "true"),
            new XElement("MinimumCertificateKeySize", "2048"));

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        var securityOpt = result.OpcUaTagChannelOpt.SecurityOpt;
        Assert.False(securityOpt.AutoAcceptUntrustedCertificates);
        Assert.True(securityOpt.RejectSHA1SignedCertificates);
        Assert.Equal((ushort)2048, securityOpt.MinimumCertificateKeySize);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenSecurityOptMissing_UsesLooseDefaults()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-security-defaults",
            Driver = OpcUaClientNames.DriverName,
        };

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        var securityOpt = result.OpcUaTagChannelOpt.SecurityOpt;
        Assert.True(securityOpt.AutoAcceptUntrustedCertificates);
        Assert.False(securityOpt.RejectSHA1SignedCertificates);
        Assert.Equal((ushort)1024, securityOpt.MinimumCertificateKeySize);
        // 未配置证书库根目录 ⇒ 走内置默认根（CommonApplicationData 下），而不是空串
        Assert.Null(securityOpt.CertificateStoreRoot);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_ReadsCertificateStoreRoot()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-store-root",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["SecurityOpt"] = new XElement("SecurityOpt",
            new XElement("CertificateStoreRoot", "/home/you/.local/share/OPC Foundation/CertificateStores"));

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.Equal(
            "/home/you/.local/share/OPC Foundation/CertificateStores",
            result.OpcUaTagChannelOpt.SecurityOpt.CertificateStoreRoot);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenCertificateStoreRootIsBlank_TreatedAsUnset()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-store-root-blank",
            Driver = OpcUaClientNames.DriverName,
        };
        // XML 里“占位但留空”很常见，不该让项目加载失败
        baseDesc.Extras["SecurityOpt"] = new XElement("SecurityOpt",
            new XElement("CertificateStoreRoot", "   "));

        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.Null(result.OpcUaTagChannelOpt.SecurityOpt.CertificateStoreRoot);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenSecurityOptBooleanIsInvalid_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-security-bad",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["SecurityOpt"] = new XElement("SecurityOpt",
            new XElement("AutoAcceptUntrustedCertificates", "abc"));

        var ex = Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToOpcUaClientTagChannelDescriptor());

        Assert.Contains("AutoAcceptUntrustedCertificates", ex.Message);
        Assert.Contains("abc", ex.Message);
        Assert.Equal("Channel(opcua-security-bad)", ex.Location);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenMinimumKeySizeIsInvalid_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-security-bad-key",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["SecurityOpt"] = new XElement("SecurityOpt",
            new XElement("MinimumCertificateKeySize", "0"));

        var ex = Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToOpcUaClientTagChannelDescriptor());

        Assert.Contains("MinimumCertificateKeySize", ex.Message);
    }

    [Fact]
    public void ToOpcUaClientTagChannelDescriptor_WhenUsePasswordIsInvalid_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-bad-password-flag",
            Driver = OpcUaClientNames.DriverName,
        };
        baseDesc.Extras["ServerOpt"] = new XElement("ServerOpt",
            new XElement("DiscoveryUrl", "opc.tcp://localhost:4840"),
            new XElement("UsePassword", "maybe"));

        // 以前静默降级成 false（把"配错了"伪装成"配对了"）
        var ex = Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToOpcUaClientTagChannelDescriptor());

        Assert.Contains("UsePassword", ex.Message);
        Assert.Contains("maybe", ex.Message);
    }

    [Fact]
    public void ToXElement_RoundTripsServerOptAndSecurityOpt()
    {
        var descriptor = new OpcUaClientTagChannelDescriptor
        {
            Name = "opcua-roundtrip",
            OpcUaTagChannelOpt = new OpcUaClientTagChannelOpt
            {
                ClientName = "client-1",
                ServerOpt = new OpcUaServerOpt { DiscoveryUrl = "opc.tcp://10.0.0.7:4840", UserName = "u" },
                SecurityOpt = new OpcUaSecurityOpt
                {
                    AutoAcceptUntrustedCertificates = false,
                    RejectSHA1SignedCertificates = true,
                    MinimumCertificateKeySize = 2048,
                    CertificateStoreRoot = "/home/you/.local/share/OPC Foundation/CertificateStores",
                },
            },
        };

        var ele = descriptor.ToXElement();

        // 模拟加载器：子元素进 Extras，再反向解析
        var baseDesc = new TagChannelDescriptor
        {
            Name = "opcua-roundtrip",
            Driver = OpcUaClientNames.DriverName,
            Extras = ele.Elements().ToDictionary(child => child.Name.LocalName, child => child),
        };
        var result = baseDesc.ToOpcUaClientTagChannelDescriptor();

        Assert.Equal("client-1", result.OpcUaTagChannelOpt.ClientName);
        Assert.Equal("opc.tcp://10.0.0.7:4840", result.OpcUaTagChannelOpt.ServerOpt.DiscoveryUrl);
        Assert.Equal("u", result.OpcUaTagChannelOpt.ServerOpt.UserName);
        Assert.False(result.OpcUaTagChannelOpt.SecurityOpt.AutoAcceptUntrustedCertificates);
        Assert.True(result.OpcUaTagChannelOpt.SecurityOpt.RejectSHA1SignedCertificates);
        Assert.Equal((ushort)2048, result.OpcUaTagChannelOpt.SecurityOpt.MinimumCertificateKeySize);
        Assert.Equal("/home/you/.local/share/OPC Foundation/CertificateStores", result.OpcUaTagChannelOpt.SecurityOpt.CertificateStoreRoot);
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

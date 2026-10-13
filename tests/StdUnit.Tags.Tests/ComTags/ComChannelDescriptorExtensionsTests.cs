using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.ComTags;

public class ComChannelDescriptorExtensionsTests
{
    #region ToComChannelDescriptor (From Base TagChannelDescriptor via Extras)

    [Fact]
    public void ToComChannelDescriptor_FromBaseType_DefaultValues()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "com-defaults",
            Driver = ComDriverNames.DriverName,
        };

        var result = baseDesc.ToComChannelDescriptor();

        Assert.Equal("COM1", result.Option.Port);
        Assert.Equal(9600, result.Option.BaudRate);
        Assert.Equal(System.IO.Ports.Parity.None, result.Option.Parity);
        Assert.Equal(8, result.Option.DataBits);
        Assert.Equal(System.IO.Ports.StopBits.None, result.Option.StopBits);
        Assert.Equal(1, result.Option.ChannelCapacity);
        Assert.True(result.Option.ReadEntireLine);
        Assert.Null(result.Option.ReadScript);
        Assert.False(result.Option.ReadScriptDebugInformationEnabled);
    }

    [Fact]
    public void ToComChannelDescriptor_WhenAlreadyComDescriptor_ReturnsSame()
    {
        var descriptor = new ComChannelDescriptor
        {
            Name = "com-already",
            Driver = ComDriverNames.DriverName,
            Option = new ComChannelOption
            {
                Port = "COM10",
                BaudRate = 115200,
            },
        };

        var result = descriptor.ToComChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToComChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "S7",
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => descriptor.ToComChannelDescriptor());
        Assert.Contains(ComDriverNames.DriverName, ex.Message);
    }

    [Fact]
    public void ToComChannelDescriptor_InvalidBaudRate_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "com-bad-baud",
            Driver = ComDriverNames.DriverName,
        };
        baseDesc.Extras["BaudRate"] = new XElement("BaudRate", "not-a-number");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToComChannelDescriptor());
    }

    [Fact]
    public void ToComChannelDescriptor_InvalidParity_Throws()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "com-bad-parity",
            Driver = ComDriverNames.DriverName,
        };
        baseDesc.Extras["Parity"] = new XElement("Parity", "InvalidParity");

        Assert.Throws<TagsProjectXmlException>(() => baseDesc.ToComChannelDescriptor());
    }

    [Fact]
    public void ToComChannelDescriptor_PreservesExtras()
    {
        var baseDesc = new TagChannelDescriptor
        {
            Name = "com-extra-keys",
            Driver = ComDriverNames.DriverName,
        };
        baseDesc.Extras["CustomKey"] = new XElement("CustomKey", "CustomValue");

        var result = baseDesc.ToComChannelDescriptor();

        Assert.True(result.Extras.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", result.Extras["CustomKey"].Value);
    }

    #endregion
}

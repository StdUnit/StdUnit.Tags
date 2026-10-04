using System;
using System.Xml.Linq;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

public class SimpleFilesTagChannelFactoryTests
{


    #region SimpleFilesTagChannelFactory

    [Fact]
    public void Factory_GetAvailableDrivers_ReturnsSimpleFiles()
    {
        var factory = new SimpleFilesTagChannelFactory(NullLoggerFactory.Instance);

        var drivers = factory.GetAvailableDrivers();

        Assert.Contains(SimpleFilesNames.DriverName, drivers);
    }

    [Fact]
    public void Factory_Create_ReturnsSimpleFilesChannel()
    {
        var factory = new SimpleFilesTagChannelFactory(NullLoggerFactory.Instance);
        var descriptor = new TagChannelDescriptor
        {
            Name = "factory-ch",
            Driver = SimpleFilesNames.DriverName,
        };

        var channel = factory.Create(descriptor);

        var simpleFilesChannel = Assert.IsType<SimpleFilesTagChannel>(channel);
        Assert.Equal("factory-ch", simpleFilesChannel.ChannelName());
        Assert.Null(simpleFilesChannel.Settings.BaseDir);
    }

    [Fact]
    public void Factory_Create_WithBaseDir_SetsSettings()
    {
        var factory = new SimpleFilesTagChannelFactory(NullLoggerFactory.Instance);
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "factory-ch-dir",
            Driver = SimpleFilesNames.DriverName,
            BaseDir = @"C:\tags",
        };

        var channel = factory.Create(descriptor);

        var simpleFilesChannel = Assert.IsType<SimpleFilesTagChannel>(channel);
        Assert.Equal(@"C:\tags", simpleFilesChannel.Settings.BaseDir);
    }

    [Fact]
    public void Factory_Create_WithExtrasBaseDir_SetsSettings()
    {
        var factory = new SimpleFilesTagChannelFactory(NullLoggerFactory.Instance);
        var descriptor = new TagChannelDescriptor
        {
            Name = "factory-ch-extras",
            Driver = SimpleFilesNames.DriverName,
        };
        descriptor.Extras["BaseDir"] = new XElement("BaseDir", @"C:\extras-dir");

        var channel = factory.Create(descriptor);

        var simpleFilesChannel = Assert.IsType<SimpleFilesTagChannel>(channel);
        Assert.Equal(@"C:\extras-dir", simpleFilesChannel.Settings.BaseDir);
    }

    [Fact]
    public void Factory_Create_WrongDriver_Throws()
    {
        var factory = new SimpleFilesTagChannelFactory(NullLoggerFactory.Instance);
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "ModbusTcp",
        };

        Assert.Throws<InvalidOperationException>(() => factory.Create(descriptor));
    }

    #endregion
}

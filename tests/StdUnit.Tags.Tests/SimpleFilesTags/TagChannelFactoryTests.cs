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
        var baseDir = TestPaths.TempPath("tags");
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "factory-ch-dir",
            Driver = SimpleFilesNames.DriverName,
            BaseDir = baseDir,
        };

        var channel = factory.Create(descriptor);

        var simpleFilesChannel = Assert.IsType<SimpleFilesTagChannel>(channel);
        Assert.Equal(baseDir, simpleFilesChannel.Settings.BaseDir);
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
        var baseDirFromExtras = TestPaths.TempPath("extras-dir");
        descriptor.Extras["BaseDir"] = new XElement("BaseDir", baseDirFromExtras);

        var channel = factory.Create(descriptor);

        var simpleFilesChannel = Assert.IsType<SimpleFilesTagChannel>(channel);
        Assert.Equal(baseDirFromExtras, simpleFilesChannel.Settings.BaseDir);
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

        Assert.Throws<TagsProjectConfigurationException>(() => factory.Create(descriptor));
    }

    #endregion
}

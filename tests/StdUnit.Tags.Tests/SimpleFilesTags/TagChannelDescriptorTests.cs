using System;
using System.Xml.Linq;
using StdUnit.Tags.SimpleFiles;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

public class TagChannelDescriptorTests
{

    #region SimpleFilesTagChannelDescriptor
    [Fact]
    public void Descriptor_ToXElement_WithBaseDir_IncludesElement()
    {
        // BaseDir 只是数据（本用例不做任何文件 I/O），但用运行时临时目录而不是字面量 C:\ ：
        // 后者在 Linux 上不是 rooted 路径，一旦哪天 MakePath 开始判定 rooted，用例就会被平台悄悄改义。
        var baseDir = TestPaths.TempPath("data", "tags");
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "my-channel",
            Driver = SimpleFilesNames.DriverName,
            BaseDir = baseDir,
        };

        var element = descriptor.ToXElement();

        Assert.Equal("my-channel", element.Attribute("name")?.Value);
        Assert.Equal(SimpleFilesNames.DriverName, element.Attribute("driver")?.Value);
        Assert.Equal(baseDir, element.Element("BaseDir")?.Value);
    }

    [Fact]
    public void Descriptor_ToXElement_WithoutBaseDir_OmitsElement()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "no-base",
            Driver = SimpleFilesNames.DriverName,
        };

        var element = descriptor.ToXElement();

        Assert.Equal("no-base", element.Attribute("name")?.Value);
        Assert.Null(element.Element("BaseDir"));
    }

    [Fact]
    public void Descriptor_RoundtripXml_PreservesAllProperties()
    {
        var baseDir = TestPaths.TempPath("tags");
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "roundtrip-ch",
            Driver = SimpleFilesNames.DriverName,
            BaseDir = baseDir,
        };

        var xml = descriptor.ToXElement().ToString();
        var parsed = XElement.Parse(xml).ToTagChannelDescriptor();
        var converted = parsed.ToSimpleFilesTagChannelDescriptor();

        Assert.Equal("roundtrip-ch", converted.Name);
        Assert.Equal(SimpleFilesNames.DriverName, converted.Driver);
        Assert.Equal(baseDir, converted.BaseDir);
    }

    [Fact]
    public void Descriptor_RoundtripXml_WithoutBaseDir_BaseDirIsNull()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "no-base-ch",
            Driver = SimpleFilesNames.DriverName,
        };

        var xml = descriptor.ToXElement().ToString();
        var parsed = XElement.Parse(xml).ToTagChannelDescriptor();
        var converted = parsed.ToSimpleFilesTagChannelDescriptor();

        Assert.Null(converted.BaseDir);
    }

    #endregion

    #region ToSimpleFilesTagChannelDescriptor

    [Fact]
    public void ToSimpleFilesTagChannelDescriptor_WithExtrasBaseDir_ReadsIt()
    {
        var genericDescriptor = new TagChannelDescriptor
        {
            Name = "from-extras",
            Driver = SimpleFilesNames.DriverName,
        };
        var baseDirFromExtras = TestPaths.TempPath("from-extras");
        genericDescriptor.Extras["BaseDir"] = new XElement("BaseDir", baseDirFromExtras);

        var converted = genericDescriptor.ToSimpleFilesTagChannelDescriptor();

        Assert.Equal(baseDirFromExtras, converted.BaseDir);
    }

    [Fact]
    public void ToSimpleFilesTagChannelDescriptor_WhenAlreadySimpleFilesDescriptor_ReturnsSame()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor
        {
            Name = "already-typed",
            Driver = SimpleFilesNames.DriverName,
            BaseDir = TestPaths.TempPath("data"),
        };

        var result = descriptor.ToSimpleFilesTagChannelDescriptor();

        Assert.Same(descriptor, result);
    }

    [Fact]
    public void ToSimpleFilesTagChannelDescriptor_WrongDriver_Throws()
    {
        var descriptor = new TagChannelDescriptor
        {
            Name = "wrong",
            Driver = "ModbusTcp",
        };

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => descriptor.ToSimpleFilesTagChannelDescriptor());
        Assert.Contains(SimpleFilesNames.DriverName, ex.Message);
    }

    [Fact]
    public void ToSimpleFilesTagChannelDescriptor_CreatesCopyWithSameExtras()
    {
        var genericDescriptor = new TagChannelDescriptor
        {
            Name = "copy-test",
            Driver = SimpleFilesNames.DriverName,
        };
        genericDescriptor.Extras["SomeKey"] = new XElement("SomeKey", "SomeValue");

        var converted = genericDescriptor.ToSimpleFilesTagChannelDescriptor();

        Assert.Equal("copy-test", converted.Name);
        Assert.Equal(SimpleFilesNames.DriverName, converted.Driver);
        Assert.True(converted.Extras.ContainsKey("SomeKey"));
        Assert.Equal("SomeValue", converted.Extras["SomeKey"].Value);
    }

    #endregion
}
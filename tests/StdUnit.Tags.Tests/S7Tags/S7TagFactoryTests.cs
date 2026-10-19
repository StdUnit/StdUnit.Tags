using StdUnit.Tags.S7;
using System;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.S7Tags;

/// <summary>
/// <see cref="S7TagFactory"/>：测点种类分派、TagSize 归一化、位地址跨字节归一化、STR 的 maxlen 处理。
/// </summary>
public class S7TagFactoryTests
{
    private static S7TagFactory CreateFactory(string startAddress = "DB200.100")
    {
        var builder = new S7TagCbntBuilder();
        builder.WithCbntDescriptor(new TagCbntDescriptor { Name = "cbnt1", StartAddress = startAddress });
        return builder.MakeS7TagFactory();
    }

    private static TagDescriptor CreateDescriptor(string kind, string rawAddress, int tagSize = 0, string tagName = "tag")
        => new TagDescriptor
        {
            TagName = tagName,
            TagKind = kind,
            RawAddress = rawAddress,
            TagSize = tagSize,
        };

    [Theory]
    [InlineData(BuiltinTagKinds.BIT, typeof(S7BitTagCbntor), 1, "DB200.104.1")]
    [InlineData(BuiltinTagKinds.BYTE, typeof(S7ByteTagCbntor), 1, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT16, typeof(S7Int16TagCbntor), 2, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT16, typeof(S7UInt16TagCbntor), 2, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT32, typeof(S7Int32TagCbntor), 4, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT32, typeof(S7UInt32TagCbntor), 4, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT64, typeof(S7Int64TagCbntor), 8, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT64, typeof(S7UInt64TagCbntor), 8, "DB200.104")]
    [InlineData(BuiltinTagKinds.FLOAT, typeof(S7FloatTagCbntor), 4, "DB200.104")]
    public void CreateTag_WhenTagSizeIsZero_ShouldCreateExpectedCbntorAndNormalizeTagSize(
        string kind, Type expectedType, int expectedTagSize, string rawAddress)
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(kind, rawAddress);

        var tag = factory.CreateTag(descriptor);

        Assert.Equal(expectedType, tag.GetType());
        Assert.Equal(expectedTagSize, descriptor.TagSize);
        Assert.Equal(expectedTagSize, tag.TagSize());
        Assert.Equal(4, tag.TagOffset);
    }

    [Theory]
    [InlineData(BuiltinTagKinds.BIT, "DB200.104.1")]
    [InlineData(BuiltinTagKinds.BYTE, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT16, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT16, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT32, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT32, "DB200.104")]
    [InlineData(BuiltinTagKinds.INT64, "DB200.104")]
    [InlineData(BuiltinTagKinds.UINT64, "DB200.104")]
    [InlineData(BuiltinTagKinds.FLOAT, "DB200.104")]
    public void CreateTag_WhenTagSizeIsExplicit_ShouldKeepIt(string kind, string rawAddress)
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(kind, rawAddress, tagSize: 16);

        factory.CreateTag(descriptor);

        Assert.Equal(16, descriptor.TagSize);
    }

    [Theory]
    [InlineData("DB200.104.0", 4, 4, 0)]
    [InlineData("DB200.104.7", 4, 4, 7)]
    [InlineData("DB200.104.8", 4, 5, 0)]
    [InlineData("DB200.104.9", 4, 5, 1)]
    [InlineData("DB200.104.15", 4, 5, 7)]
    public void CreateTag_Bit_ShouldNormalizeNthBitAcrossBytes(
        string rawAddress, int expectedTagOffset, int expectedCacheOffset, byte expectedNthBit)
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(BuiltinTagKinds.BIT, rawAddress);

        var tag = Assert.IsType<S7BitTagCbntor>(factory.CreateTag(descriptor));

        Assert.Equal(expectedTagOffset, tag.TagOffset);
        Assert.Equal(expectedCacheOffset, tag.CacheOffset);
        Assert.Equal(expectedNthBit, tag.NthBit);
        Assert.Equal(1, descriptor.TagSize);
    }

    [Fact]
    public void CreateTag_ShouldCalculateOffsetFromCbntStartAddress()
    {
        var factory = CreateFactory("DB200.100");
        var descriptor = CreateDescriptor(BuiltinTagKinds.INT32, "DB200.132");

        var tag = factory.CreateTag(descriptor);

        Assert.Equal(32, tag.TagOffset);
    }

    [Fact]
    public void CreateTag_Str_ShouldCalculateSizeFromMaxlen()
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(BuiltinTagKinds.STR, "DB200.110", tagName: "str-tag");
        descriptor.Extras["maxlen"] = new XAttribute("maxlen", "10");

        var tag = Assert.IsType<S7StrTagCbntor>(factory.CreateTag(descriptor));

        Assert.Equal(10, tag.Maxlen);
        Assert.Equal(12, descriptor.TagSize);
        Assert.Equal(10, tag.TagOffset);
    }

    [Fact]
    public void CreateTag_Str_WhenMaxlenIsMissing_ShouldThrowTagsProjectXmlException()
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(BuiltinTagKinds.STR, "DB200.110", tagName: "str-tag");

        var ex = Assert.Throws<TagsProjectXmlException>(() => factory.CreateTag(descriptor));

        Assert.Contains("maxlen", ex.Message);
        Assert.Contains("Tag(str-tag)", ex.Location);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    public void CreateTag_Str_WhenMaxlenIsInvalid_ShouldThrowTagsProjectXmlException(string maxlen)
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(BuiltinTagKinds.STR, "DB200.110", tagName: "str-tag");
        descriptor.Extras["maxlen"] = new XAttribute("maxlen", maxlen);

        var ex = Assert.Throws<TagsProjectXmlException>(() => factory.CreateTag(descriptor));

        Assert.Contains("maxlen", ex.Message);
        Assert.Contains("Tag(str-tag)", ex.Location);
    }

    [Theory]
    [InlineData(BuiltinTagKinds.DOUBLE)]
    [InlineData(BuiltinTagKinds.DI)]
    [InlineData(BuiltinTagKinds.DO)]
    [InlineData(BuiltinTagKinds.Unknown)]
    public void CreateTag_WhenTagKindIsUnsupported_ShouldThrowTagsProjectConfigurationException(string kind)
    {
        var factory = CreateFactory();
        var descriptor = CreateDescriptor(kind, "DB200.104", tagName: "bad-tag");

        var ex = Assert.Throws<TagsProjectConfigurationException>(() => factory.CreateTag(descriptor));

        Assert.Contains(kind, ex.Message);
        Assert.Contains("Tag(bad-tag)", ex.Location);
    }
}

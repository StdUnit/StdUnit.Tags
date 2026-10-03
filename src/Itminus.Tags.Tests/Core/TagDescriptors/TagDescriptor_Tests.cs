using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.Core.TagDescriptors;

public class TagDescriptor_Tests
{
    public TagDescriptor_Tests()
    {
    }

    [Fact]
    public void Test_NormalizedAddress_DefaultsToRawAddress_WhenNotAssigned()
    {
        var descriptor = new TagDescriptor
        {
            RawAddress = "DB200.100.1",
            TagName = "tag-1",
            TagKind = BuiltinTagKinds.BIT,
            TagSize = 1,
        };

        Assert.Equal("DB200.100.1", descriptor.NormalizedAddress);
    }

    [Fact]
    public void Test_NormalizedAddress_FallsBackToRawAddress_WhenNormalizedIsEmpty()
    {
        var descriptor = new TagDescriptor
        {
            RawAddress = "DB200.100.1",
            TagName = "tag-2",
            TagKind = BuiltinTagKinds.BIT,
            TagSize = 1,
        };

        descriptor.NormalizedAddress = string.Empty;

        Assert.Equal("DB200.100.1", descriptor.NormalizedAddress);
    }

    [Fact]
    public void Test_NormalizedAddress_PrefersNormalizedOverRaw()
    {
        var descriptor = new TagDescriptor
        {
            RawAddress = "$$104.1",
            TagName = "tag-3",
            TagKind = BuiltinTagKinds.BIT,
            TagSize = 1,
        };

        descriptor.NormalizedAddress = "DB200.104.1";

        Assert.Equal("DB200.104.1", descriptor.NormalizedAddress);
    }

    [Fact]
    public void Test_NormalizedAddress_NeverReturnsNull()
    {
        // net472 支持分支给 getter 加了 ?? ""：契约是“永不返回 null”。
        // 即使 RawAddress 与归一化地址都是 null，也应返回空字符串而不是 null。
        var descriptor = new TagDescriptor
        {
            RawAddress = null!,
            TagName = "tag-4",
            TagKind = BuiltinTagKinds.BIT,
            TagSize = 1,
        };

        var normalized = descriptor.NormalizedAddress;

        Assert.NotNull(normalized);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void Test_ConversionWithXElement_PersistsRawAddressOnly()
    {
        var descriptor1 = new TagDescriptor
        {
            RawAddress = "$$104.1",
            TagName = "tag-raw-roundtrip",
            TagKind = BuiltinTagKinds.BIT,
            TagSize = 1,
            EndianKind = EndianKinds.LittleEndian,
            AccessMode = TagAccessMode.RW,
        };

        // simulate runtime normalization before write-back
        descriptor1.NormalizedAddress = "DB200.104.1";

        var element = descriptor1.ToXElement();
        Assert.Equal("$$104.1", (string?)element.Attribute("address"));

        var descriptor2 = element.ToTagDescriptor();
        Assert.Equal("$$104.1", descriptor2.RawAddress);
        Assert.Equal("$$104.1", descriptor2.NormalizedAddress);
    }

    [Fact]
    public void Test_ConversionWithXElement()
    {
        var descriptor1 = new TagDescriptor
        {
            RawAddress = "DB200.100.1",
            TagKind = BuiltinTagKinds.BIT,
            TagName = "拍照-请求-标志",
            EndianKind = EndianKinds.LittleEndian,
            TagSize = 1,
            AccessMode = TagAccessMode.RW,
            Note = null,
        };
        var ele1 = descriptor1.ToXElement();
        var xml1 = ele1.ToString();

        var descriptor2 = ele1.ToTagDescriptor();
        var ele2 = descriptor2.ToXElement();
        var xml2 = ele2.ToString();
        Assert.Equal(xml1, xml2);

        Assert.Equal(descriptor1.TagName, descriptor2.TagName);
        Assert.Equal(descriptor1.RawAddress, descriptor2.RawAddress);
        Assert.Equal(descriptor1.TagKind, descriptor2.TagKind);
        Assert.Equal(descriptor1.EndianKind, descriptor2.EndianKind);
        Assert.Equal(descriptor1.TagSize, descriptor2.TagSize);
        Assert.Equal(descriptor1.AccessMode, descriptor2.AccessMode);
        Assert.Equal(descriptor1.Note, descriptor2.Note);
    }

    [Fact]
    public void Test_ConversionWithXElement_IncludesExtras()
    {
        var descriptor1 = new TagDescriptor
        {
            RawAddress = "DB200.100.2",
            TagKind = BuiltinTagKinds.BYTE,
            TagName = "ExtraTest",
            EndianKind = EndianKinds.LittleEndian,
            TagSize = 1,
            AccessMode = TagAccessMode.RW,
            Note = "with extras",
        };

        // add extras
        descriptor1.Extras["ext1"] = new XAttribute("ext1", "v1");
        descriptor1.Extras["ext2"] = new XAttribute("ext2", "v2");

        var ele1 = descriptor1.ToXElement();
        var descriptor2 = ele1.ToTagDescriptor();

        Assert.NotNull(descriptor2.Extras);
        Assert.True(descriptor2.Extras.ContainsKey("ext1"));
        Assert.True(descriptor2.Extras.ContainsKey("ext2"));
        Assert.Equal("v1", descriptor2.Extras["ext1"].Value);
        Assert.Equal("v2", descriptor2.Extras["ext2"].Value);

        // ensure roundtrip xml equals
        var ele2 = descriptor2.ToXElement();
        Assert.Equal(ele1.ToString(), ele2.ToString());
    }

}

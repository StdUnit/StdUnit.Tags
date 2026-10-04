using StdUnit.Tags.OpcUaClient.Cbnts;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

public class OpcUaClientTagFactoryTests
{
    [Fact]
    public void CreateTag_ReturnsOpcUaClientTagCbntor()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var builder = new OpcUaClientTagCbntBuilder();
        var factory = new OpcUaClientTagFactory(builder);

        var descriptor = new TagDescriptor
        {
            TagName = "myTag",
            RawAddress = "ns=1;s=MyVar",
            TagKind = BuiltinTagKinds.FLOAT,
            TagSize = 4,
        };
        var tag = factory.CreateTag(descriptor);

        Assert.IsType<OpcUaClientTagCbntor>(tag);
        Assert.Equal("myTag", tag.TagName());
        Assert.Equal("ns=1;s=MyVar", tag.NormalizedAddress());
    }

    [Fact]
    public void CreateTag_WithDifferentKinds_AllCreated()
    {
        var builder = new OpcUaClientTagCbntBuilder();
        var factory = new OpcUaClientTagFactory(builder);

        var boolTag = factory.CreateTag(new TagDescriptor { TagName = "b", RawAddress = "ns=1;s=BoolVar", TagKind = BuiltinTagKinds.BIT, TagSize = 1 });
        var int32Tag = factory.CreateTag(new TagDescriptor { TagName = "i32", RawAddress = "ns=1;s=IntVar", TagKind = BuiltinTagKinds.INT32, TagSize = 4 });
        var floatTag = factory.CreateTag(new TagDescriptor { TagName = "f", RawAddress = "ns=1;s=FloatVar", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 });
        var strTag = factory.CreateTag(new TagDescriptor { TagName = "s", RawAddress = "ns=1;s=StrVar", TagKind = BuiltinTagKinds.STR, TagSize = 256 });

        Assert.IsType<OpcUaClientTagCbntor>(boolTag);
        Assert.IsType<OpcUaClientTagCbntor>(int32Tag);
        Assert.IsType<OpcUaClientTagCbntor>(floatTag);
        Assert.IsType<OpcUaClientTagCbntor>(strTag);
    }
}

using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.OpcUaClient.Cbnts;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

public class OpcUaClientTagCbntBuilderTests
{
    [Fact]
    public void AddTags_CreatesTagsForEachDescriptor()
    {
        var builder = new OpcUaClientTagCbntBuilder();
        var descriptors = new[]
        {
            new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 },
            new TagDescriptor { TagName = "t2", RawAddress = "ns=1;s=Var2", TagKind = BuiltinTagKinds.BIT, TagSize = 1 },
        };

        var result = builder.AddTags(descriptors, null!);

        Assert.Same(builder, result);
        Assert.Equal(2, builder.TagCbnt.Children.Count);
        Assert.True(builder.TagCbnt.Children.ContainsKey("t1"));
        Assert.True(builder.TagCbnt.Children.ContainsKey("t2"));
        Assert.IsType<OpcUaClientTagCbntor>(builder.TagCbnt.Children["t1"]);
        Assert.IsType<OpcUaClientTagCbntor>(builder.TagCbnt.Children["t2"]);
    }

    [Fact]
    public void AddTags_EmptyList_AddsNoChildren()
    {
        var builder = new OpcUaClientTagCbntBuilder();

        builder.AddTags([], null!);

        Assert.Empty(builder.TagCbnt.Children);
    }


}

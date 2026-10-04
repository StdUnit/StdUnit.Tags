using System;
using StdUnit.Tags.Tests.Core;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagContainers;

public class TagContainerExtensionsTests
{

    #region IntoTagContainer
    [Fact]
    public void From_ITagGrp_IntoTagContainer_ProducesSameType()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, null);

        var container = grp.IntoTagContainer();

        Assert.True(container.IsTagGrp);
    }

    [Fact]
    public void From_ITagCbnt_IntoTagContainer_ProducesSameType()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "c" });

        var container = cbnt.IntoTagContainer();

        Assert.True(container.IsTagCbnt);
    }
    #endregion
    #region SearchRequiredChannel
    [Fact]
    public void SearchRequiredChannel_WhenCbnt_DelegatesToCbnt()
    {
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "c" }) { Parent = grp };
        var container = TagContainer.From(cbnt);

        var result = container.SearchRequiredChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchRequiredChannel_WhenGrp_DelegatesToGrp()
    {
        var channel = new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var container = TagContainer.From(grp);

        var result = container.SearchRequiredChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchRequiredChannel_WhenNoChannel_Throws()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, null);
        var container = TagContainer.From(grp);

        Assert.Throws<Exception>(() => container.SearchRequiredChannel());
    }
    #endregion
}
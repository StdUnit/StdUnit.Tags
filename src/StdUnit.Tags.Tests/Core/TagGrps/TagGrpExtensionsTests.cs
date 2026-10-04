using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrps;

public class TagGrpExtensionsTests
{
    #region SearchScanInterval

    [Fact]
    public void SearchScanInterval_WhenDescriptorHasValue_ReturnsOwnValue()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test", ScanInterval = 200 }, channel: null);

        var result = grp.SearchScanInterval();

        Assert.Equal(200, result);
    }

    [Fact]
    public void SearchScanInterval_WhenDescriptorHasZero_ReturnsZero()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "test", ScanInterval = 0 }, channel: null);

        var result = grp.SearchScanInterval();

        Assert.Equal(0, result);
    }

    [Fact]
    public void SearchScanInterval_BubblesUpMultiLevel()
    {
        // 中间节点 ScanInterval 为 null → 冒泡到 root
        var root = new TagGrp(new TagGrpDescriptor { Name = "root", ScanInterval = 500 }, channel: null);
        var mid = new TagGrp(new TagGrpDescriptor { Name = "mid" }, channel: null) { Parent = root };
        var leaf = new TagGrp(new TagGrpDescriptor { Name = "leaf" }, channel: null) { Parent = mid };

        var result = leaf.SearchScanInterval();

        Assert.Equal(500, result);
    }

    [Fact]
    public void SearchScanInterval_DescriptorExistsButScanIntervalNull_BubblesUp()
    {
        // Descriptor 存在但 ScanInterval 为 null → 冒泡到父级
        var parent = new TagGrp(new TagGrpDescriptor { Name = "parent", ScanInterval = 200 }, channel: null);
        var child = new TagGrp(new TagGrpDescriptor { Name = "child" }, channel: null)
        {
            Parent = parent,
        };

        var result = child.SearchScanInterval();

        Assert.Equal(200, result);
    }

    #endregion

    #region TagName (ITagGrp)

    [Fact]
    public void TagName_ForITagGrp_ReturnsDescriptorName()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "myGrp" }, channel: null);

        var result = grp.TagName();

        Assert.Equal("myGrp", result);
    }

    [Fact]
    public void TagName_ForITagGrp_DoesNotBubbleUp()
    {
        var parent = new TagGrp(new TagGrpDescriptor { Name = "parent" }, channel: null);
        var child = new TagGrp(new TagGrpDescriptor { Name = "child" }, channel: null) { Parent = parent };

        var result = child.TagName();

        // TagName() 不冒泡，始终返回自身 Descriptor.Name
        Assert.Equal("child", result);
        Assert.NotEqual("parent", result);
    }

    #endregion

    #region IsEntry (ITagGrp)

    [Fact]
    public void IsEntry_WhenDescriptorIsEntry_ReturnsTrue()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true }, channel: null);

        Assert.True(grp.IsEntry());
    }

    [Fact]
    public void IsEntry_WhenDescriptorNotEntry_ReturnsFalse()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "normal", IsEntry = false }, channel: null);

        Assert.False(grp.IsEntry());
    }

    [Fact]
    public void IsEntry_DefaultIsFalse()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "normal" }, channel: null);

        Assert.False(grp.IsEntry());
    }

    #endregion
}

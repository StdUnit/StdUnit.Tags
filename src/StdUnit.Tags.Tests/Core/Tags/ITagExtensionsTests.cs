using System;
using StdUnit.Tags;
using StdUnit.Tags.S7;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Tags;

public class ITagExtensionsTests
{
    private FakedChannel CreateChannel() => new FakedChannel(new TagChannelDescriptor { Name = "fake-channel" });

    #region SearchEntry

    [Fact]
    public void SearchEntry_WhenParentGrpIsEntry_ReturnsParent()
    {
        var channel = CreateChannel();
        var entry = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true }, channel);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, entry);

        var result = tag.SearchEntry();

        Assert.Same(entry, result);
    }

    [Fact]
    public void SearchEntry_WhenParentGrpNotEntry_BubblesUpToAncestor()
    {
        var channel = CreateChannel();
        var rootEntry = new TagGrp(new TagGrpDescriptor { Name = "root", IsEntry = true }, channel);
        rootEntry.AddTag(new TagGrp(new TagGrpDescriptor { Name = "sub" }, null));
        var subGrp = rootEntry.SelectGrp("sub");
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, subGrp);

        var result = tag.SearchEntry();

        Assert.Same(rootEntry, result);
    }

    [Fact]
    public void SearchEntry_WhenNoEntry_ReturnsNull()
    {
        var channel = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g", IsEntry = false }, channel);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, grp);

        var result = tag.SearchEntry();

        Assert.Null(result);
    }

    [Fact]
    public void SearchEntry_WhenParentIsCbnt_BubblesToCbntParent()
    {
        var channel = CreateChannel();
        var entry = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true }, channel);
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "cbnt1" }) { Parent = entry };
        var tag = new S7ByteTagCbntor(new TagDescriptor { TagName = "t1", RawAddress = "0", TagKind = BuiltinTagKinds.BYTE, TagSize = 1 }, cbnt, 0)
        {
            Parent = TagContainer.From(cbnt)
        };

        var result = tag.SearchEntry();

        Assert.Same(entry, result);
    }

    #endregion

    #region SearchRequiredChannel

    [Fact]
    public void SearchRequiredChannel_WhenTagHasOwnChannel_ReturnsOwnChannel()
    {
        var channel = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel, grp);

        var result = tag.SearchRequiredChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchRequiredChannel_WhenTagHasNoChannel_BubblesToParent()
    {
        var channel = CreateChannel();
        var parentGrp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, parentGrp);

        var result = tag.SearchRequiredChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchRequiredChannel_WhenNoChannelAtAll_Throws()
    {
        // FakedTag 构造函数会调用 SearchRequiredChannel(), 没有通道会直接抛出。
        // 使用 TagCbntor（其构造函数不检查通道）来测试此路径。
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "cbnt1" });
        var tag = new S7ByteTagCbntor(new TagDescriptor { TagName = "t1", RawAddress = "0", TagKind = BuiltinTagKinds.BYTE, TagSize = 1 }, cbnt, 0);

        var ex = Assert.Throws<InvalidOperationException>(() => tag.SearchRequiredChannel());
        Assert.Contains("t1", ex.Message);
    }

    #endregion

    #region AsTag

    [Fact]
    public void AsTag_WhenTypeMatches_ReturnsTag()
    {
        var channel = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        ITag tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, grp);

        var result = tag.AsTag<FakedTag>();

        Assert.NotNull(result);
    }

    [Fact]
    public void AsTag_WhenTypeMismatches_Throws()
    {
        var channel = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        ITag tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, grp);

        var ex = Assert.Throws<InvalidCastException>(() => tag.AsTag<S7ByteTagCbntor>());
        Assert.Contains(typeof(S7ByteTagCbntor).ToString(), ex.Message);
    }

    #endregion

    #region GetTagValue

    [Fact]
    public void GetTagValue_ReturnsValue()
    {
        var channel = CreateChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, channel);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, channel: null, grp);
        tag.Value = "hello";

        var result = tag.GetTagValue<string>();

        Assert.Equal("hello", result);
    }

    #endregion
}

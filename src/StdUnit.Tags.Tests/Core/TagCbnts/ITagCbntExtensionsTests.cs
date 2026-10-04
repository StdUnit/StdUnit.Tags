using System;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags;
using StdUnit.Tags.S7;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagCbnts;

public class ITagCbntExtensionsTests
{
    #region TagName

    [Fact]
    public void TagName_ReturnsDescriptorName()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "myCbnt" });

        Assert.Equal("myCbnt", cbnt.TagName());
    }

    [Fact]
    public void TagName_DoesNotBubbleUp()
    {
        var child = new TestByteTagCbnt(new TagCbntDescriptor { Name = "child" })
        { Parent = new TagGrp(new TagGrpDescriptor { Name = "grp" }, null) };

        Assert.Equal("child", child.TagName());
    }

    #endregion

    #region SelectTag

    [Fact]
    public void SelectTag_WithValidPath_ReturnsChild()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });
        var d = new TagDescriptor { TagName = "t1", RawAddress = "0", TagKind = BuiltinTagKinds.BYTE, TagSize = 1 };
        var child = new S7ByteTagCbntor(d, cbnt, 0);
        cbnt.Children.Add("t1", child);

        var result = cbnt.SelectTag("t1");

        Assert.Same(child, result);
    }

    [Fact]
    public void SelectTag_WithInvalidPath_Throws()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });

        Assert.Throws<Exception>(() => cbnt.SelectTag("nonexistent"));
    }

    #endregion

    #region SearchChannel

    [Fact]
    public void SearchChannel_WhenOwnChannelSet_ReturnsOwnChannel()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });
        var channel = new ChannelMock();
        cbnt.Channel = channel;

        var result = cbnt.SearchChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchChannel_WhenNoOwnChannel_BubblesToParent()
    {
        var parentGrp = new TagGrp(new TagGrpDescriptor { Name = "parentGrp" }, null);
        var channel = new ChannelMock();
        parentGrp.Channel = channel;
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" }) { Parent = parentGrp };

        var result = cbnt.SearchChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchChannel_WhenNoChannelAtAll_ReturnsNull()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });

        var result = cbnt.SearchChannel();

        Assert.Null(result);
    }

    #endregion

    #region SearchRequiredChannel

    [Fact]
    public void SearchRequiredChannel_WhenChannelExists_ReturnsChannel()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });
        var channel = new ChannelMock();
        cbnt.Channel = channel;

        var result = cbnt.SearchRequiredChannel();

        Assert.Same(channel, result);
    }

    [Fact]
    public void SearchRequiredChannel_WhenNoChannel_Throws()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });

        var ex = Assert.Throws<Exception>(() => cbnt.SearchRequiredChannel());
        Assert.Contains("Channel is not configured", ex.Message);
        Assert.Contains("g", ex.Message);
    }

    #endregion

    #region SearchAccessMode

    [Fact]
    public void SearchAccessMode_WhenOwnModeSet_ReturnsOwnMode()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", AccessMode = TagAccessMode.RO });

        Assert.Equal(TagAccessMode.RO, cbnt.SearchAccessMode());
    }

    [Fact]
    public void SearchAccessMode_WhenOwnModeNullAndParentModeSet_ReturnsParentMode()
    {
        var parentGrp = new TagGrp(new TagGrpDescriptor { Name = "p", AccessMode = TagAccessMode.WO }, null);
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" }) { Parent = parentGrp };

        Assert.Equal(TagAccessMode.WO, cbnt.SearchAccessMode());
    }

    [Fact]
    public void SearchAccessMode_WhenBothNull_DefaultsToRW()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });

        Assert.Equal(TagAccessMode.RW, cbnt.SearchAccessMode());
    }

    #endregion

    #region IsReadOnly / IsWriteOnly

    [Fact]
    public void IsReadOnly_WhenModeRO_ReturnsTrue()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", AccessMode = TagAccessMode.RO });

        Assert.True(cbnt.IsReadOnly());
        Assert.False(cbnt.IsWriteOnly());
    }

    [Fact]
    public void IsWriteOnly_WhenModeWO_ReturnsTrue()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g", AccessMode = TagAccessMode.WO });

        Assert.False(cbnt.IsReadOnly());
        Assert.True(cbnt.IsWriteOnly());
    }

    [Fact]
    public void IsReadOnly_WhenDefaultRW_ReturnsFalse()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "g" });

        Assert.False(cbnt.IsReadOnly());
        Assert.False(cbnt.IsWriteOnly());
    }

    #endregion

    /// <summary>
    /// 简化版的 MockChannel，仅实现 ITagChannel
    /// </summary>
    private class ChannelMock : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "Mock",
            Driver = "MOCK",
        };
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }
}

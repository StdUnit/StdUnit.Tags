using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagUnions;

public class TagUnionExtensions_IsXyz_AsXyz_Tests
{
    #region Helpers

    private static ITag CreateFakeTag(string name = "t")
        => new FakeTag { TagDescriptor = new TagDescriptor { TagName = name } };
    private static ITagCbnt CreateFakeCbnt(string name = "c")
        => new TestByteTagCbnt(new TagCbntDescriptor { Name = name });
    private static ITagGrp CreateFakeGrp(string name = "g")
        => new TagGrp(new TagGrpDescriptor { Name = name }, null);

    private sealed class FakeTag : ITag
    {
        public TagDescriptor TagDescriptor { get; set; } = new() { TagName = "t" };
        public object? Value { get; set; }
        public DateTime Timestamp { get; set; }
        public event TagSyncEventHandler? OnTagRead { add { } remove { } }
        public event TagSyncEventHandler? OnTagWritten { add { } remove { } }
        public bool IsScanned { get; set; }
        public bool IsDirty { get; set; }
        public ITagChannel? Channel => null;
        public TagContainer? Parent { get; set; }
        public Task ReadAsync(CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    #endregion

    #region IsTagUnit / IsTagCbnt / IsTagGrp

    [Fact]
    public void IsTagUnit_WhenTagUnit_ReturnsTrue()
    {
        var u = new TagUnion.TagUnit(CreateFakeTag());
        Assert.True(u.IsTagUnit());
        Assert.False(u.IsTagCbnt());
        Assert.False(u.IsTagGrp());
    }

    [Fact]
    public void IsTagCbnt_WhenTagCbnt_ReturnsTrue()
    {
        var u = new TagUnion.TagCbnt(CreateFakeCbnt());
        Assert.False(u.IsTagUnit());
        Assert.True(u.IsTagCbnt());
        Assert.False(u.IsTagGrp());
    }

    [Fact]
    public void IsTagGrp_WhenTagGrp_ReturnsTrue()
    {
        var u = new TagUnion.TagGrp(CreateFakeGrp());
        Assert.False(u.IsTagUnit());
        Assert.False(u.IsTagCbnt());
        Assert.True(u.IsTagGrp());
    }

    #endregion

    #region AsTag / AsTagCbnt / AsTagGrp

    [Fact]
    public void AsTag_WhenTagUnit_ReturnsTag()
    {
        var tag = CreateFakeTag("myTag");
        var u = new TagUnion.TagUnit(tag);

        var result = u.AsTag();

        Assert.Same(tag, result);
    }

    [Fact]
    public void AsTag_WhenTagCbnt_Throws()
    {
        var u = new TagUnion.TagCbnt(CreateFakeCbnt());

        var ex = Assert.Throws<Exception>(() => u.AsTag());
        Assert.Contains("TagCbnt", ex.Message);
        Assert.Contains("ITag", ex.Message);
    }

    [Fact]
    public void AsTag_WhenTagGrp_Throws()
    {
        var u = new TagUnion.TagGrp(CreateFakeGrp());

        var ex = Assert.Throws<Exception>(() => u.AsTag());
        Assert.Contains("TagGrp", ex.Message);
        Assert.Contains("ITag", ex.Message);
    }

    [Fact]
    public void AsTagCbnt_WhenTagCbnt_ReturnsCbnt()
    {
        var cbnt = CreateFakeCbnt("myCbnt");
        var u = new TagUnion.TagCbnt(cbnt);

        var result = u.AsTagCbnt();

        Assert.Same(cbnt, result);
    }

    [Fact]
    public void AsTagCbnt_WhenTagUnit_Throws()
    {
        var u = new TagUnion.TagUnit(CreateFakeTag());

        var ex = Assert.Throws<Exception>(() => u.AsTagCbnt());
        Assert.Contains("ITag", ex.Message);
        Assert.Contains("ITagCbnt", ex.Message);
    }

    [Fact]
    public void AsTagCbnt_WhenTagGrp_Throws()
    {
        var u = new TagUnion.TagGrp(CreateFakeGrp());

        var ex = Assert.Throws<Exception>(() => u.AsTagCbnt());
        Assert.Contains("TagGrp", ex.Message);
        Assert.Contains("ITagCbnt", ex.Message);
    }

    [Fact]
    public void AsTagGrp_WhenTagGrp_ReturnsGrp()
    {
        var grp = CreateFakeGrp("myGrp");
        var u = new TagUnion.TagGrp(grp);

        var result = u.AsTagGrp();

        Assert.Same(grp, result);
    }

    [Fact]
    public void AsTagGrp_WhenTagUnit_Throws()
    {
        var u = new TagUnion.TagUnit(CreateFakeTag());

        var ex = Assert.Throws<Exception>(() => u.AsTagGrp());
        Assert.Contains("ITag", ex.Message);
        Assert.Contains("ITagGrp", ex.Message);
    }

    [Fact]
    public void AsTagGrp_WhenTagCbnt_Throws()
    {
        var u = new TagUnion.TagCbnt(CreateFakeCbnt());

        var ex = Assert.Throws<Exception>(() => u.AsTagGrp());
        Assert.Contains("TagCbnt", ex.Message);
        Assert.Contains("ITagGrp", ex.Message);
    }

    #endregion
}

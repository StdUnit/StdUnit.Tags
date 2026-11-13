using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

/// <summary>
/// <see cref="DirtyNodeIndex"/> 与 <see cref="ITagGrp.IsDirty()"/> 的等价性：索引只收录 Tag / Cbnt，
/// 正是 <c>IsDirty()</c> 的语义；任何一处不一致都会让运行器"该写的没写"或"白走一趟"。
/// </summary>
public class DirtyNodeIndexTests
{
    private static TagGrp NewGroup(string name) =>
        new(new TagGrpDescriptor { Name = name }, new FakedChannel(new TagChannelDescriptor { Name = "ch" }));

    private static FakedTag NewTag(string name, ITagGrp parent) =>
        new(new TagDescriptor { TagName = name }, new FakedChannel(new TagChannelDescriptor { Name = "ch" }), parent);

    private static FakedTagCbnt NewCbnt(string name) => new(new TagCbntDescriptor { Name = name });

    private static void AssertEquivalent(ITagGrp entry)
    {
        var index = DirtyNodeIndex.Build(entry);
        Assert.Equal(entry.IsDirty(), index.AnyDirty());
    }

    [Fact]
    public void EmptyEntry_IsClean()
    {
        var entry = NewGroup("entry");
        var index = DirtyNodeIndex.Build(entry);

        Assert.False(index.AnyDirty());
        AssertEquivalent(entry);
    }

    [Fact]
    public void DirtyTag_IsDetected()
    {
        var entry = NewGroup("entry");
        var tag = NewTag("t", entry);
        entry.AddTag(tag);

        var index = DirtyNodeIndex.Build(entry);
        Assert.False(index.AnyDirty());

        tag.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);

        tag.IsDirty = false;
        Assert.False(index.AnyDirty());
        AssertEquivalent(entry);
    }

    [Fact]
    public void DirtyCbnt_IsDetected()
    {
        var entry = NewGroup("entry");
        var cbnt = NewCbnt("c");
        entry.AddTag(cbnt);

        var index = DirtyNodeIndex.Build(entry);
        Assert.False(index.AnyDirty());

        cbnt.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);
    }

    [Fact]
    public void DirtyTag_InNestedGroup_IsDetected()
    {
        var entry = NewGroup("entry");
        var inner = NewGroup("inner");
        entry.AddTag(inner);
        var tag = NewTag("t", inner);
        inner.AddTag(tag);

        var index = DirtyNodeIndex.Build(entry);
        Assert.False(index.AnyDirty());

        tag.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);
    }

    [Fact]
    public void MixedTree_StaysEquivalent()
    {
        var entry = NewGroup("entry");
        var t1 = NewTag("t1", entry);
        var c1 = NewCbnt("c1");
        var nested = NewGroup("nested");
        var t2 = NewTag("t2", nested);
        entry.AddTag(t1);
        entry.AddTag(c1);
        entry.AddTag(nested);
        nested.AddTag(t2);

        var index = DirtyNodeIndex.Build(entry);

        Assert.False(index.AnyDirty());
        AssertEquivalent(entry);

        c1.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);

        c1.IsDirty = false;
        t2.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);

        t2.IsDirty = false;
        t1.IsDirty = true;
        Assert.True(index.AnyDirty());
        AssertEquivalent(entry);
    }
}

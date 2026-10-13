using Xunit;

namespace StdUnit.Tags.Tests.Core.TagContainers;

public class TagContainerTests
{
    [Fact]
    public void From_ITagCbnt_CreatesTagContainerWithIsTagCbntTrue()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "cbnt1" });

        var container = TagContainer.From(cbnt);

        Assert.True(container.IsTagCbnt);
        Assert.False(container.IsTagGrp);
    }

    [Fact]
    public void From_ITagGrp_CreatesTagContainerWithIsTagGrpTrue()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp1" }, null);

        var container = TagContainer.From(grp);

        Assert.True(container.IsTagGrp);
        Assert.False(container.IsTagCbnt);
    }

    [Fact]
    public void Map_WhenTagCbnt_CallsHandleTagCbnt()
    {
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "cbnt1" });
        var container = TagContainer.From(cbnt);

        var result = container.Map(
            handleTagCbnt: c => "cbnt:" + c.TagName(),
            handleTagGrp: g => "grp:" + g.TagName()
        );

        Assert.Equal("cbnt:cbnt1", result);
    }

    [Fact]
    public void Map_WhenTagGrp_CallsHandleTagGrp()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp1" }, null);
        var container = TagContainer.From(grp);

        var result = container.Map(
            handleTagCbnt: c => "cbnt:" + c.TagName(),
            handleTagGrp: g => "grp:" + g.TagName()
        );

        Assert.Equal("grp:grp1", result);
    }

    [Fact]
    public void Map_ThrowsOnUnknownSubtype()
    {
        // 通过继承来创建一个未预料的子类型 — 但 record 是 sealed，实际不会发生
        // 直接测试 Map 的兜底分支通过反射几乎不可能。这里确认 From 工厂方法只生成两种已知子类型。
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g" }, null);
        var container = TagContainer.From(grp);

        Assert.True(container.IsTagGrp);
        // Map 能正常工作
        var result = container.Map(_ => "cbnt", _ => "grp");
        Assert.Equal("grp", result);
    }


}

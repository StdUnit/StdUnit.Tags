using System.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrps;

public class ScanEntriesTests
{
    [Fact]
    public void ScanEntries_WhenSelfIsEntry_ReturnsSelf()
    {
        var grp = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true }, null);

        var entries = grp.ScanEntries();

        Assert.Single(entries);
        Assert.Same(grp, entries[0]);
    }

    [Fact]
    public void ScanEntries_WhenChildIsEntry_ReturnsChild()
    {
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var child = new TagGrp(new TagGrpDescriptor { Name = "child", IsEntry = true }, null);
        root.AddTag(child);

        var entries = root.ScanEntries();

        Assert.Single(entries);
        Assert.Same(child, entries[0]);
    }

    [Fact]
    public void ScanEntries_WhenNonEntryChildHasEntryGrandchild_ReturnsGrandchild()
    {
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var mid = new TagGrp(new TagGrpDescriptor { Name = "mid" }, null);  // 不是入口
        var leaf = new TagGrp(new TagGrpDescriptor { Name = "leaf", IsEntry = true }, null);
        mid.AddTag(leaf);
        root.AddTag(mid);

        var entries = root.ScanEntries();

        Assert.Single(entries);
        Assert.Same(leaf, entries[0]);
    }

    [Fact]
    public void ScanEntries_WhenMultipleChildrenAreEntries_ReturnsAll()
    {
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var a = new TagGrp(new TagGrpDescriptor { Name = "a", IsEntry = true }, null);
        var b = new TagGrp(new TagGrpDescriptor { Name = "b", IsEntry = true }, null);
        root.AddTag(a);
        root.AddTag(b);

        var entries = root.ScanEntries();

        Assert.Equal(2, entries.Count);
        Assert.Contains(a, entries);
        Assert.Contains(b, entries);
    }



    [Fact]
    public void ScanEntries_RecursesIntoNonEntryChildren()
    {
        /*
            root
            ├─ mid (not entry)
            │  ├─ leaf1 (entry)
            │  └─ leaf2 (entry)
        */
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var mid = new TagGrp(new TagGrpDescriptor { Name = "mid" }, null);  // 非入口
        var leaf1 = new TagGrp(new TagGrpDescriptor { Name = "leaf1", IsEntry = true }, null);
        var leaf2 = new TagGrp(new TagGrpDescriptor { Name = "leaf2", IsEntry = true }, null);
        mid.AddTag(leaf1);
        mid.AddTag(leaf2);
        root.AddTag(mid);

        var entries = root.ScanEntries();

        Assert.Equal(2, entries.Count);
        Assert.Contains(leaf1, entries);
        Assert.Contains(leaf2, entries);
    }


    [Fact]
    public void ScanEntries_WhenNoChildren_ReturnsEmpty()
    {
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var entries = root.ScanEntries();
        Assert.Empty(entries);
    }

    [Fact]
    public void ScanEntries_WhenNoEntryExists_ReturnsEmpty()
    {
        var root = new TagGrp(new TagGrpDescriptor { Name = "root" }, null);
        var mid = new TagGrp(new TagGrpDescriptor { Name = "mid" }, null);
        root.AddTag(mid);

        var entries = root.ScanEntries();

        Assert.Empty(entries);
    }

    /// <summary>
    /// 语义固化：入口识别遇到入口后<b>立即停止下探</b>，所以嵌套在入口内的
    /// <c>isEntry="true"</c> <b>不会</b>被识别为独立入口（它只是一个普通子组，
    /// 其子树仍由最外层入口的 runner 轮询）。错误配置由 <see cref="NestedEntryValidator"/> 拒绝。
    /// </summary>
    [Fact]
    public void ScanEntries_EntryNestedInsideEntry_ReturnsOnlyOutermost()
    {
        /*
            entry (entry)
            ├─ subEntry (entry, 嵌套 → 不生效)
            │  └─ leaf (entry, 嵌套 → 不生效)
        */
        var entry = new TagGrp(new TagGrpDescriptor { Name = "entry", IsEntry = true }, null);
        var subEntry = new TagGrp(new TagGrpDescriptor { Name = "subEntry", IsEntry = true }, null);
        var leaf = new TagGrp(new TagGrpDescriptor { Name = "leaf", IsEntry = true }, null);
        subEntry.AddTag(leaf);
        entry.AddTag(subEntry);

        var entries = entry.ScanEntries();

        Assert.Single(entries);
        Assert.Same(entry, entries[0]);
    }
}

using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core;

/// <summary>
/// 访问模式（<c>access</c>）的继承语义：**逐级冒泡**——自身 → 父级 Cbnt → 各级 Grp → 默认 RW。<br/>
/// 历史上 <see cref="ITag"/> / <see cref="ITagCbnt"/> 只查"直接父级自己配了没"，
/// 于是 `Grp(RO) → Cbnt(未配) → Tag` 会被误判成 RW（而 `Grp(RO) → Tag` 却是 RO）。2.0 统一为逐级冒泡。
/// </summary>
public class AccessModeInheritanceTests
{
    private static TagGrp NewGroup(string name, TagAccessMode? access = null)
        => new(new TagGrpDescriptor { Name = name, AccessMode = access }, new FakedChannel(new TagChannelDescriptor { Name = "ch" }));

    private static FakedTagCbnt NewCbnt(string name, TagAccessMode? access = null)
        => new(new TagCbntDescriptor { Name = name, AccessMode = access });

    private static FakedTag NewTag(string name, ITagGrp parent, TagAccessMode? access = null)
        => new(new TagDescriptor { TagName = name, AccessMode = access }, new FakedChannel(new TagChannelDescriptor { Name = "ch" }), parent);

    [Fact]
    public void Tag_WithNothingConfigured_DefaultsToRW()
    {
        var grp = NewGroup("g");
        var tag = NewTag("t", grp);
        grp.AddTag(tag);

        Assert.Equal(TagAccessMode.RW, tag.SearchAccessMode());
    }

    [Fact]
    public void Tag_InheritsFromDirectParentGroup()
    {
        var grp = NewGroup("g", TagAccessMode.RO);
        var tag = NewTag("t", grp);
        grp.AddTag(tag);

        Assert.Equal(TagAccessMode.RO, tag.SearchAccessMode());
    }

    /// <summary>核心回归：隔一层未配置的 Cbnt，也必须继承到祖先 Grp 的配置。</summary>
    [Fact]
    public void Tag_InheritsThroughUnconfiguredCbntFromGrandparentGroup()
    {
        var grp = NewGroup("g", TagAccessMode.RO);
        var cbnt = NewCbnt("c");            // 未配置 access
        grp.AddTag(cbnt);

        var tag = NewTag("t", grp);
        tag.Parent = TagContainer.From(cbnt);   // 父容器是 Cbnt

        Assert.Equal(TagAccessMode.RO, cbnt.SearchAccessMode());
        Assert.Equal(TagAccessMode.RO, tag.SearchAccessMode());
    }

    [Fact]
    public void Cbnt_OverridesAncestor()
    {
        var grp = NewGroup("g", TagAccessMode.RO);
        var cbnt = NewCbnt("c", TagAccessMode.WO);
        grp.AddTag(cbnt);

        var tag = NewTag("t", grp);
        tag.Parent = TagContainer.From(cbnt);

        Assert.Equal(TagAccessMode.WO, cbnt.SearchAccessMode());
        Assert.Equal(TagAccessMode.WO, tag.SearchAccessMode());
    }

    [Fact]
    public void Tag_OverridesAllAncestors()
    {
        var grp = NewGroup("g", TagAccessMode.RO);
        var cbnt = NewCbnt("c", TagAccessMode.WO);
        grp.AddTag(cbnt);

        var tag = NewTag("t", grp, TagAccessMode.R1W);
        tag.Parent = TagContainer.From(cbnt);

        Assert.Equal(TagAccessMode.R1W, tag.SearchAccessMode());
    }

    [Fact]
    public void Tag_InheritsThroughNestedGroups()
    {
        var outer = NewGroup("outer", TagAccessMode.RO);
        var inner = NewGroup("inner");     // 未配置
        outer.AddTag(inner);

        var tag = NewTag("t", inner);
        inner.AddTag(tag);

        Assert.Equal(TagAccessMode.RO, inner.SearchAccessMode());
        Assert.Equal(TagAccessMode.RO, tag.SearchAccessMode());
    }

    /// <summary>
    /// 运行期缓存：同一 <see cref="TagUnion"/> 反复读取时，解析结果被缓存且值保持正确。
    /// （"访问模式是配置期属性"是缓存成立的前提，见 <see cref="TagUnion.CachedAccessMode"/>。）
    /// </summary>
    [Fact]
    public async Task ReadAsync_CachesInheritedAccessMode()
    {
        var grp = NewGroup("g", TagAccessMode.RO);
        var tag = NewTag("t", grp);
        grp.AddTag(tag);
        var union = grp["t"];

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);
        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(TagAccessMode.RO, union.CachedAccessMode);
    }
}
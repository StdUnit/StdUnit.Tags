using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrps;

/// <summary>
/// <see cref="ITagGrpExtensions.CollectChannels"/> 的测试。<br/>
/// 背景：单入口多通道场景下，<see cref="ITagGrpExtensions.SearchChannel(ITagGrp)"/> 只能解析出入口的<b>主通道</b>；
/// 入口子树中其它节点显式声明的通道必须由 <c>CollectChannels</c> 一并收集，
/// 否则运行器不会为它们建连，要到读/写该子树的测点时才失败。
/// </summary>
public class CollectChannelsTests
{
    private static FakedChannel CreateChannel(string name)
        => new FakedChannel(new TagChannelDescriptor { Name = name });

    private static TagGrp CreateGrp(string name, ITagChannel? channel = null, bool isEntry = false)
        => new TagGrp(new TagGrpDescriptor { Name = name, IsEntry = isEntry }, channel);


    [Fact]
    public void CollectChannels_WhenOnlyEntryChannel_ReturnsEntryChannel()
    {
        var ch1 = CreateChannel("S7-1");
        var entry = CreateGrp("entry", ch1, isEntry: true);

        var result = entry.CollectChannels();

        Assert.Single(result);
        Assert.Same(ch1, result[0]);
    }

    [Fact]
    public void CollectChannels_EntryWithChildGroups_CollectsAllChannelsInDocumentOrder()
    {
        // 复刻实际配置：入口用 S7-1，子组 grp2 用 ch-2
        var ch1 = CreateChannel("S7-1");
        var ch2 = CreateChannel("ch-2");

        var entry = CreateGrp("entry", ch1, isEntry: true);
        var statusGrp = CreateGrp("通用状态");         // 无自身通道 → 继承入口主通道
        var grp2 = CreateGrp("grp2", ch2);             // 自身通道 ch-2
        entry.AddTag(statusGrp);
        entry.AddTag(grp2);

        var result = entry.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    [Fact]
    public void CollectChannels_WhenSubtreeReusesSameChannel_ReturnsDeduplicatedList()
    {
        var ch1 = CreateChannel("S7-1");
        var entry = CreateGrp("entry", ch1, isEntry: true);
        // 两个子组都显式写了同一个通道
        entry.AddTag(CreateGrp("a", ch1));
        entry.AddTag(CreateGrp("b", ch1));

        var result = entry.CollectChannels();

        Assert.Single(result);
        Assert.Same(ch1, result[0]);
    }

    [Fact]
    public void CollectChannels_WhenNestedEntryDeclaresOwnChannel_StillCollectsIt()
    {
        // 嵌套的 isEntry="true" 不生效（ScanEntries 遇到最外层入口即停止下探，
        // 见 NestedEntryValidator）：该子树仍由本入口的 runner 轮询，所以它的通道也必须被收集。
        var ch1 = CreateChannel("S7-1");
        var ch3 = CreateChannel("ch-3");

        var entry = CreateGrp("entry", ch1, isEntry: true);
        var nestedEntry = CreateGrp("nestedEntry", ch3, isEntry: true);
        entry.AddTag(nestedEntry);

        var result = entry.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch3, result[1]);
    }

    [Fact]
    public void CollectChannels_WhenEntryChannelInheritedFromOuterGroup_IncludesInheritedChannel()
    {
        var ch1 = CreateChannel("S7-1");
        var ch2 = CreateChannel("ch-2");

        var outer = CreateGrp("root", ch1);
        var entry = CreateGrp("entry", channel: null, isEntry: true);
        outer.AddTag(entry);
        entry.AddTag(CreateGrp("grp2", ch2));

        var result = entry.CollectChannels();

        // 入口自身未声明通道 → 向上继承 outer 的通道，仍应被包含
        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    [Fact]
    public void CollectChannels_WhenTagHasOwnChannel_IncludesTagChannel()
    {
        var ch1 = CreateChannel("S7-1");
        var ch2 = CreateChannel("ch-2");

        var entry = CreateGrp("entry", ch1, isEntry: true);
        var tag = new FakedTag(new TagDescriptor { TagName = "t1", RawAddress = "0" }, ch2, entry);
        entry.AddTag(tag);

        var result = entry.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    [Fact]
    public void CollectChannels_WhenCbntHasOwnChannel_IncludesCbntChannel()
    {
        var ch1 = CreateChannel("S7-1");
        var ch2 = CreateChannel("ch-2");

        var entry = CreateGrp("entry", ch1, isEntry: true);
        var cbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "c1" })
        {
            Channel = ch2,
        };
        entry.AddTag(cbnt);

        var result = entry.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    [Fact]
    public void CollectChannels_OnNonEntryNode_CollectsItsOwnSubtree()
    {
        var ch1 = CreateChannel("ch-1");
        var ch2 = CreateChannel("ch-2");

        var grp = CreateGrp("grp", ch1);
        grp.AddTag(CreateGrp("child", ch2));

        var result = grp.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    [Fact]
    public void CollectChannels_WhenNoChannelAtAll_ReturnsEmpty()
    {
        var grp = CreateGrp("grp", channel: null);

        var result = grp.CollectChannels();

        Assert.Empty(result);
    }

    [Fact]
    public void CollectChannels_DeduplicatesByReference_NotByValueEquality()
    {
        // 通道代表一条物理连接：即使驱动重写了 Equals/GetHashCode，两个不同的通道实例
        // 也不应被误判为同一条连接（否则第二个通道永远不会被 runner 建连）。
        // 这正是 CollectChannels 按引用去重的原因。
        //
        // net8.0 用标准库 ReferenceEqualityComparer，net472 用 Compat.ReferenceEqualityComparerCompat polyfill，
        // 两者语义一致，因此本用例在两个目标框架下都应通过。
        var ch1 = new ValueEqualChannel(new TagChannelDescriptor { Name = "S7-1" });
        var ch2 = new ValueEqualChannel(new TagChannelDescriptor { Name = "S7-2" });
        Assert.Equal(ch1, ch2); // 前提：值相等性成立，但二者不是同一实例

        var entry = CreateGrp("entry", ch1, isEntry: true);
        entry.AddTag(CreateGrp("a", ch2));

        var result = entry.CollectChannels();

        Assert.Equal(2, result.Count);
        Assert.Same(ch1, result[0]);
        Assert.Same(ch2, result[1]);
    }

    /// <summary>
    /// 重写了 Equals/GetHashCode 的通道：任意两个实例在“值”上都相等，但不是同一条物理连接。
    /// </summary>
    private sealed class ValueEqualChannel : ITagChannel
    {
        public ValueEqualChannel(TagChannelDescriptor descriptor) => Descriptor = descriptor;

        public TagChannelDescriptor Descriptor { get; }

        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose() { }

        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;

        public override bool Equals(object? obj) => obj is ValueEqualChannel;

        public override int GetHashCode() => 0;
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagUnions;

/// <summary>
/// 遍历模式（<see cref="TraversalMode"/>）的使能门控语义测试。<br/>
/// 公开的 <c>ReadAsync(ct)</c> / <c>WriteAsync(ct)</c> 保持历史行为（不检查使能，可手动点动被禁用的测点）；
/// 自动轮询走的 <see cref="TraversalMode.RespectEnabled"/> 才按 <c>IsEnabled</c> 自顶向下短路。
/// </summary>
public class TagUnionTraversalModeTests
{
    private sealed class CountingTag : ITag
    {
        public CountingTag(string name) => TagDescriptor = new TagDescriptor { TagName = name };

        public TagDescriptor TagDescriptor { get; set; }
        public object? Value { get; set; }
        public DateTime Timestamp { get; set; }

        public event TagSyncEventHandler OnTagRead { add { } remove { } }
        public event TagSyncEventHandler OnTagWritten { add { } remove { } }

        public bool IsScanned { get; set; }
        public bool IsDirty { get; set; }
        public ITagChannel? Channel => null;
        public TagContainer? Parent { get; set; }

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task ReadAsync(CancellationToken ct)
        {
            ReadCount++;
            IsScanned = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(CancellationToken ct)
        {
            WriteCount++;
            IsDirty = false;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingCbnt : ITagCbnt
    {
        public CountingCbnt(string name, bool isEnabled)
        {
            Descriptor = new TagCbntDescriptor { Name = name, IsEnabled = isEnabled };
            IsEnabled = isEnabled;
        }

        public TagCbntDescriptor Descriptor { get; set; }
        public ITagGrp? Parent { get; set; }

        private readonly Dictionary<string, ITagCbntor> _children = new();
        public IDictionary<string, ITagCbntor> Children => _children;
        public ITagCbntor this[string tagName] => _children[tagName];

        public bool IsEnabled { get; set; }
        public bool IsScanned { get; set; }
        public ITagChannel? Channel { get; set; }
        public string StartAddress { get; set; } = string.Empty;
        public bool IsDirty { get; set; }

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task ReadAsync(CancellationToken ct)
        {
            ReadCount++;
            IsScanned = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(CancellationToken ct)
        {
            WriteCount++;
            IsDirty = false;
            return Task.CompletedTask;
        }
    }

    private static TagGrp NewGrp(string name, bool isEnabled = true)
        => new TagGrp(new TagGrpDescriptor { Name = name, IsEntry = false, IsEnabled = isEnabled }, null);

    private static Task PollReadAsync(ITagGrp grp, CancellationToken ct) =>
        grp.ReadAsync(TraversalMode.RespectEnabled, ct);

    private static Task PollWriteAsync(ITagGrp grp, CancellationToken ct) => 
        grp.WriteAsync(TraversalMode.RespectEnabled, ct);

    #region 手工调用：IgnoreEnabled

    [Fact]
    public async Task ManualReadAsync_IgnoresDisabledCbnt()
    {
        var root = NewGrp("root");
        var disabled = new CountingCbnt("disabled", isEnabled: false);
        root.AddTag(disabled);

        await root.ReadAsync(CancellationToken.None);

        Assert.Equal(1, disabled.ReadCount);
    }

    [Fact]
    public async Task ManualWriteAsync_IgnoresDisabledCbnt()
    {
        var root = NewGrp("root");
        var disabled = new CountingCbnt("disabled", isEnabled: false) { IsDirty = true };
        root.AddTag(disabled);

        await root.WriteAsync(CancellationToken.None);

        Assert.Equal(1, disabled.WriteCount);
        Assert.False(disabled.IsDirty);
    }

    [Fact]
    public async Task ManualReadAsync_IgnoresDisabledNestedGrp()
    {
        var root = NewGrp("root");
        var disabledGrp = NewGrp("disabledGrp", isEnabled: false);
        var cbnt = new CountingCbnt("c1", isEnabled: true);
        disabledGrp.AddTag(cbnt);
        root.AddTag(disabledGrp);

        await root.ReadAsync(CancellationToken.None);

        Assert.Equal(1, cbnt.ReadCount);
    }

    #endregion

    #region 自动轮询：RespectEnabled

    [Fact]
    public async Task PollReadAsync_SkipsDisabledCbnt_ButReadsEnabledSibling()
    {
        var root = NewGrp("root");
        var disabled = new CountingCbnt("disabled", isEnabled: false);
        var enabled = new CountingCbnt("enabled", isEnabled: true);
        root.AddTag(disabled);
        root.AddTag(enabled);

        await PollReadAsync(root, CancellationToken.None);

        Assert.Equal(0, disabled.ReadCount);
        Assert.Equal(1, enabled.ReadCount);
    }

    [Fact]
    public async Task PollWriteAsync_SkipsDirtyDisabledCbnt_ButWritesEnabledSibling()
    {
        var root = NewGrp("root");
        var disabled = new CountingCbnt("disabled", isEnabled: false) { IsDirty = true };
        var enabled = new CountingCbnt("enabled", isEnabled: true) { IsDirty = true };
        root.AddTag(disabled);
        root.AddTag(enabled);

        await PollWriteAsync(root, CancellationToken.None);

        Assert.Equal(0, disabled.WriteCount);
        Assert.True(disabled.IsDirty);   // 脏标记保留，重新使能后再刷写
        Assert.Equal(1, enabled.WriteCount);
        Assert.False(enabled.IsDirty);
    }

    [Fact]
    public async Task PollReadAsync_SkipsWholeSubtreeOfDisabledNestedGrp()
    {
        // A/B(disabled)/(C+D)：B 未使能时，其下的 C、D 都不应被采集
        var root = NewGrp("A");
        var b = NewGrp("B", isEnabled: false);
        var c = new CountingCbnt("C", isEnabled: true);
        var d = new CountingCbnt("D", isEnabled: true);
        b.AddTag(c);
        b.AddTag(d);
        root.AddTag(b);

        var sibling = new CountingCbnt("E", isEnabled: true);
        root.AddTag(sibling);

        await PollReadAsync(root, CancellationToken.None);

        Assert.Equal(0, c.ReadCount);
        Assert.Equal(0, d.ReadCount);
        Assert.Equal(1, sibling.ReadCount);
    }

    [Fact]
    public async Task PollReadAsync_DisabledTagGrpAsRoot_SkipsEverything()
    {
        var root = NewGrp("root", isEnabled: false);
        var cbnt = new CountingCbnt("c1", isEnabled: true);
        root.AddTag(cbnt);

        await PollReadAsync(root, CancellationToken.None);

        Assert.Equal(0, cbnt.ReadCount);
    }

    [Fact]
    public async Task PollReadAsync_TagHasNoEnabledFlag_FollowsItsGrp()
    {
        var root = NewGrp("root");
        var tag = new CountingTag("t1");
        root.AddTag(tag);

        await PollReadAsync(root, CancellationToken.None);

        Assert.Equal(1, tag.ReadCount);
    }

    [Fact]
    public async Task PollWriteAsync_TagHasNoEnabledFlag_FollowsItsGrp()
    {
        var root = NewGrp("root");
        var tag = new CountingTag("t1") { IsDirty = true };
        root.AddTag(tag);

        await PollWriteAsync(root, CancellationToken.None);

        Assert.Equal(1, tag.WriteCount);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task PollWriteAsync_DisabledGrp_SkipsDirtyTag()
    {
        var root = NewGrp("root", isEnabled: false);
        var tag = new CountingTag("t1") { IsDirty = true };
        root.AddTag(tag);

        await PollWriteAsync(root, CancellationToken.None);

        Assert.Equal(0, tag.WriteCount);
        Assert.True(tag.IsDirty);
    }

    [Fact]
    public async Task PollReadAsync_WhenAllEnabled_ReadsEverything()
    {
        var root = NewGrp("root");
        var grp = NewGrp("grp");
        var cbnt = new CountingCbnt("c1", isEnabled: true);
        var tag = new CountingTag("t1");
        grp.AddTag(cbnt);
        root.AddTag(grp);
        root.AddTag(tag);

        await PollReadAsync(root, CancellationToken.None);

        Assert.Equal(1, cbnt.ReadCount);
        Assert.Equal(1, tag.ReadCount);
    }

    #endregion
}

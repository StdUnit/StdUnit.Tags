using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagUnions;

/// <summary>
/// <see cref="TagUnionExtensions.ReadAsync(TagUnion, TraversalMode, CancellationToken)"/> /
/// <see cref="TagUnionExtensions.WriteAsync(TagUnion, TraversalMode, CancellationToken)"/> 的分支语义：<br/>
/// 访问模式（R1W / WO / RO）、"读后标记 <c>IsScanned</c>"（同步完成走快路径、真异步走续体）、以及封闭类型之外的兜底。
/// </summary>
public class TagUnionExtensions_ReadWrite_Tests
{
    private sealed class FakeTag : ITag
    {
        public FakeTag(TagAccessMode? access = null)
            => TagDescriptor = new TagDescriptor { TagName = "t", AccessMode = access };

        public TagDescriptor TagDescriptor { get; set; }
        public object? Value { get; set; }
        public DateTime Timestamp { get; set; }

        public event TagSyncEventHandler OnTagRead { add { } remove { } }
        public event TagSyncEventHandler OnTagWritten { add { } remove { } }

        public bool IsScanned { get; set; }
        public bool IsDirty { get; set; }
        public ITagChannel? Channel => null;
        public TagContainer? Parent { get; set; }

        /// <summary>非 null 时，<see cref="ReadAsync"/> 返回这个未完成的 Task（模拟设备 I/O）。</summary>
        public Task? PendingRead { get; set; }

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task ReadAsync(CancellationToken ct)
        {
            ReadCount++;
            return PendingRead ?? Task.CompletedTask;
        }

        public Task WriteAsync(CancellationToken ct)
        {
            WriteCount++;
            IsDirty = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCbnt : ITagCbnt
    {
        public FakeCbnt(TagAccessMode? access = null)
            => Descriptor = new TagCbntDescriptor { Name = "c", AccessMode = access };

        public TagCbntDescriptor Descriptor { get; set; }
        public ITagGrp? Parent { get; set; }

        private readonly Dictionary<string, ITagCbntor> _children = new();
        public IDictionary<string, ITagCbntor> Children => _children;
        public ITagCbntor this[string key] => _children[key];

        public bool IsEnabled { get; set; } = true;
        public bool IsScanned { get; set; }
        public ITagChannel? Channel { get; set; }
        public string StartAddress { get; set; } = string.Empty;
        public bool IsDirty { get; set; }

        /// <summary>非 null 时，<see cref="ReadAsync"/> 返回这个未完成的 Task（模拟设备 I/O）。</summary>
        public Task? PendingRead { get; set; }

        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task ReadAsync(CancellationToken ct)
        {
            ReadCount++;
            return PendingRead ?? Task.CompletedTask;
        }

        public Task WriteAsync(CancellationToken ct)
        {
            WriteCount++;
            IsDirty = false;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// <see cref="TagUnion"/> 是公开的抽象 record，第三方可以派生——兜底分支必须显式失败而不是静默跳过。
    /// </summary>
    private sealed record UnknownUnion : TagUnion
    {
        public override void Accept(ITagUnionVisitor visitor) => throw new NotSupportedException();
    }

    private static TaskCompletionSource<bool> PendingCompletion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    #region ReadAsync — 访问模式

    [Fact]
    public async Task ReadAsync_TagUnit_R1W_AlreadyScanned_DoesNotRead()
    {
        var tag = new FakeTag(TagAccessMode.R1W) { IsScanned = true };
        var union = new TagUnion.TagUnit(tag);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, tag.ReadCount);
        Assert.True(tag.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagUnit_R1W_NotScanned_ReadsAndMarksScanned()
    {
        var tag = new FakeTag(TagAccessMode.R1W);
        var union = new TagUnion.TagUnit(tag);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, tag.ReadCount);
        Assert.True(tag.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagUnit_WriteOnly_DoesNotRead()
    {
        var tag = new FakeTag(TagAccessMode.WO);
        var union = new TagUnion.TagUnit(tag);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, tag.ReadCount);
        Assert.False(tag.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagUnit_ReadOnly_Reads()
    {
        var tag = new FakeTag(TagAccessMode.RO);
        var union = new TagUnion.TagUnit(tag);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, tag.ReadCount);
    }

    [Fact]
    public async Task ReadAsync_TagUnit_AsyncRead_MarksScannedAfterCompletion()
    {
        var tag = new FakeTag();
        var tcs = PendingCompletion();
        tag.PendingRead = tcs.Task;
        var union = new TagUnion.TagUnit(tag);

        var readTask = union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.False(readTask.IsCompleted);
        Assert.False(tag.IsScanned);

        tcs.SetResult(true);
        await readTask;

        Assert.True(tag.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagCbnt_R1W_AlreadyScanned_DoesNotRead()
    {
        var cbnt = new FakeCbnt(TagAccessMode.R1W) { IsScanned = true };
        var union = new TagUnion.TagCbnt(cbnt);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, cbnt.ReadCount);
    }

    [Fact]
    public async Task ReadAsync_TagCbnt_R1W_NotScanned_ReadsAndMarksScanned()
    {
        var cbnt = new FakeCbnt(TagAccessMode.R1W);
        var union = new TagUnion.TagCbnt(cbnt);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, cbnt.ReadCount);
        Assert.True(cbnt.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagCbnt_WriteOnly_DoesNotRead()
    {
        var cbnt = new FakeCbnt(TagAccessMode.WO);
        var union = new TagUnion.TagCbnt(cbnt);

        await union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, cbnt.ReadCount);
    }

    [Fact]
    public async Task ReadAsync_TagCbnt_AsyncRead_MarksScannedAfterCompletion()
    {
        var cbnt = new FakeCbnt();
        var tcs = PendingCompletion();
        cbnt.PendingRead = tcs.Task;
        var union = new TagUnion.TagCbnt(cbnt);

        var readTask = union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.False(readTask.IsCompleted);
        Assert.False(cbnt.IsScanned);

        tcs.SetResult(true);
        await readTask;

        Assert.True(cbnt.IsScanned);
    }

    [Fact]
    public async Task ReadAsync_TagCbnt_RespectEnabled_WhenDisabled_DoesNotRead()
    {
        var cbnt = new FakeCbnt { IsEnabled = false };
        var union = new TagUnion.TagCbnt(cbnt);

        await union.ReadAsync(TraversalMode.RespectEnabled, CancellationToken.None);

        Assert.Equal(0, cbnt.ReadCount);
    }

    #endregion

    #region WriteAsync — 访问模式与脏标记

    [Fact]
    public async Task WriteAsync_TagUnit_ReadOnly_DoesNotWrite()
    {
        var tag = new FakeTag(TagAccessMode.RO) { IsDirty = true };
        var union = new TagUnion.TagUnit(tag);

        await union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, tag.WriteCount);
        Assert.True(tag.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_TagUnit_NotDirty_DoesNotWrite()
    {
        var tag = new FakeTag(TagAccessMode.RW);
        var union = new TagUnion.TagUnit(tag);

        await union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, tag.WriteCount);
    }

    [Fact]
    public async Task WriteAsync_TagUnit_Dirty_AndWritable_Writes()
    {
        var tag = new FakeTag(TagAccessMode.RW) { IsDirty = true };
        var union = new TagUnion.TagUnit(tag);

        await union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, tag.WriteCount);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_TagCbnt_ReadOnly_DoesNotWrite()
    {
        var cbnt = new FakeCbnt(TagAccessMode.RO) { IsDirty = true };
        var union = new TagUnion.TagCbnt(cbnt);

        await union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(0, cbnt.WriteCount);
    }

    [Fact]
    public async Task WriteAsync_TagCbnt_Dirty_AndWritable_Writes()
    {
        var cbnt = new FakeCbnt(TagAccessMode.RW) { IsDirty = true };
        var union = new TagUnion.TagCbnt(cbnt);

        await union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, cbnt.WriteCount);
        Assert.False(cbnt.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_TagCbnt_RespectEnabled_WhenDisabled_KeepsDirty()
    {
        var cbnt = new FakeCbnt { IsEnabled = false, IsDirty = true };
        var union = new TagUnion.TagCbnt(cbnt);

        await union.WriteAsync(TraversalMode.RespectEnabled, CancellationToken.None);

        Assert.Equal(0, cbnt.WriteCount);
        Assert.True(cbnt.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_TagGrp_DelegatesToGroup()
    {
        var tag = new FakeTag { IsDirty = true };
        var grp = new TagGrp(new TagGrpDescriptor { Name = "g", IsEntry = true }, null);
        grp.AddTag(tag);

        await new TagUnion.TagGrp(grp).WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None);

        Assert.Equal(1, tag.WriteCount);
    }

    #endregion

    #region 封闭类型之外的兜底

    [Fact]
    public async Task ReadAsync_UnknownUnion_ReturnsFaultedTask()
    {
        var union = new UnknownUnion();

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => union.ReadAsync(TraversalMode.IgnoreEnabled, CancellationToken.None));

        Assert.Contains("unknown types", ex.Message);
    }

    [Fact]
    public async Task WriteAsync_UnknownUnion_ReturnsFaultedTask()
    {
        var union = new UnknownUnion();

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => union.WriteAsync(TraversalMode.IgnoreEnabled, CancellationToken.None));

        Assert.Contains("unknown types", ex.Message);
    }

    [Fact]
    public void IsDirty_UnknownUnion_Throws()
    {
        var union = new UnknownUnion();

        var ex = Assert.Throws<NotImplementedException>(() => union.IsDirty());

        Assert.Contains("unknown types", ex.Message);
    }

    #endregion
}

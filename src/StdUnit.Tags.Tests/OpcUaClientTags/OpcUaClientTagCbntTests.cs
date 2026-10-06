using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.OpcUaClient.Cbnts;
using Opc.Ua;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;


public class OpcUaClientTagCbntTests
{
    [Fact]
    public void Constructor_SetsMetadata()
    {
        var descriptor = new TagCbntDescriptor
        {
            Name = "myCbnt",
            StartAddress = "ns=1",
            IsEnabled = true,
        };

        var cbnt = new OpcUaClientTagCbnt(descriptor);

        Assert.Equal("myCbnt", cbnt.TagName());
        Assert.Equal("ns=1", cbnt.StartAddress);
        Assert.True(cbnt.IsEnabled);
        Assert.Empty(cbnt.Children);
    }

    [Fact]
    public void Bag_Count_TracksItems()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });

        Assert.Empty(cbnt.Bag);

        cbnt.Bag.TryAdd(new Opc.Ua.NodeId("test", 1), new Opc.Ua.DataValue());
        Assert.Single(cbnt.Bag);
    }

    #region this[string tagName] 索引器

    [Fact]
    public void Indexer_WhenChildExists_ReturnsChild()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "myTag", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 };
        var child = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);
        cbnt.Children.Add("myTag", child);

        var result = cbnt["myTag"];

        Assert.Same(child, result);
    }

    [Fact]
    public void Indexer_WhenChildNotFound_Throws()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "myCbnt", StartAddress = "ns=1" });

        var ex = Assert.Throws<KeyNotFoundException>(() => cbnt["nonexistent"]);
        Assert.Contains("myCbnt", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    #endregion

    [Fact]
    public async Task ReadAsync_WhenChannelNotOpcUa_Throws()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = new FakeSimpleChannel(),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => cbnt.ReadAsync(CancellationToken.None));
        Assert.Contains(nameof(OpcUaClientTagChannel), ex.Message);
    }

    [Fact]
    public async Task WriteAsync_WhenChannelNotOpcUa_Throws()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = new FakeSimpleChannel(),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => cbnt.WriteAsync(CancellationToken.None));
        Assert.Contains(nameof(OpcUaClientTagChannel), ex.Message);
    }

    #region ReadAsync / WriteAsync happy path (使用 MockChannel)

    [Fact]
    public async Task ReadAsync_PopulatesBagAndNotifiesChildren()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 };
        var d2 = new TagDescriptor { TagName = "t2", RawAddress = "ns=1;s=Var2", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var child1 = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        var child2 = new OpcUaClientTagCbntor(d2, cbnt, 0, 0);
        cbnt.Children.Add("t1", child1);
        cbnt.Children.Add("t2", child2);

        channel.ReadAsyncOverride = (nodeIds, ct) =>
        {
            var values = new DataValueCollection { new DataValue(1.23f), new DataValue { Value = 42 } };
            var errs = new List<ServiceResult> { null!, null! };
            return Task.FromResult((values, (IList<ServiceResult>)errs));
        };

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Equal(2, cbnt.Bag.Count);
        Assert.Equal(1.23f, cbnt.Bag[child1.NodeId].Value);
        Assert.Equal(42, cbnt.Bag[child2.NodeId].Value);
    }

    [Fact]
    public async Task ReadAsync_WithSingleChild_Works()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 };
        var child1 = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        cbnt.Children.Add("t1", child1);

        channel.ReadAsyncOverride = (_, _) =>
            Task.FromResult<(DataValueCollection, IList<ServiceResult>)>(
                (new DataValueCollection { new DataValue(3.14f) }, new List<ServiceResult> { null! }));

        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Single(cbnt.Bag);
        Assert.Equal(3.14f, cbnt.Bag[child1.NodeId].Value);
    }

    [Fact]
    public async Task WriteAsync_WritesDirtyChildrenAndClearsFlags()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.FLOAT, TagSize = 4 };
        var child1 = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        child1.Value = 1.23f;  // 触发脏标记
        cbnt.Children.Add("t1", child1);

        IDictionary<NodeId, DataValue>? written = null;
        channel.WriteAsyncOverride = (dict, _) =>
        {
            written = dict;
            return Task.CompletedTask;
        };

        Assert.True(child1.IsDirty);
        await cbnt.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal(child1.NodeId, written.Keys.First());
        // 写入后清除脏标记
        Assert.False(child1.IsDirty);
        Assert.False(cbnt.IsDirty);
    }

    [Fact]
    public async Task WriteAsync_WhenNoDirtyChildren_CallsChannelWithEmptyDict()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        IDictionary<NodeId, DataValue>? written = null;
        channel.WriteAsyncOverride = (dict, _) =>
        {
            written = dict;
            return Task.CompletedTask;
        };

        await cbnt.WriteAsync(CancellationToken.None);

        // WriteAsync 始终调用 channel.WriteAsync，无脏数据时传入空字典
        Assert.NotNull(written);
        Assert.Empty(written);
    }

    #endregion

    /// <summary>
    /// 通道把"节点状态为 Bad"判为读取失败时，cbnt 不应改动缓存、也不应发通知——
    /// 异常原样向上传播给 runner 的重试/崩溃处理。
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenChannelThrows_KeepsBagAndNotificationsUntouched()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var child = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        cbnt.Children.Add("t1", child);
        var eventFired = false;
        child.OnTagRead += (_, _) => eventFired = true;

        channel.ReadAsyncOverride = (_, _) => throw new InvalidOperationException("读取节点失败：节点=ns=1;s=Var1 状态码=0x80340000(BadNodeIdUnknown)");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => cbnt.ReadAsync(CancellationToken.None));

        Assert.Contains("BadNodeIdUnknown", ex.Message);
        Assert.Empty(cbnt.Bag);
        Assert.False(eventFired);
    }

    [Fact]
    public async Task ReadAsync_WhenResultCountMismatches_ThrowsWithContext()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        cbnt.Children.Add("t1", new OpcUaClientTagCbntor(d1, cbnt, 0, 0));
        channel.ReadAsyncOverride = (_, _) => Task.FromResult<(DataValueCollection, IList<ServiceResult>)>((
            new DataValueCollection(),
            new List<ServiceResult>()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => cbnt.ReadAsync(CancellationToken.None));

        Assert.Contains("mock", ex.Message);
        Assert.Contains("c", ex.Message);
    }

    /// <summary>
    /// "子测点 ↔ NodeId"映射是缓存的（轮询高频路径只构建一次）；若加载后又有子节点增删，
    /// 必须按数量校验重建，而不是沿用旧映射（漏读 / 下标错位）。
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenChildrenChangedAfterFirstRead_RebuildsNodeMap()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var d2 = new TagDescriptor { TagName = "t2", RawAddress = "ns=1;s=Var2", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var child1 = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        var child2 = new OpcUaClientTagCbntor(d2, cbnt, 0, 0);
        cbnt.Children.Add("t1", child1);

        channel.ReadAsyncOverride = (nodeIds, _) =>
        {
            var values = new DataValueCollection();
            var errs = new List<ServiceResult>();
            for (int i = 0; i < nodeIds.Count; i++)
            {
                values.Add(new DataValue { Value = 1 });
                errs.Add(null!);
            }            return Task.FromResult<(DataValueCollection, IList<ServiceResult>)>((values, errs));
        };

        await cbnt.ReadAsync(CancellationToken.None);
        Assert.Single(cbnt.Bag);

        // 映射缓存已建立后再加一个子测点
        cbnt.Children.Add("t2", child2);
        await cbnt.ReadAsync(CancellationToken.None);

        Assert.Equal(2, cbnt.Bag.Count);
        Assert.True(cbnt.Bag.ContainsKey(child1.NodeId));
        Assert.True(cbnt.Bag.ContainsKey(child2.NodeId));
    }

    /// <summary>
    /// 两个子测点指向同一个 NodeId（别名）是合法配置：写入时只写一次，不再抛"已添加相同键"。
    /// </summary>
    [Fact]
    public async Task WriteAsync_WithAliasedNodeId_WritesOnceWithoutThrowing()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "a", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var d2 = new TagDescriptor { TagName = "b", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var a = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        var b = new OpcUaClientTagCbntor(d2, cbnt, 0, 0);
        cbnt.Children.Add("a", a);
        cbnt.Children.Add("b", b);
        Assert.Equal(a.NodeId, b.NodeId);

        a.Value = 1;
        b.Value = 2;

        IDictionary<NodeId, DataValue>? written = null;
        channel.WriteAsyncOverride = (dict, _) =>
        {
            written = dict;
            return Task.CompletedTask;
        };

        await cbnt.WriteAsync(CancellationToken.None);

        Assert.NotNull(written);
        Assert.Single(written);
        Assert.False(a.IsDirty);
        Assert.False(b.IsDirty);
    }

    /// <summary>
    /// 脏但缓存里没值（只有绕开 <c>Value</c> setter 直接置 <c>IsDirty = true</c> 才会）：
    /// 必须抛带通道/测点/节点上下文的错，而不是静默跳过（否则"标记了要写却什么都没写"无人知晓）。
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenDirtyButNoCachedValue_ThrowsWithContext()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var d1 = new TagDescriptor { TagName = "t1", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var child = new OpcUaClientTagCbntor(d1, cbnt, 0, 0);
        cbnt.Children.Add("t1", child);
        child.IsDirty = true;   // 绕开 Value setter

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => cbnt.WriteAsync(CancellationToken.None));

        Assert.Contains("mock", ex.Message);
        Assert.Contains("t1", ex.Message);
        Assert.Contains(child.NodeId.ToString(), ex.Message);
    }

    private class FakeSimpleChannel : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "Fake",
            Driver = "Fake",
        };
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }
}

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

        var ex = Assert.Throws<Exception>(() => cbnt["nonexistent"]);
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

        var ex = await Assert.ThrowsAsync<Exception>(() => cbnt.ReadAsync(CancellationToken.None));
        Assert.Contains(nameof(OpcUaClientTagChannel), ex.Message);
    }

    [Fact]
    public async Task WriteAsync_WhenChannelNotOpcUa_Throws()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = new FakeSimpleChannel(),
        };

        var ex = await Assert.ThrowsAsync<Exception>(() => cbnt.WriteAsync(CancellationToken.None));
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

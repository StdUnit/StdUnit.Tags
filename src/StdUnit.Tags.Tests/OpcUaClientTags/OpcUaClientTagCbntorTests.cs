using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient.Cbnts;
using Opc.Ua;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

/// <summary>
/// 测试 <see cref="OpcUaClientTagCbntor"/> 的 Value get/set、构造函数校验、
/// ReadAsync/WriteAsync 异常路径。
/// </summary>
public class OpcUaClientTagCbntorTests
{
    #region c'tor tests
    [Fact]
    public void Constructor_WhenCbntIsOpcUaClientTagCbnt_Succeeds()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=tag1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        Assert.Equal("ns=1;s=tag1", tag.NodeId.ToString());
    }

    [Fact]
    public void Constructor_WhenCbntIsNotOpcUaClientTagCbnt_Throws()
    {
        var regularCbnt = new TestByteTagCbnt(new TagCbntDescriptor { Name = "regular", StartAddress = "0" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=tag1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new OpcUaClientTagCbntor(descriptor, regularCbnt, 0, 0));
        Assert.Contains("OpcUaTagCbnt", ex.Message);
    }

    [Fact]
    public void Constructor_RawAddressIsUsedAsNodeId()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=MyVar", TagKind = BuiltinTagKinds.BIT, TagSize = 1 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        Assert.Equal("ns=1;s=MyVar", tag.RawAddress());
        Assert.Equal("ns=1;s=MyVar", tag.NormalizedAddress());
        Assert.Equal("ns=1;s=MyVar", tag.NodeId.ToString());
    }
    #endregion

    #region .Value getter
    [Fact]
    public void Value_WhenNotInBag_ReturnsNull()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=absent", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        var v = tag.Value;
        Assert.Null(v);
    }

    [Fact]
    public void Value_Set_AddsToBag()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        tag.Value = 42;
        Assert.True(cbnt.Bag.ContainsKey(tag.NodeId));
    }

    [Fact]
    public void Value_SetAndGet_Roundtrips()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=var2", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        tag.Value = 99;
        Assert.Equal(99, tag.Value);
    }

    [Fact]
    public void Value_Set_MarksTagDirty()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=var3", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        Assert.False(tag.IsDirty);
        tag.Value = 42;
        Assert.True(tag.IsDirty);
    }

    [Fact]
    public void Value_UpdatesExistingBagEntry()
    {
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" });
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=var4", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        tag.Value = 10;
        tag.Value = 20;
        Assert.Equal(20, tag.Value);
    }

    #endregion

    #region ReadAsync / WriteAsync — 通道类型不对，应该抛出异常
    [Fact]
    public async Task ReadAsync_WhenChannelNotOpcUa_Throws()
    {
        var fakeChannel = new FakeChannel();
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = fakeChannel,
        };
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=tag1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tag.ReadAsync(CancellationToken.None));
        Assert.Contains("OpcUaTagChannel", ex.Message);
    }

    [Fact]
    public async Task WriteAsync_WhenChannelNotOpcUa_Throws()
    {
        var fakeChannel = new FakeChannel();
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = fakeChannel,
        };
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=tag1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);
        tag.Value = 42;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tag.WriteAsync(CancellationToken.None));
        Assert.Contains("OpcUaTagChannel", ex.Message);
    }
    #endregion

    /// <summary>
    /// 一个简单的 FakeChannel，仅用于触发异常路径
    /// </summary>
    private class FakeChannel : ITagChannel
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

    #region ReadAsync / WriteAsync happy path (使用 MockOpcUaChannel)

    [Fact]
    public async Task ReadAsync_WithMockChannel_FiresOnTagReadAndUpdatesTimestamp()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        channel.ReadAsyncOverride = async (_, _) =>
        {
            return (
                new DataValueCollection {
                    new DataValue { Value = 99 }
                },
                new List<ServiceResult> { null! }
            );
        };

        var eventFired = false;
        tag.OnTagRead += (_, _) => eventFired = true;

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(eventFired);
        Assert.Equal(99, tag.Value);
        Assert.NotEqual(default, tag.Timestamp);
    }

    [Fact]
    public async Task ReadAsync_WithMockChannel_PopulatesBag()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);

        channel.ReadAsyncOverride = async (_, _) =>
        {
            return (
                new DataValueCollection { new DataValue { Value = 42 } },
                new List<ServiceResult> { null! }
            );
        };

        await tag.ReadAsync(CancellationToken.None);

        Assert.True(cbnt.Bag.ContainsKey(tag.NodeId));
        Assert.Equal(42, cbnt.Bag[tag.NodeId].Value);
    }

    [Fact]
    public async Task WriteAsync_WithMockChannel_FiresOnTagWrittenAndClearsDirty()
    {
        var channel = new MockOpcUaChannel("mock");
        var cbnt = new OpcUaClientTagCbnt(new TagCbntDescriptor { Name = "c", StartAddress = "ns=1" })
        {
            Channel = channel,
        };
        var descriptor = new TagDescriptor { TagName = "t", RawAddress = "ns=1;s=Var1", TagKind = BuiltinTagKinds.INT32, TagSize = 4 };
        var tag = new OpcUaClientTagCbntor(descriptor, cbnt, 0, 0);
        tag.Value = 123;  // 触发脏标记

        IDictionary<NodeId, DataValue>? written = null;
        channel.WriteAsyncOverride = (dict, _) =>
        {
            written = dict;
            return Task.CompletedTask;
        };
        var eventFired = false;
        tag.OnTagWritten += (_, _) => eventFired = true;

        Assert.True(tag.IsDirty);
        await tag.WriteAsync(CancellationToken.None);

        Assert.True(eventFired);
        Assert.False(tag.IsDirty);
        Assert.NotNull(written);
        Assert.Single(written);
        Assert.Equal(tag.NodeId, written.Keys.First());
        Assert.Equal(123, written.Values.First().Value);
    }

    #endregion
}

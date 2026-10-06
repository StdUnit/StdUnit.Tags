using StdUnit.Tags.OpcUaClient;
using StdUnit.Tags.OpcUaClient.DirectTags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

public class OpcUaClientDirectTagTests
{
    private readonly ServiceProvider _root;

    public OpcUaClientDirectTagTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddOpcUaClientSupport();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildProject_WithDirectTagsUnderTagGrp_Works()
    {
        using var scope = this._root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();

var dir = TestPaths.Fixture("OpcUaClientTags", "DirectTags");

        using var proj = factory.Create(dir);

        Assert.Single(proj.Channels);
        Assert.IsType<OpcUaClientTagChannel>(proj.Channels[0]);

        var grp = proj.Tags.SelectGrp("g-direct");
        Assert.NotNull(grp);

        var v3 = grp.SelectTag("var3");
        Assert.NotNull(v3);
        Assert.Equal("var3", v3.TagName());
        Assert.Equal("ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var3", v3.NormalizedAddress());
        Assert.IsType<OpcUaClientDirectTag>(v3);

        var v4 = grp.SelectTag("var4");
        Assert.NotNull(v4);
        Assert.Equal("var4", v4.TagName());
        Assert.Equal("ns=4;s=|var|CODESYS Control Win V3 x64.Application.PLC_PRG.var4", v4.NormalizedAddress());
        Assert.IsType<OpcUaClientDirectTag>(v4);
    }

    [Fact]
    public async Task DirectTag_ReadAsync_UsesFakeChannelValue()
    {
        var channel = new FakeOpcUaClientTagChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "v1",
            RawAddress = "ns=4;s=Demo.Var1",
        };

        channel.SetReadValue("ns=4;s=Demo.Var1", new DataValue { Value = 123 });
        var tag = new OpcUaClientDirectTag(descriptor, channel, container);

        await tag.ReadAsync(CancellationToken.None);

        Assert.Equal(123, tag.Value);
        Assert.False(tag.IsDirty);
    }

    [Fact]
    public async Task DirectTag_ReadAsync_WhenValueIsBad_KeepsPreviousValue()
    {
        var channel = new FakeOpcUaClientTagChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "v1",
            RawAddress = "ns=4;s=Demo.Var1",
        };

        channel.SetReadValue("ns=4;s=Demo.Var1", new DataValue { Value = 123 });
        var tag = new OpcUaClientDirectTag(descriptor, channel, container);
        await tag.ReadAsync(CancellationToken.None);
        var goodTimestamp = tag.Timestamp;

        var eventFired = false;
        tag.OnTagRead += (_, _) => eventFired = true;
        channel.FailRead = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => tag.ReadAsync(CancellationToken.None));

        // 读取失败（Bad → 通道抛异常）时，值/通知/时间戳都不应被改动
        Assert.Equal(123, tag.Value);
        Assert.False(eventFired);
        Assert.Equal(goodTimestamp, tag.Timestamp);
    }

    [Fact]
    public async Task DirectTag_WriteAsync_WritesFakeChannelValue()
    {
        var channel = new FakeOpcUaClientTagChannel();
        var grp = new TagGrp(new TagGrpDescriptor { Name = "grp", IsEntry = true }, channel);
        var container = TagContainer.From(grp);
        var descriptor = new TagDescriptor
        {
            TagName = "v2",
            RawAddress = "ns=4;s=Demo.Var2",
        };

        var tag = new OpcUaClientDirectTag(descriptor, channel, container)
        {
            Value = "abc"
        };

        await tag.WriteAsync(CancellationToken.None);

        Assert.True(channel.TryGetLastWritten("ns=4;s=Demo.Var2", out var written));
        Assert.NotNull(written);
        Assert.Equal("abc", written!.Value);
        Assert.False(tag.IsDirty);
    }

    private sealed class FakeOpcUaClientTagChannel : OpcUaClientTagChannel
    {
        public FakeOpcUaClientTagChannel()
            : base(
                new OpcUaClientTagChannelDescriptor()
                {
                    Name = "fake-opcua",
                    OpcUaTagChannelOpt = new OpcUaClientTagChannelOpt
                    {
                        ClientName = "fake-client",
                        ServerOpt = new OpcUaServerOpt
                        {
                            DiscoveryUrl = "opc.tcp://localhost:4840"
                        }
                    }
                },
                new LoggerFactory().CreateLogger<OpcUaClientTagChannel>()
            )
        {
        }

        private readonly Dictionary<string, DataValue> _reads = new();
        private readonly Dictionary<string, DataValue> _writes = new();

        /// <summary>置为 true 时读取直接失败，用于验证"读取失败不改动测点状态"。</summary>
        public bool FailRead { get; set; }

        public void SetReadValue(string nodeId, DataValue value)
        {
            _reads[nodeId] = value;
        }

        public bool TryGetLastWritten(string nodeId, out DataValue? value)
        {
            if (_writes.TryGetValue(nodeId, out var got))
            {
                value = got;
                return true;
            }

            value = null;
            return false;
        }

        public override Task<DataValue> ReadValueAsync(NodeId nodeId, CancellationToken ct)
        {
            if (FailRead)
            {
                throw new InvalidOperationException($"通道(fake-opcua)读取节点值失败：节点={nodeId} 状态码=0x80340000(BadNodeIdUnknown)");
            }
            if (!_reads.TryGetValue(nodeId.ToString(), out var value))
            {
                throw new InvalidOperationException($"No fake read value configured for NodeId={nodeId}");
            }

            return Task.FromResult(value);
        }

        public override Task WriteValueAsync(NodeId nodeId, DataValue value, CancellationToken ct)
        {
            _writes[nodeId.ToString()] = value;
            return Task.CompletedTask;
        }
    }
}

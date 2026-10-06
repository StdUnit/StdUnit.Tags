using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.OpcUaClient.Cbnts;

internal class OpcUaClientTagCbntor : TagCbntor
{
    private OpcUaClientTagCbnt _cbnt;

    /// <summary>
    /// 节点ID
    /// </summary>
    public NodeId NodeId { get; }

    /// <summary>
    /// c'tor
    /// </summary>
    /// <exception cref="TagsProjectConfigurationException">所属测点组合不是 <see cref="OpcUaClientTagCbnt"/></exception>
    public OpcUaClientTagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
        : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
    {
        this._cbnt = this.TagCbnt as OpcUaClientTagCbnt
            ?? throw new TagsProjectConfigurationException(
                $"测点组合 '{tagCbnt.TagName()}' 必须是 {nameof(OpcUaClientTagCbnt)}，实际为 {tagCbnt.GetType().Name}",
                $"Tag({tagDescriptor.TagName})");
        this.NodeId = tagDescriptor.RawAddress;
    }

    /// <inheritdoc/>
    public override object? Value
    {
        get
        {
            if (!this._cbnt.Bag.TryGetValue(this.NodeId, out var nodeVal))
            {
                return null;
            }
            return nodeVal.Value;
        }
        set
        {
            this._cbnt.Bag.AddOrUpdate(this.NodeId, new DataValue() { Value = value }, (nid, v) =>
            {
                v.Value = value;
                return v;
            });
            this.MarkDirty();
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(CancellationToken ct)
    {
        var channel = this.TagCbnt.SearchRequiredChannel();
        var opcUaChannel = channel as OpcUaClientTagChannel
            ?? throw new InvalidOperationException("Channel is not an OpcUaTagChannel");
        var cbnt = this.TagCbnt as OpcUaClientTagCbnt
            ?? throw new InvalidOperationException("Cbnt is not an OpcUaTagCbnt");
        if (!cbnt.Bag.TryGetValue(this.NodeId, out var nodeValue))
        {
            // 同 OpcUaClientTagCbnt.WriteAsync：脏但没有值属于"标记了要写、实际什么都没写"的静默失效，必须报出来
            throw new InvalidOperationException(
                $"通道({opcUaChannel.ChannelName()}) 写入失败：测点({this.TagName()}) 被标记为脏，但缓存里没有它的值（NodeId={this.NodeId}），无法写入");
        }
        var tobeWritten = new Dictionary<NodeId, DataValue>
        {
            { this.NodeId, nodeValue }
        };
        await opcUaChannel.WriteAsync(tobeWritten, ct);
        this.NotifyTagWritten();
        this.IsDirty = false;
    }

    /// <inheritdoc />
    public override async Task ReadAsync(CancellationToken ct)
    {
        var channel = this.TagCbnt.SearchRequiredChannel();
        var opcUaChannel = channel as OpcUaClientTagChannel
            ?? throw new InvalidOperationException("Channel is not an OpcUaTagChannel");
        var cbnt = this.TagCbnt as OpcUaClientTagCbnt
            ?? throw new InvalidOperationException("Cbnt is not an OpcUaTagCbnt");
        var (values, errs) = await opcUaChannel.ReadAsync([this.NodeId], ct);

        // 通道保证"返回即非坏值"（坏点会让这次读取直接失败）
        cbnt.Bag[this.NodeId] = values[0];
        this.Timestamp = DateTime.Now;
        this.NotifyTagRead();
    }
}

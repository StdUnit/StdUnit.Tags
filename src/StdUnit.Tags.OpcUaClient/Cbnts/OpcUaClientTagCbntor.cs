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
    public OpcUaClientTagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
        : base(tagDescriptor, tagCbnt, tagOffset, cacheOffset)
    {
        this._cbnt = this.TagCbnt as OpcUaClientTagCbnt
            ?? throw new InvalidOperationException("Cbnt is not an OpcUaTagCbnt");
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
        var nodeValue = cbnt.Bag[this.NodeId];
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

        var value = values[0];
        cbnt.Bag[this.NodeId] = value;
        this.Timestamp = DateTime.Now;
        this.NotifyTagRead();
    }
}

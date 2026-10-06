using Opc.Ua;
using System.Collections.Concurrent;

namespace StdUnit.Tags.OpcUaClient.Cbnts;

internal class OpcUaClientTagCbnt : ITagCbnt
{
    internal OpcUaClientTagCbnt(TagCbntDescriptor descriptor)
    {
        Descriptor = descriptor;
        IsEnabled = descriptor.IsEnabled;
        StartAddress = descriptor.StartAddress;
    }

    /// <inheritdoc/>
    public TagCbntDescriptor Descriptor { get; set; }

    /// <inheritdoc/>
    public ITagGrp? Parent { get; set; }

    /// <inheritdoc/>
    public IDictionary<string, ITagCbntor> Children { get; } = new Dictionary<string, ITagCbntor>();
    /// <inheritdoc/>
    public ITagCbntor this[string tagName] => this.Children.TryGetValue(tagName, out var tag) ?
        tag :
        throw new Exception($"TagCbnt({this.TagName()}) has no child who's name={tagName}");

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }
    /// <inheritdoc/>
    public bool IsScanned { get; set; }
    /// <inheritdoc/>
    public ITagChannel? Channel { get; set; }
    /// <inheritdoc/>
    public string StartAddress { get; set; }

    /// <inheritdoc/>
    public bool IsDirty { get; set; }

    /// <summary>
    /// OpcUA 节点值集合
    /// </summary>
    public ConcurrentDictionary<NodeId, DataValue> Bag { get; } = new ConcurrentDictionary<NodeId, DataValue>();

    private Dictionary<string, NodeId> _nodeIdCache { get; } = new Dictionary<string, NodeId>();

    private NodeId GetNodeIdByTagName(OpcUaClientTagCbntor cbntor)
    {
        var childTagName = cbntor.TagName();
        if (_nodeIdCache.TryGetValue(childTagName, out var nodeId))
        {
            return nodeId;
        }

        // 缓存中没有，则从子标签获取
        nodeId = cbntor.NodeId;
        _nodeIdCache[childTagName] = nodeId;
        return nodeId;
    }

    /// <inheritdoc/>
    public async Task ReadAsync(CancellationToken ct)
    {
        var channel = this.SearchChannel() as OpcUaClientTagChannel;
        if (channel is null)
        {
            throw new Exception($"TagCbnt({this.TagName()}) 通道应为{nameof(OpcUaClientTagChannel)},实际为{channel?.GetType()}");
        }
        var nodeIds = this.Children
            .Select(child =>
            {
                var cbntor = child.Value as OpcUaClientTagCbntor;
                if (cbntor is null)
                {
                    throw new Exception($"TagCbnt({this.TagName()}) 下的子标签({child.Key}) 应为{nameof(OpcUaClientTagCbntor)},实际为{child.Value.GetType()}");
                }
                return this.GetNodeIdByTagName(cbntor);
            })
            .ToList();
        var (values, errs) = await channel.ReadAsync(nodeIds!, ct);

        for (int i = 0; i < nodeIds.Count; i++)
        {
            var nodeId = nodeIds[i];
            var err = errs[i];
            //todo: 检查错误

            var value = values[i];
            this.Bag[nodeId] = value;
            this.Bag.AddOrUpdate(nodeId, value, (k, v) => value);
        }

        foreach (var kv in this.Children)
        {
            var tag = kv.Value;
            tag.NotifyTagRead();
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(CancellationToken ct)
    {
        var channel = this.SearchChannel() as OpcUaClientTagChannel;
        if (channel is null)
        {
            throw new Exception($"TagCbnt({this.TagName()}) 通道应为{nameof(OpcUaClientTagChannel)},实际为{channel?.GetType()}");
        }
        var toBeWritten = this.Children
            .Where(c => c.Value.IsDirty)
            .Select(child =>
            {
                var cbntor = child.Value as OpcUaClientTagCbntor;
                if (cbntor is null)
                {
                    throw new Exception($"TagCbnt({this.TagName()}) 下的子标签({child.Key}) 应为{nameof(OpcUaClientTagCbntor)},实际为{child.Value.GetType()}");
                }
                var nodeId = this.GetNodeIdByTagName(cbntor);
                return new KeyValuePair<NodeId, DataValue>(
                    nodeId,
                    this.Bag[nodeId]
                );
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        await channel.WriteAsync(toBeWritten, ct);

        foreach (var kv in this.Children)
        {
            var tag = kv.Value;
            tag.NotifyTagWritten();
            tag.IsDirty = false;
        }
        this.IsDirty = false;
    }
}

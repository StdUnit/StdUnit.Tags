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
    /// <exception cref="KeyNotFoundException">指定的子测点名不存在</exception>
    public ITagCbntor this[string tagName] => this.Children.TryGetValue(tagName, out var tag) ?
        tag :
        throw new KeyNotFoundException($"TagCbnt({this.TagName()}) has no child who's name={tagName}");

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

    /// <summary>
    /// "子测点 ↔ NodeId"映射缓存，两个数组下标一一对应。<br/>
    /// 读取/写入每轮都要这份映射（且要按子节点顺序与返回结果对齐），而轮询是高频路径，故只构建一次：
    /// 原来每轮都要走一遍 LINQ、按测点名查一次字典、再分配两个 List。<br/>
    /// 子节点集合在加载完成后不再变化——与 S7/Modbus 一致（它们在构建期就把偏移量烘进了字节缓存）。
    /// 这里按数量做一次校验，万一有人在加载后增删子节点，会重建映射而不是静默错位。
    /// </summary>
    private OpcUaClientTagCbntor[]? _mapChildren;
    private NodeId[]? _mapNodeIds;

    private (OpcUaClientTagCbntor[] Children, NodeId[] NodeIds) GetNodeMap()
    {
        var cachedChildren = _mapChildren;
        var cachedNodeIds = _mapNodeIds;
        if (cachedChildren is not null && cachedNodeIds is not null && cachedChildren.Length == this.Children.Count)
        {
            return (cachedChildren, cachedNodeIds);
        }

        var children = new OpcUaClientTagCbntor[this.Children.Count];
        var nodeIds = new NodeId[this.Children.Count];
        var i = 0;
        foreach (var kv in this.Children)
        {
            var cbntor = kv.Value as OpcUaClientTagCbntor;
            if (cbntor is null)
            {
                throw new InvalidOperationException($"TagCbnt({this.TagName()}) 下的子标签({kv.Key}) 应为{nameof(OpcUaClientTagCbntor)},实际为{kv.Value.GetType()}");
            }
            children[i] = cbntor;
            nodeIds[i] = cbntor.NodeId;
            i++;
        }

        _mapChildren = children;
        _mapNodeIds = nodeIds;
        return (children, nodeIds);
    }

    /// <inheritdoc/>
    public async Task ReadAsync(CancellationToken ct)
    {
        var channel = this.SearchChannel() as OpcUaClientTagChannel;
        if (channel is null)
        {
            throw new InvalidOperationException($"TagCbnt({this.TagName()}) 通道应为{nameof(OpcUaClientTagChannel)},实际为{channel?.GetType()}");
        }

        var (children, nodeIds) = this.GetNodeMap();
        var (values, errs) = await channel.ReadAsync(nodeIds, ct);
        if (values.Count != nodeIds.Length)
        {
            throw new InvalidOperationException($"通道({channel.ChannelName()}) 读取结果数量与请求不一致：请求={nodeIds.Length}，返回={values.Count}（TagCbnt={this.TagName()}）");
        }

        // 通道保证"返回即非坏值"（坏点会让这次读取直接失败，见 OpcUaClientTagChannel.ReadAsync），
        // 所以这里不判断质量：先写完整个缓存再通知——处理器里读同组其它测点时，不应看到半更新的缓存。
        for (int i = 0; i < nodeIds.Length; i++)
        {
            this.Bag[nodeIds[i]] = values[i];
        }

        this.NotifyChildrenRead();
    }

    /// <summary>
    /// 通知所有子测点已被整体读取（缓存已全部刷新）。与 <c>TagCbnt.NotifyChildrenRead()</c> 语义一致。
    /// </summary>
    private void NotifyChildrenRead()
    {
        foreach (var kv in this.Children)
        {
            kv.Value.NotifyTagRead();
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(CancellationToken ct)
    {
        var channel = this.SearchChannel() as OpcUaClientTagChannel;
        if (channel is null)
        {
            throw new InvalidOperationException($"TagCbnt({this.TagName()}) 通道应为{nameof(OpcUaClientTagChannel)},实际为{channel?.GetType()}");
        }
        var (children, nodeIds) = this.GetNodeMap();
        var toBeWritten = new Dictionary<NodeId, DataValue>();
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (!child.IsDirty)
            {
                continue;
            }

            var nodeId = nodeIds[i];
            if (!this.Bag.TryGetValue(nodeId, out var value))
            {
                // 脏但没有值：正常路径不会出现（Value setter 先写 Bag 再 MarkDirty），
                // 只有绕开 setter 直接置 IsDirty = true 才会。必须报出来而不是跳过——
                // 跳过会变成"标记了要写、实际什么都没写"的静默失效。
                throw new InvalidOperationException(
                    $"通道({channel.ChannelName()}) 写入失败：测点({child.TagName()}) 被标记为脏，但缓存里没有它的值（NodeId={nodeId}），无法写入");
            }

            toBeWritten[nodeId] = value;
        }

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

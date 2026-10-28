using System.Globalization;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 一个入口的值收集器：保存上一次的值用于 diff，并能给出当前全量值（订阅基线与重同步）。<br/>
/// <b>所有成员线程安全</b>：diff 在轮询线程上跑，全量构建在发送循环/请求线程上跑。
/// </summary>
internal sealed class EntryValuesCollector
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<LeafTag> _leaves;
    private readonly IReadOnlyList<ContainerNode> _containers;
    private readonly Dictionary<ITag, Previous> _previousLeafValues = new(ReferenceEqualityComparer.Instance);
    /// <summary>上一次推送的容器使能标志，键是容器身份（<see cref="ContainerNode.Identity"/>）。</summary>
    private readonly Dictionary<object, bool> _prevContainerIsEnabledFlags = new(ReferenceEqualityComparer.Instance);

    public EntryValuesCollector(IReadOnlyList<LeafTag> leaves, IReadOnlyList<ContainerNode> containers)
    {
        this._leaves = leaves;
        this._containers = containers;

        // 预先记下"构建时刻"的值：这样第一轮只报告真正变化过的点，
        // 而不是把一整套默认值当成"全都变了"再推一遍。
        foreach (var leaf in leaves)
        {
            var tag = leaf.Tag;
            this._previousLeafValues[tag] = new Previous(tag.Value, tag.IsDirty, tag.IsScanned);
        }
        foreach (var container in containers)
        {
            this._prevContainerIsEnabledFlags[container.Identity] = container.IsEnabled;
        }
    }

    /// <summary>
    /// 计算自上次调用以来的变化。<b>只在轮询线程上调用</b>；无变化时返回 <c>null</c>。<br/>
    /// O(该入口子树的节点数)：叶测点一趟、容器一趟，都是只读扫描，<b>只有真正变化的元素</b>才会生成 DTO。
    /// </summary>
    public EntryDelta? Diff()
    {
        lock (this._gate)
        {
            var values = new List<TagValueDto>();
            foreach (var leaf in this._leaves)
            {
                var tag = leaf.Tag;
                var value = tag.Value;
                var isDirty = tag.IsDirty;
                var isScanned = tag.IsScanned;

                if (this._previousLeafValues.TryGetValue(tag, out var previous)
                    && Equals(previous.Value, value)
                    && previous.IsDirty == isDirty
                    && previous.IsScanned == isScanned)
                {
                    continue;
                }

                this._previousLeafValues[tag] = new Previous(value, isDirty, isScanned);
                values.Add(CreateValueDto(leaf));
            }

            var containerFlags = new List<ContainerFlagDto>();
            foreach (var container in this._containers)
            {
                var isEnabled = container.IsEnabled;
                if (this._prevContainerIsEnabledFlags.TryGetValue(container.Identity, out var previous) && previous == isEnabled)
                {
                    continue;
                }

                this._prevContainerIsEnabledFlags[container.Identity] = isEnabled;
                containerFlags.Add(new ContainerFlagDto(container.Path, isEnabled));
            }

            return values.Count == 0 && containerFlags.Count == 0
                ? null
                : new EntryDelta(values, containerFlags);
        }
    }

    /// <summary>
    /// 该入口当前的<b>全部</b>值（订阅基线 / 重同步）。<b>不能在轮询线程上调用</b>（O(n) 且要分配字符串）。
    /// </summary>
    public List<TagValueDto> GetAll()
    {
        lock (this._gate)
        {
            var values = new List<TagValueDto>(this._leaves.Count);
            foreach (var leaf in this._leaves)
            {
                values.Add(CreateValueDto(leaf));
            }
            return values;
        }
    }

    private static TagValueDto CreateValueDto(LeafTag leaf)
    {
        var tag = leaf.Tag;
        var (value, timestamp) = ReadStable(tag);
        return new TagValueDto(
            leaf.Path,
            tag.TagKind(),
            Convert.ToString(value, CultureInfo.InvariantCulture),
            timestamp,
            tag.IsDirty,
            tag.IsScanned);
    }

    /// <summary>
    /// 读一对 (值, 时间戳)。<see cref="ITag.Timestamp"/> 是 8 字节结构，32 位宿主上可能有撕裂读，
    /// 所以读两次时间戳，不一致就重试（最多几次），仍不一致则接受最后一次的结果。
    /// </summary>
    private static (object? Value, DateTime Timestamp) ReadStable(ITag tag)
    {
        for (var i = 0; i < 3; i++)
        {
            var before = tag.Timestamp;
            var value = tag.Value;
            var after = tag.Timestamp;
            if (before == after)
            {
                return (value, after);
            }
        }

        return (tag.Value, tag.Timestamp);
    }

    private readonly struct Previous
    {
        public Previous(object? value, bool isDirty, bool isScanned)
        {
            this.Value = value;
            this.IsDirty = isDirty;
            this.IsScanned = isScanned;
        }

        public object? Value { get; }

        public bool IsDirty { get; }

        public bool IsScanned { get; }
    }
}



/// <summary>
/// 一次轮次里发生变化的内容。<br/>
/// 两个列表都可能为空（表示该维度没有变化），但都<b>不为 null</b>——调用方用 <c>Count</c> 判断。
/// </summary>
internal sealed class EntryDelta
{
    public EntryDelta(List<TagValueDto> values, List<ContainerFlagDto> containerFlags)
    {
        this.Values = values;
        this.ContainerFlags = containerFlags;
    }

    public List<TagValueDto> Values { get; }

    public List<ContainerFlagDto> ContainerFlags { get; }
}

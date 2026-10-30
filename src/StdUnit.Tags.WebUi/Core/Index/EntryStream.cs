namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 一个入口的观测视图：叶子测点集合、容器集合、值收集器。
/// </summary>
internal sealed class EntryStream
{
    private long _seq;

    public EntryStream(string key, string path, ITagGrp entry, IReadOnlyList<LeafTag> leaves, IReadOnlyList<ContainerNode> containers)
    {
        this.Key = key;
        this.Path = path;
        this.Entry = entry;
        this.Collector = new EntryValuesCollector(leaves, containers);
    }

    /// <summary>
    /// 入口键，等于 <c>entry.TagName()</c>。<br/>
    /// <c>?entries=</c> 与 <c>values.entry</c> 用的就是它。
    /// </summary>
    public string Key { get; }

    /// <summary>入口在树中的路径。</summary>
    public string Path { get; }

    public ITagGrp Entry { get; }

    public EntryValuesCollector Collector { get; }

    /// <summary>
    /// 该入口内单调递增的帧序号（每次项目启动从 0 开始）。
    /// </summary>
    public long IncrementSeq() => Interlocked.Increment(ref this._seq);
}

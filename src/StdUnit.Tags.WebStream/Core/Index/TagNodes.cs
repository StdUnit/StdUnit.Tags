namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 一个叶测点（<see cref="ITag"/>）及其在树中的路径。
/// </summary>
internal readonly struct LeafTag
{
    public LeafTag(string path, ITag tag)
    {
        this.Path = path;
        this.Tag = tag;
    }

    public string Path { get; }

    public ITag Tag { get; }
}


/// <summary>
/// 一个容器（<see cref="ITagGrp"/> 或 <see cref="ITagCbnt"/>）及其在树中的路径。
/// </summary>
internal readonly struct ContainerNode
{
    public ContainerNode(string path, ITagGrp? grp, ITagCbnt? cbnt)
    {
        this.Path = path;
        this.Grp = grp;
        this.Cbnt = cbnt;
    }

    public string Path { get; }

    public ITagGrp? Grp { get; }

    public ITagCbnt? Cbnt { get; }

    /// <summary>用于字典键（引用身份，避免依赖实现是否重写 Equals）。</summary>
    public object Identity => (object?)this.Grp ?? this.Cbnt!;

    public bool IsEnabled => this.Grp?.IsEnabled ?? this.Cbnt!.IsEnabled;
}

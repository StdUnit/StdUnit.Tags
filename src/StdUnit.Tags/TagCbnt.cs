namespace StdUnit.Tags;

/// <summary>
/// 代表一组测点组合（强类型泛型基类）。<br/>
/// 本类不假设底层通道类型，具体强类型由子类扩展<br/>
/// <br/>
/// 本类定义为 internal，仅开放给 S7、ModbusTcp 等由我自己内置实现的Tier1模块(意味着不必担心滥用问题)。<br/>
/// <br/>
/// 外部开发者绝不应该依赖这个类，而是要提供自己的实现——听起来有点麻烦，不过不必担心：
/// 因为除了读写通道，其他属性的定义和方法都极其简单而且直观。<br/>
/// </summary>
/// <typeparam name="T">缓存元素类型，必须是非托管类型（byte / ushort 等）</typeparam>
internal abstract class TagCbnt<T> : ITagCbnt where T : unmanaged
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="descriptor"></param>
    internal TagCbnt(TagCbntDescriptor descriptor)
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
    public ITagChannel? Channel { get; set; }

    /// <inheritdoc/>
    public string StartAddress { get; set; }

    /// <inheritdoc/>
    public int CacheSize { get; set; }

    /// <summary>
    /// 底层硬件相对应的缓存（强类型视图）。<br/>
    /// 元素语义：T=byte 时是原始字节；T=ushort 时是寄存器值数组。
    /// </summary>
    public Memory<T> Cache { get; protected set; } = Memory<T>.Empty;

    /// <summary>
    /// 调整缓存大小（单位为字节，与 <see cref="CacheSize"/> 一致）。<br/>
    /// 内部按元素大小折算成元素个数分配。
    /// </summary>
    /// <param name="cacheSize">字节数</param>
    public void ResizeCache(int cacheSize)
    {
        this.CacheSize = cacheSize;
        var elemCount = cacheSize / System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var cache = new T[elemCount];

        var len = Math.Min(elemCount, this.Cache.Length);
        if (len > 0)
        {
            this.Cache.Span.Slice(0, len).CopyTo(cache);
        }
        this.Cache = cache;
    }

    /// <inheritdoc/>
    public virtual bool IsDirty { get; set; }

    #region 子节点
    /// <inheritdoc/>
    public IDictionary<string, ITagCbntor> Children { get; } = new Dictionary<string, ITagCbntor>();

    /// <inheritdoc/>
    public ITagCbntor this[string tagName] => this.Children.TryGetValue(tagName, out var tag) ?
        tag :
        throw new Exception($"TagCbnt({this.TagName()}) has no child who's name={tagName}");
    #endregion

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }

    /// <inheritdoc/>
    public bool IsScaned { get; set; }

    /// <inheritdoc/>
    public abstract Task ReadAsync(CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task WriteAsync(CancellationToken ct);

    /// <summary>
    /// 通知所有子测点已被整体读取（数据已刷新到 Cache）。
    /// </summary>
    protected void NotifyChildrenRead()
    {
        foreach (var kv in this.Children)
        {
            kv.Value.NotifyTagRead();
        }
    }

    /// <summary>
    /// 通知所有子测点已被整体写入，并清除本组合与子测点的脏标记。
    /// </summary>
    protected void NotifyChildrenWritten()
    {
        foreach (var kv in this.Children)
        {
            kv.Value.NotifyTagWritten();
            kv.Value.IsDirty = false;
        }
        this.IsDirty = false;
    }
}

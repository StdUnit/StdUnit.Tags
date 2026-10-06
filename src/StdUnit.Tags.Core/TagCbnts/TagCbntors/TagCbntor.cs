namespace StdUnit.Tags;


/// <summary>
/// 测点组合子基类。<br/>
/// <br/>
/// 本基类只承载<b>与缓存形态无关</b>的公共部分（描述符、偏移、脏标记、事件通知、读写抽象）。<br/>
/// 缓存的具体形态与解读方式由各驱动的具体 Cbntor 决定：<br/>
/// - 字节缓存驱动（S7、Modbus 位空间）在构造时强类型绑定 <c>TagCbnt&lt;byte&gt;</c>；<br/>
/// - 寄存器缓存驱动（Modbus 字空间）在构造时强类型绑定 <c>TagCbnt&lt;ushort&gt;</c>；<br/>
/// - 无缓存驱动（OpcUa 按 NodeId 直读直写）直接继承本类，自行实现读写。<br/>
/// </summary>
public abstract class TagCbntor : ITagCbntor
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt"></param>
    /// <param name="tagOffset"></param>
    /// <param name="cacheOffset"></param>
    public TagCbntor(TagDescriptor tagDescriptor, ITagCbnt tagCbnt, int tagOffset, int cacheOffset)
    {
        TagDescriptor = tagDescriptor;
        TagCbnt = tagCbnt;
        TagOffset = tagOffset;
        CacheOffset = cacheOffset;
    }

    /// <summary>
    /// 测点描述
    /// </summary>
    public TagDescriptor TagDescriptor { get; set; } = null!;


    /// <summary>
    /// 所属的测点组合
    /// </summary>
    public ITagCbnt TagCbnt { get; set; } = null!;

    /// <summary>
    /// 通道
    /// </summary>
    public ITagChannel? Channel => TagCbnt.SearchChannel();

    /// <summary>
    /// 父容器，指向所属的测点组合。<br/>
    /// </summary>
    public TagContainer? Parent { get; set; }

    /// <summary>
    /// 测点数据在组合内的定位偏移，供驱动特定的 Cbntor 从组合缓存中定位本测点。<br/>
    /// <b>单位由组合缓存元素类型决定，由各驱动的具体 Cbntor 自行解释</b>：<br/>
    /// - S7、Modbus 位空间（缓存元素 byte）：单位为 byte；<br/>
    /// - Modbus 字空间（缓存元素 ushort）：单位为寄存器索引（= TagOffset / 2）。<br/>
    /// 大多数时候与 <see cref="TagOffset"/> 相同；当地址含位地址且跨字节/寄存器时可能不一致（如 S7 跨字节位会导致 CacheOffset 比 TagOffset 大 1）。
    /// </summary>
    public int CacheOffset { get; set; }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public virtual int TagOffset { get; set; }

    /// <inheritdoc />
    public abstract object? Value { get; set; }

    /// <inheritdoc />
    public virtual DateTime Timestamp { get; set; }

    /// <inheritdoc />
    public event TagSyncEventHandler? OnTagRead;

    /// <inheritdoc />
    public event TagSyncEventHandler? OnTagWritten;

    /// <summary>
    /// 通知值已经更新，这个方法不在乎值是否一样
    /// </summary>
    public virtual void NotifyTagRead()
    {
        if (this.OnTagRead != null)
        {
            var eArgs = new TagSyncEventArgs(this.Value, this.Timestamp, TagSyncEventArgs.Kinds.Read);
            this.OnTagRead(this, eArgs);
        }
    }

    /// <inheritdoc/>
    public void NotifyTagWritten()
    {
        if (this.OnTagWritten != null)
        {
            var eArgs = new TagSyncEventArgs(this.Value, this.Timestamp, TagSyncEventArgs.Kinds.Written);
            this.OnTagWritten(this, eArgs);
        }
    }

    /// <inheritdoc/>
    public bool IsScanned { get; set; }

    #region 
    /// <inheritdoc />
    public virtual bool IsDirty { get; set; }


    /// <summary>
    /// 标记已脏
    /// </summary>
    protected virtual void MarkDirty()
    {
        this.IsDirty = true;
        this.TagCbnt.IsDirty = true;
    }
    #endregion

    /// <summary>
    /// 把当前测点值刷到底层。<br/>
    /// 本基类<b>不假设任何缓存形态</b>（不同驱动的缓存语义不同：S7/Modbus位空间为字节、Modbus字空间为寄存器数组、OpcUa 无缓存）。<br/>
    /// 具体驱动必须重写本方法，按自家驱动的缓存类型（强类型绑定 <c>TagCbnt&lt;T&gt;</c>）实现。<br/>
    /// 注意：如果某个驱动没有缓存（如 OpcUa 按 NodeId 直读直写），同样必须重写（可抛 <see cref="NotSupportedException"/>）。
    /// </summary>
    /// <returns></returns>
    public abstract Task WriteAsync(CancellationToken ct);

    /// <summary>
    /// 从底层读取数据到当前测点值。<br/>
    /// 本基类<b>不假设任何缓存形态</b>（不同驱动的缓存语义不同：S7/Modbus位空间为字节、Modbus字空间为寄存器数组、OpcUa 无缓存）。<br/>
    /// 具体驱动必须重写本方法，按自家驱动的缓存类型（强类型绑定 <c>TagCbnt&lt;T&gt;</c>）实现。<br/>
    /// 注意：如果某个驱动没有缓存（如 OpcUa 按 NodeId 直读直写），同样必须重写（可抛 <see cref="NotSupportedException"/>）。
    /// </summary>
    /// <returns></returns>
    public abstract Task ReadAsync(CancellationToken ct);

}

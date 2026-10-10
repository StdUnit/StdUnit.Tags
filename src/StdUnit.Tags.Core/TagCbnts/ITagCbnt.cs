namespace StdUnit.Tags;

/// <summary>
/// 代表一组支持批量读或者批量写的测点组合。
/// 测点组内的这些测点共享同一段底层缓存，同属一种读写访问方式。
/// 在自动轮询时，这些测点也有相同的采样间隔。
/// </summary>
public interface ITagCbnt
{
    /// <summary>
    /// 对应的描述符，可用于获取完整的静态配置信息
    /// </summary>
    TagCbntDescriptor Descriptor { get; set; }

    /// <summary>
    /// 父组合
    /// </summary>
    ITagGrp? Parent { get; set; }

    /// <summary>
    /// 子测点集合
    /// </summary>
    IDictionary<string, ITagCbntor> Children { get; }

    /// <summary>
    /// 子测点getter。如果指定的测点名不存在，则抛出异常
    /// </summary>
    /// <param name="tagName"></param>
    /// <returns></returns>
    public ITagCbntor this[string tagName] { get; }

    /// <summary>
    /// 在自动轮询模式下，是否使能？<br/>
    /// 本属性是短路语义，即自身未使能时，其整棵子树都不再被考虑。
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 是否被扫描过
    /// </summary>
    bool IsScanned { get; set; }


    #region 底层硬件相关
    /// <summary>
    /// 测点通道
    /// </summary>
    ITagChannel? Channel { get; set; }

    /// <summary>
    /// 起始地址。<br/>
    /// 构建时从 <see cref="Descriptor"/> 拷贝初始化，但之后可独立变更，不污染描述符。
    /// </summary>
    string StartAddress { get; set; }


    /// <summary>
    /// 从底层读取数据到缓存
    /// </summary>
    /// <returns></returns>
    abstract Task ReadAsync(CancellationToken ct);

    /// <summary>
    /// 刷写缓存数据到底层
    /// </summary>
    /// <returns></returns>
    abstract Task WriteAsync(CancellationToken ct);
    #endregion



    /// <summary>
    /// 表明是否有改动
    /// </summary>
    bool IsDirty { get; set; }
}

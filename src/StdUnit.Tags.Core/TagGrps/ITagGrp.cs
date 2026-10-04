

namespace StdUnit.Tags;


/// <summary>
/// 代表一组测点群组。群组内的各个测点是松散的，可能共享通信通道，也可能不共享通信通道。<br/>
/// 由于这种性质，群组中的测点不会被统一读，也不会被统一写，它们的读或写往往意味着多次IO交互。<br/>
/// </summary>
public interface ITagGrp
{
    /// <summary>
    /// 父群组
    /// </summary>
    ITagGrp? Parent { get; set; }

    /// <summary>
    /// 对应的描述符，可用于获取完整的静态配置信息
    /// </summary>
    TagGrpDescriptor Descriptor { get; set; }

    #region Child
    /// <summary>
    /// 子测点集合
    /// </summary>
    IDictionary<string, TagUnion> Children { get; }

    /// <summary>
    /// 直接子测点getter。如果指定的测点名不存在，则抛出异常
    /// </summary>
    /// <param name="tagName"></param>
    /// <returns></returns>
    TagUnion this[string tagName] { get; }

    /// <summary>
    /// 以路径获取子节点并作为<see cref="TagUnion"/>返回。<br/>
    /// 如果路径不存在，会抛出异常。
    /// </summary>
    /// <param name="path">以“/”分隔</param>
    /// <returns></returns>
    TagUnion Descendant(string path);

    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    ITagGrp AddTag(ITag tag);

    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tagCbnt"></param>
    /// <returns></returns>
    ITagGrp AddTag(ITagCbnt tagCbnt);

    /// <summary>
    /// 增加测点
    /// </summary>
    /// <param name="tagGrp"></param>
    /// <returns></returns>
    ITagGrp AddTag(ITagGrp tagGrp);
    #endregion


    /// <summary>
    /// 是否使能？
    /// </summary>
    public bool IsEnabled { get; set; }



    #region 底层硬件相关
    /// <summary>
    /// 测点通道
    /// </summary>
    public ITagChannel? Channel { get; set; }

    /// <summary>
    /// 从底层读取数据到缓存
    /// </summary>
    /// <returns></returns>
    public abstract Task ReadAsync(CancellationToken ct);

    /// <summary>
    /// 刷写缓存数据到底层
    /// </summary>
    /// <returns></returns>
    public abstract Task WriteAsync(CancellationToken ct);


    /// <summary>
    /// 是否有脏数据
    /// </summary>
    /// <returns></returns>
    bool IsDirty();
    #endregion
}

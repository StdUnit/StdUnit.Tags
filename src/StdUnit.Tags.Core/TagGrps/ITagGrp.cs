

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
    /// 在自动轮询模式下，是否使能？<br/>
    /// 本属性是短路语义，即自身未使能时，其整棵子树都不再被考虑。
    /// </summary>
    public bool IsEnabled { get; set; }



    #region 底层硬件相关
    /// <summary>
    /// 测点通道
    /// </summary>
    public ITagChannel? Channel { get; set; }

    /// <summary>
    /// 以指定的遍历模式读取子树。<br/>
    /// <see cref="TraversalMode.RespectEnabled"/> 时应用使能门控，且是<b>自顶向下短路</b>的：
    /// 自身未使能则整棵子树都不再被考虑，既不读取也不刷写（脏标记保留，重新使能后再刷写）；
    /// 父节点被判为跳过时，子节点根本不会被访问。<br/>
    /// <see cref="TraversalMode.IgnoreEnabled"/> 时不检查使能，即历史行为（少传参数可用扩展方法 <c>ReadAsync(ct)</c>）。<br/>
    /// 实现方必须把 <paramref name="mode"/> 透传给子节点，否则门控会在该层断掉。
    /// </summary>
    /// <param name="mode">遍历模式</param>
    /// <param name="ct"></param>
    public abstract Task ReadAsync(TraversalMode mode, CancellationToken ct);

    /// <summary>
    /// 以指定的遍历模式刷写子树。语义见 <see cref="ReadAsync(TraversalMode, CancellationToken)"/>。
    /// </summary>
    /// <param name="mode">遍历模式</param>
    /// <param name="ct"></param>
    public abstract Task WriteAsync(TraversalMode mode, CancellationToken ct);


    /// <summary>
    /// 是否有脏数据
    /// </summary>
    /// <returns></returns>
    bool IsDirty();
    #endregion
}

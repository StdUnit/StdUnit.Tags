namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="TagContainer"/>
/// </summary>
public static class TagContainerExtensions
{
    /// <summary>
    /// (冒泡式)获取访问模式（<b>逐级冒泡</b>：容器自身 → 各级祖先 → 默认 <see cref="TagAccessMode.RW"/>）。<br/>
    /// 与 <see cref="SearchChannel"/> 同族：由容器节点继续向上追溯，而不是只看直接父级。
    /// </summary>
    /// <param name="tagContainer"></param>
    /// <returns></returns>
    public static TagAccessMode SearchAccessMode(this TagContainer tagContainer) => tagContainer switch
    {
        TagContainer.TagCbnt cbnt => cbnt.Value.SearchAccessMode(),
        TagContainer.TagGrp grp => grp.Value.SearchAccessMode(),
        _ => throw new NotImplementedException($"未预料到的{nameof(TagContainer)}子类型: {tagContainer.GetType()}")
    };

    /// <summary>
    /// (冒泡式)获取 <see cref="ITagChannel"/>。<br/>
    /// 如果没有找到，返回 null。<br/>
    /// </summary>
    /// <param name="tagContainer"></param>
    /// <returns></returns>
    public static ITagChannel? SearchChannel(this TagContainer tagContainer) => tagContainer.Map(
        handleTagCbnt: cbnt => cbnt.SearchChannel(),
        handleTagGrp: grp => grp.SearchChannel()
    );

    /// <summary>
    /// (冒泡式)获取 <see cref="ITagChannel"/>。<br/>
    /// 如果没有找到，则抛出异常<br/>
    /// </summary>
    /// <param name="tagContainer"></param>
    /// <returns></returns>
    public static ITagChannel SearchRequiredChannel(this TagContainer tagContainer) => tagContainer.Map(
        handleTagCbnt: cbnt => cbnt.SearchRequiredChannel(),
        handleTagGrp: grp => grp.SearchRequiredChannel()
    );



    /// <summary>
    /// 转成 <see cref="TagContainer"/>
    /// </summary>
    /// <param name="cbnt"></param>
    /// <returns></returns>
    public static TagContainer IntoTagContainer(this ITagCbnt cbnt) => TagContainer.From(cbnt);

    /// <summary>
    /// 转成 <see cref="TagContainer"/>
    /// </summary>
    /// <param name="grp"></param>
    /// <returns></returns>
    public static TagContainer IntoTagContainer(this ITagGrp grp) => TagContainer.From(grp);
}




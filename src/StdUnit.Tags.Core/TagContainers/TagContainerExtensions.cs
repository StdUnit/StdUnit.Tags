namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="TagContainer"/>
/// </summary>
public static class TagContainerExtensions
{
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




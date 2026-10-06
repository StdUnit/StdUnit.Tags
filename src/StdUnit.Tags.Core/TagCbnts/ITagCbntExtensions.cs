namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="ITagCbnt"/>
/// </summary>
public static class ITagCbntExtensions
{
    /// <summary>
    /// 获取测点组合的名称
    /// </summary>
    public static string TagName(this ITagCbnt tagcbnt) => tagcbnt.Descriptor.Name;

    /// <summary>
    /// 获取子测点。如果指定的测点名不存在，则抛出异常
    /// </summary>
    /// <param name="tagcbnt"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static ITagCbntor SelectTag(this ITagCbnt tagcbnt, string path) => tagcbnt[path];

    /// <summary>
    /// 冒泡式获取测点的通道
    /// </summary>
    /// <param name="tagcbnt"></param>
    /// <returns></returns>
    public static ITagChannel? SearchChannel(this ITagCbnt tagcbnt) => tagcbnt.Channel ?? tagcbnt.Parent?.SearchChannel();

    /// <summary>
    /// 冒泡式获取测点的通道。如果没有配置通道，则抛出异常
    /// </summary>
    /// <param name="tagcbnt"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">自身与祖先都没有配置通道</exception>
    public static ITagChannel SearchRequiredChannel(this ITagCbnt tagcbnt) => tagcbnt.SearchChannel() ?? throw new InvalidOperationException($"Channel is not configured : TagCbnt({tagcbnt.TagName()})");

    /// <summary>
    /// 获取解析后的访问模式。<br/>
    /// 如果自身和父级均未配置，则默认返回 <see cref="TagAccessMode.RW"/>。
    /// </summary>
    /// <param name="tagcbnt"></param>
    /// <returns></returns>
    public static TagAccessMode SearchAccessMode(this ITagCbnt tagcbnt)
        => tagcbnt.Descriptor.AccessMode ?? tagcbnt.Parent?.Descriptor.AccessMode ?? TagAccessMode.RW;

    /// <summary>
    /// 只读？
    /// </summary>
    /// <param name="tagcbnt"></param>
    /// <returns></returns>
    public static bool IsReadOnly(this ITagCbnt tagcbnt) => tagcbnt.SearchAccessMode() == TagAccessMode.RO;
    /// <summary>
    /// 只写？
    /// </summary>
    public static bool IsWriteOnly(this ITagCbnt tagcbnt) => tagcbnt.SearchAccessMode() == TagAccessMode.WO;
}
namespace StdUnit.Tags;

/// <summary>
/// 测点扩展方法
/// </summary>
public static class ITagExtensions
{
    /// <summary>
    /// 测点名称
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static string TagName(this ITag tag) => tag.TagDescriptor.TagName;

    /// <summary>
    /// 规范化后的测点地址
    /// </summary>
    public static TagAddress NormalizedAddress(this ITag tag) => tag.TagDescriptor.NormalizedAddress;

    /// <summary>
    /// 配置的原始测点地址
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static TagAddress RawAddress(this ITag tag) => tag.TagDescriptor.RawAddress;

    /// <summary>
    /// 所占据的字节多少
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static int TagSize(this ITag tag) => tag.TagDescriptor.TagSize;

    /// <summary>
    /// 测点种类
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static TagKinds TagKind(this ITag tag) => tag.TagDescriptor.TagKind;

    /// <summary>
    /// 大小端
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static EndianKinds TagEndian(this ITag tag) => tag.TagDescriptor.EndianKind;

    /// <summary>
    /// 访问模式（可能为 null，表示未配置）。
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static TagAccessMode? AccessMode(this ITag tag) => tag.TagDescriptor.AccessMode;

    /// <summary>
    /// 冒泡式获取解析后的访问模式。<br/>
    /// 先查自身 <see cref="TagDescriptor.AccessMode"/>，再查父级 Cbnt/Grp。<br/>
    /// 如果所有层级均未配置，默认返回 <see cref="TagAccessMode.RW"/>。
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static TagAccessMode SearchAccessMode(this ITag tag)
    {
        if (tag.TagDescriptor.AccessMode.HasValue)
            return tag.TagDescriptor.AccessMode.Value;

        if (tag.Parent is not null)
        {
            return tag.Parent.Map(
                cbnt => cbnt.Descriptor.AccessMode,
                grp => grp.Descriptor.AccessMode
            ) ?? TagAccessMode.RW;
        }

        return TagAccessMode.RW;
    }

    /// <summary>
    /// 只读？
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static bool IsReadOnly(this ITag tag) => tag.SearchAccessMode() == TagAccessMode.RO;

    /// <summary>
    /// 只写？
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static bool IsWriteOnly(this ITag tag) => tag.SearchAccessMode() == TagAccessMode.WO;

    /// <summary>
    /// 把当前测点转成具体类型
    /// </summary>
    /// <typeparam name="TTag"></typeparam>
    /// <param name="tag"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static TTag AsTag<TTag>(this ITag tag) where TTag : ITag
    {
        if (tag is not TTag t)
        {
            throw new Exception($"{tag.GetType()} is not {typeof(TTag)}");
        }
        return t;
    }


    /// <summary>
    /// 获取当前测点的值
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static TValue? GetTagValue<TValue>(this ITag tag)
    {
        TValue value = (TValue)tag.Value!;
        return value;
    }

    /// <summary>
    /// (冒泡式)获取测点通道。<br/>
    /// 先查自身，再冒泡查父容器（<see cref="ITagCbnt"/> 或 <see cref="ITagGrp"/>）。<br/>
    /// 如果没有找到，返回 null。
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static ITagChannel? SearchChannel(this ITag tag) =>
        tag.Channel ??
        tag.Parent?.SearchChannel();

    /// <summary>
    /// (冒泡式)获取测点通道。<br/>
    /// 如果没有找到，则抛出异常。<br/>
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ITagChannel SearchRequiredChannel(this ITag tag) =>
        tag.SearchChannel() ??
        throw new Exception($"相关测点未配置通道 : Tag({tag.TagName()})");

    /// <summary>
    /// 向上冒泡检索入口
    /// </summary>
    /// <param name="tag"></param>
    /// <returns>null代表未找到入口</returns>
    public static ITagGrp? SearchEntry(this ITag tag)
    {
        var container = tag.Parent;
        return container?.Map(
            cbnt => cbnt.Parent is null ? null : GetEntryForGrp(cbnt.Parent),
            grp => GetEntryForGrp(grp)
            );

        ITagGrp? GetEntryForGrp(ITagGrp grp)
        {
            if (grp.IsEntry())
            {
                return grp;
            }
            if (grp.Parent is null)
            {
                return null;
            }
            return GetEntryForGrp(grp.Parent);
        }
    }


}
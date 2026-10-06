namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan 测点组合构建器的扩展方法。
/// </summary>
public static class TagCbntBuilderExtensions
{
    /// <summary>
    /// 创建 ZLan 测点工厂（把针脚名解析为组合内的槽位偏移）。
    /// </summary>
    /// <param name="tagGroupBuilder">测点组合构建器</param>
    /// <returns>测点工厂</returns>
    public static ZLanTagFactory MakeZLanTagFactory(this ZLanCbntBuilderBase tagGroupBuilder)
    {
        var tagFactory = new ZLanTagFactory(tagGroupBuilder);
        return tagFactory;
    }
}
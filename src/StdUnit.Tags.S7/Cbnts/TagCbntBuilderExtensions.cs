namespace StdUnit.Tags.S7;

/// <summary>
/// extensions for <see cref="S7TagCbntBuilder"/>
/// </summary>
public static class TagCbntBuilderExtensions
{
    /// <summary>
    /// 创建 <see cref="S7TagFactory"/>
    /// </summary>
    /// <param name="tagGroupBuilder"></param>
    /// <returns></returns>
    internal static S7TagFactory MakeS7TagFactory(this S7TagCbntBuilder tagGroupBuilder)
    {
        var tagFactory = new S7TagFactory(tagGroupBuilder, tagGroupBuilder.TypedCbnt);
        return tagFactory;
    }



}
namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// extensions for <see cref="ModbusBitTagCbntBuilder"/> to create <see cref="ModbusBitTagFactory"/>
/// </summary>
public static class TagCbntBuilderExtensions
{
    /// <summary>
    /// 创建 <see cref="ModbusBitTagFactory"/> 实例
    /// </summary>
    /// <param name="tagGroupBuilder"></param>
    /// <returns></returns>
    public static ModbusBitTagFactory MakeModbusBitTagFactory(this ModbusBitTagCbntBuilder tagGroupBuilder)
    {
        var tagFactory = new ModbusBitTagFactory(tagGroupBuilder, tagGroupBuilder.TypedCbnt);
        return tagFactory;
    }
}
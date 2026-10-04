namespace StdUnit.Tags.ZLan;

public static class TagCbntBuilderExtensions
{
    public static ZLanTagFactory MakeZLanTagFactory(this ZLanCbntBuilderBase tagGroupBuilder)
    {
        var tagFactory = new ZLanTagFactory(tagGroupBuilder);
        return tagFactory;
    }
}
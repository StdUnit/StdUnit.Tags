namespace StdUnit.Tags.Hjzk;

/// <summary>
/// extension methods for HjzkCbntBuilderBase to create HjzkTagFactory instances.
/// </summary>
public static class TagCbntBuilderExtensions
{
    /// <summary>
    /// 构建一个 HjzkTagFactory 实例。
    /// </summary>
    /// <param name="tagGroupBuilder">The HjzkCbntBuilderBase instance.</param>
    /// <returns>A new HjzkTagFactory instance.</returns>
    public static HjzkTagFactory MakeHjzkTagFactory(this HjzkCbntBuilderBase tagGroupBuilder)
    {
        var tagFactory = new HjzkTagFactory(tagGroupBuilder);
        return tagFactory;
    }
}
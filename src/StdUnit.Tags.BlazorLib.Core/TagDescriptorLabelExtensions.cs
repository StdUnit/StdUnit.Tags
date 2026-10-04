namespace StdUnit.Tags.BlazorLib;

internal static class TagDescriptorLabelExtensions
{
    /// <summary>
    /// 使用 name 或 tagName 作为标签的显示名称
    /// </summary>
    /// <param name="d"></param>
    /// <returns></returns>
    public static string? GetLabel(this ITagsDescriptor? d)
    {
        return d is null ? null :
             d is TagGrpDescriptor grpDescriptor ? grpDescriptor.Name :
             d is TagCbntDescriptor cbntDescriptor ? cbntDescriptor.Name :
             d is TagDescriptor descriptor ? descriptor.TagName :
             d.ToString();
    }
}

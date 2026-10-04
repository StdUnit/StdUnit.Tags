namespace StdUnit.Tags.BlazorLib.Components.Tags.Editing;

/// <summary>
/// 一个组合的编辑器注册表，按优先级顺序排列
/// </summary>
internal class TagValueEditorRegistry
{
    private readonly List<ITagValueEditor> _editors;

    /// <summary>
    /// c'tor。
    /// 内部会按优先级顺序排列编辑器
    /// </summary>
    /// <param name="editors"></param>
    public TagValueEditorRegistry(IEnumerable<ITagValueEditor> editors)
    {
        _editors = editors?.OrderBy(e => e.Priority).ToList() ?? new();
    }

    /// <summary>
    /// 对于给定的<see cref="ITag"/>，返回第一个可以编辑它的编辑器
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public ITagValueEditor? Resolve(ITag tag)
    {
        return _editors.FirstOrDefault(e => e.CanEdit(tag));
    }
}

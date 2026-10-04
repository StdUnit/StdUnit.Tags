namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;

/// <summary>
/// 注册表，用于注册和解析 <see cref="ITagChannelDescriptorViewer"/> 和 <see cref="ITagChannelDescriptorEditor"/> 实现。   
/// </summary>
public sealed class TagChannelDescriptorRendererRegistry
{
    private readonly List<ITagChannelDescriptorViewer> _viewers;
    private readonly List<ITagChannelDescriptorEditor> _editors;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="viewers"></param>
    /// <param name="editors"></param>
    public TagChannelDescriptorRendererRegistry(IEnumerable<ITagChannelDescriptorViewer>? viewers = null, IEnumerable<ITagChannelDescriptorEditor>? editors = null)
    {
        viewers ??= Array.Empty<ITagChannelDescriptorViewer>();
        editors ??= Array.Empty<ITagChannelDescriptorEditor>();
        _viewers = viewers.OrderBy(v => v.Priority).ToList();
        _editors = editors.OrderBy(e => e.Priority).ToList();
    }

    /// <summary>
    /// 解析给定的 <see cref="TagChannelDescriptor"/> 的查看器。
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public ITagChannelDescriptorViewer? ResolveViewer(TagChannelDescriptor descriptor)
        => _viewers.FirstOrDefault(v => v.CanView(descriptor));


    /// <summary>
    /// 解析给定的 <see cref="TagChannelDescriptor"/> 的编辑器。
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public ITagChannelDescriptorEditor? ResolveEditor(TagChannelDescriptor descriptor)
        => _editors.FirstOrDefault(e => e.CanEdit(descriptor));
}

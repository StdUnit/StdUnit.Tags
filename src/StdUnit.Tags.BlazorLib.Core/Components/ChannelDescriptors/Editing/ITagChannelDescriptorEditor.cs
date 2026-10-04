using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;

/// <summary>
/// <see cref="TagChannelDescriptor"/> 编辑器
/// </summary>
public interface ITagChannelDescriptorEditor
{
    /// <summary>
    /// 优先级
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 能否编辑指定的描述符？
    /// </summary>
    /// <param name="descriptor">要编辑的描述符。</param>
    /// <returns>如果可以编辑指定的描述符，则返回 true；否则返回 false。</returns>
    bool CanEdit(TagChannelDescriptor descriptor);

    /// <summary>
    /// Render an editor UI for a descriptor. Implementations may materialize a strongly-typed descriptor.
    /// When descriptor instance changes or its values change, call <paramref name="descriptorChanged"/>.
    /// </summary>
    RenderFragment Edit(TagChannelDescriptor descriptor, Action<TagChannelDescriptor> descriptorChanged);
}

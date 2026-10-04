
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;

/// <summary>
/// <see cref="TagChannelDescriptor"/> 查看器
/// </summary>
public interface ITagChannelDescriptorViewer
{
    /// <summary>
    /// 优先级
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 能否查看指定的 <see cref="TagChannelDescriptor"/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    bool CanView(TagChannelDescriptor descriptor);

    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    RenderFragment View(TagChannelDescriptor descriptor);
}

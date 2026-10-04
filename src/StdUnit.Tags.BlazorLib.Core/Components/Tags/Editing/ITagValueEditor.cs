using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.Tags.Editing;

/// <summary>
/// Tag ValueEditor
/// </summary>
public interface ITagValueEditor
{
    /// <summary>
    /// 优先级
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 能否编辑测点
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    bool CanEdit(ITag tag);

    /// <summary>
    /// 渲染编辑器
    /// </summary>
    /// <param name="project"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    RenderFragment Render(ITagsProject? project, ITag tag);
}

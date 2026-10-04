
using StdUnit.Tags.BlazorLib.Components.Tags.Editing;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.Tags.Editors;

sealed class TextTagValueEditor : ITagValueEditor
{
    /// <inheritdoc/>
    public int Priority => int.MaxValue;

    /// <inheritdoc/>
    public bool CanEdit(ITag tag)
    {
        if (tag is null)
            return false;
        if (tag.IsReadOnly())
            return false;
        return tag.TagKind() == BuiltinTagKinds.STR;
    }

    /// <inheritdoc/>
    public RenderFragment Render(ITagsProject? project, ITag tag) => builder =>
    {
        builder.OpenComponent(0, typeof(TextTagValueEditorView));
        builder.AddAttribute(1, "Project", project);
        builder.AddAttribute(2, "Tag", tag);
        builder.CloseComponent();
    };
}

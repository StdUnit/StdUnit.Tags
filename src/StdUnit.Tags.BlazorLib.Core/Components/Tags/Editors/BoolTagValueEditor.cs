
using StdUnit.Tags.BlazorLib.Components.Tags.Editing;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.Tags.Editors;

sealed class BoolTagValueEditor : ITagValueEditor
{
    /// <inheritdoc/>
    public int Priority => int.MaxValue - 2;

    /// <inheritdoc/>
    public bool CanEdit(ITag tag)
    {
        if (tag is null)
            return false;
        if (tag.IsReadOnly())
            return false;

        return tag.Value is bool
            || tag.TagKind() == BuiltinTagKinds.BIT
            || tag.TagKind() == BuiltinTagKinds.DI
            || tag.TagKind() == BuiltinTagKinds.DO;
    }

    /// <inheritdoc/>
    public RenderFragment Render(ITagsProject? project, ITag tag) => builder =>
    {
        builder.OpenComponent(0, typeof(BoolTagValueEditorView));
        builder.AddAttribute(1, "Project", project);
        builder.AddAttribute(2, "Tag", tag);
        builder.CloseComponent();
    };
}


using StdUnit.Tags.BlazorLib.Components.Tags.Editing;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.Tags.Editors;

sealed class NumericTagValueEditor : ITagValueEditor
{
    /// <inheritdoc/>
    public int Priority => int.MaxValue - 1;

    /// <inheritdoc/>
    public bool CanEdit(ITag tag)
    {
        if (tag is null)
            return false;
        if (tag.IsReadOnly())
            return false;

        var kind = tag.TagKind();
        return kind == BuiltinTagKinds.BYTE
            || kind == BuiltinTagKinds.INT16
            || kind == BuiltinTagKinds.UINT16
            || kind == BuiltinTagKinds.INT32
            || kind == BuiltinTagKinds.UINT32
            || kind == BuiltinTagKinds.INT64
            || kind == BuiltinTagKinds.UINT64
            || kind == BuiltinTagKinds.FLOAT;
    }

    /// <inheritdoc/>
    public RenderFragment Render(ITagsProject? project, ITag tag) => builder =>
    {
        builder.OpenComponent(0, typeof(NumericTagValueEditorView));
        builder.AddAttribute(1, "Project", project);
        builder.AddAttribute(2, "Tag", tag);
        builder.CloseComponent();
    };
}

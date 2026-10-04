using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.Hjzk;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Hjzk;

sealed class HjzkTagChannelDescriptorEditor : ITagChannelDescriptorEditor
{
    public int Priority => 110;
    public bool CanEdit(TagChannelDescriptor descriptor) => descriptor.Driver == HjzkNames.DriverName;

    public RenderFragment Edit(TagChannelDescriptor descriptor, Action<TagChannelDescriptor> descriptorChanged)
    {
        var d = descriptor.ToHjzkTagChannelDescriptor();
        if (!ReferenceEquals(d, descriptor))
        {
            descriptorChanged(d);
        }

        return builder =>
        {
            builder.OpenComponent<HjzkTagChannelDescriptorEditorView>(0);
            builder.AddAttribute(1, nameof(HjzkTagChannelDescriptorEditorView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

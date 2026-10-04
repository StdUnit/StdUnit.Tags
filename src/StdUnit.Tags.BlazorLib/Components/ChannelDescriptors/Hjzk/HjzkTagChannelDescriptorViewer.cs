using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.Hjzk;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Hjzk;

sealed class HjzkTagChannelDescriptorViewer : ITagChannelDescriptorViewer
{
    public int Priority => 110;
    public bool CanView(TagChannelDescriptor descriptor) => descriptor.Driver == HjzkNames.DriverName;

    public RenderFragment View(TagChannelDescriptor descriptor)
    {
        var d = descriptor.ToHjzkTagChannelDescriptor();
        return builder =>
        {
            builder.OpenComponent<HjzkTagChannelDescriptorViewerView>(0);
            builder.AddAttribute(1, nameof(HjzkTagChannelDescriptorViewerView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

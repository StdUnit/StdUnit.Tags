using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.ComScanner;
using StdUnit.Tags.ComScanner.Channels;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Com;

sealed class ComChannelDescriptorViewer : ITagChannelDescriptorViewer
{
    public int Priority => 100;
    public bool CanView(TagChannelDescriptor descriptor) => descriptor.Driver == ComDriverNames.DriverName;

    public RenderFragment View(TagChannelDescriptor descriptor)
    {
        var d = descriptor.ToComChannelDescriptor();
        return builder =>
        {
            builder.OpenComponent<ComChannelDescriptorViewerView>(0);
            builder.AddAttribute(1, nameof(ComChannelDescriptorViewerView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

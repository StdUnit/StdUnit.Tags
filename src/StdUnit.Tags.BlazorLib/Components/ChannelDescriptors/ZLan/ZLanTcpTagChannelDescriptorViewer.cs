using StdUnit.Tags;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.ZLan;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ZLan;

public sealed class ZLanTcpTagChannelDescriptorViewer : ITagChannelDescriptorViewer
{
    public int Priority => 105;
    public bool CanView(TagChannelDescriptor descriptor) => descriptor.Driver == ZLanTcpNames.DriverName;

    public RenderFragment View(TagChannelDescriptor descriptor)
    {
        var d = descriptor.ToZLanTcpTagChannelDescriptor();
        return builder =>
        {
            builder.OpenComponent<ZLanTcpTagChannelDescriptorViewerView>(0);
            builder.AddAttribute(1, nameof(ZLanTcpTagChannelDescriptorViewerView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

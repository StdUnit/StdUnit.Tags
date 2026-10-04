using StdUnit.Tags;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.OpcUaClient;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.OpcUa;

public sealed class OpcUaClientTagChannelDescriptorViewer : ITagChannelDescriptorViewer
{
    public int Priority => 100;
    public bool CanView(TagChannelDescriptor descriptor) => descriptor.Driver == OpcUaClientNames.DriverName;

    public RenderFragment View(TagChannelDescriptor descriptor)
    {
        var d = descriptor.ToOpcUaClientTagChannelDescriptor();
        return builder =>
        {
            builder.OpenComponent<OpcUaClientTagChannelDescriptorViewerView>(0);
            builder.AddAttribute(1, nameof(OpcUaClientTagChannelDescriptorViewerView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

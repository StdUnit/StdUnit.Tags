using StdUnit.Tags;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.OpcUaClient;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.OpcUa;

public sealed class OpcUaClientTagChannelDescriptorEditor : ITagChannelDescriptorEditor
{
    public int Priority => 100;
    public bool CanEdit(TagChannelDescriptor descriptor) => descriptor.Driver == OpcUaClientNames.DriverName;

    public RenderFragment Edit(TagChannelDescriptor descriptor, Action<TagChannelDescriptor> descriptorChanged)
    {
        var d = descriptor.ToOpcUaClientTagChannelDescriptor();
        if (!ReferenceEquals(d, descriptor))
        {
            descriptorChanged(d);
        }

        return builder =>
        {
            builder.OpenComponent<OpcUaClientTagChannelDescriptorEditorView>(0);
            builder.AddAttribute(1, nameof(OpcUaClientTagChannelDescriptorEditorView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}
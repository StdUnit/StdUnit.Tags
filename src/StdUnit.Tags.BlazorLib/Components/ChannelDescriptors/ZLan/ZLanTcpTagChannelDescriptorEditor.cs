using StdUnit.Tags;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;
using StdUnit.Tags.ZLan;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ZLan;

public sealed class ZLanTcpTagChannelDescriptorEditor : ITagChannelDescriptorEditor
{
    public int Priority => 105;
    public bool CanEdit(TagChannelDescriptor descriptor) => descriptor.Driver == ZLanTcpNames.DriverName;

    public RenderFragment Edit(TagChannelDescriptor descriptor, Action<TagChannelDescriptor> descriptorChanged)
    {
        var d = descriptor.ToZLanTcpTagChannelDescriptor();
        if (!ReferenceEquals(d, descriptor))
        {
            descriptorChanged(d);
        }

        return builder =>
        {
            builder.OpenComponent<ZLanTcpTagChannelDescriptorEditorView>(0);
            builder.AddAttribute(1, nameof(ZLanTcpTagChannelDescriptorEditorView.Descriptor), d);
            builder.CloseComponent();
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.OpcUa;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddOpcUaChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<OpcUaClientTagChannelDescriptorViewer>()
                .AddTagChannelDescriptorEditor<OpcUaClientTagChannelDescriptorEditor>();
        return builder;
    }
}

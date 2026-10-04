using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ModbusTcp;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ModbusTcp;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddModbusTcpChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<ModbusTcpTagChannelDescriptorViewer>()
               .AddTagChannelDescriptorEditor<ModbusTcpTagChannelDescriptorEditor>();
        return builder;
    }
}

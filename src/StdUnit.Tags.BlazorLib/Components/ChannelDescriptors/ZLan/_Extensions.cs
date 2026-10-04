using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ZLan;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddZLanChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<ZLanTcpTagChannelDescriptorViewer>()
               .AddTagChannelDescriptorEditor<ZLanTcpTagChannelDescriptorEditor>();
        return builder;
    }
}

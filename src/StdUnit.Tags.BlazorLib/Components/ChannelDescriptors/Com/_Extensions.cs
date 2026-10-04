using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Com;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddComScannerChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<ComChannelDescriptorViewer>()
               .AddTagChannelDescriptorEditor<ComChannelDescriptorEditor>();
        return builder;
    }
}

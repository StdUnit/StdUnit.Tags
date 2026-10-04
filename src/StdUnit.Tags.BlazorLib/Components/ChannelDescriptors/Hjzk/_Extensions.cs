using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Hjzk;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddHjzkChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<HjzkTagChannelDescriptorViewer>()
                .AddTagChannelDescriptorEditor<HjzkTagChannelDescriptorEditor>();
        return builder;
    }
}

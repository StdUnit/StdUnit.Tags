using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.S7;

internal static class ServiceCollectionExtensions
{
    public static TagsBlazorBuilder AddS7ChannelDescriptorViewerAndEditor(this TagsBlazorBuilder builder)
    {
        builder.AddTagChannelDescriptorViewer<S7TagChannelDescriptorViewer>()
               .AddTagChannelDescriptorEditor<S7TagChannelDescriptorEditor>();
        return builder;
    }
}

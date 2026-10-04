using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.OpcUa;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ZLan;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Hjzk;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.ModbusTcp;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.S7;
using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Com;

namespace StdUnit.Tags.BlazorLib;

/// <summary>
/// extensions for DependencyInjection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 增加TagsBlazorLib服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IServiceCollection AddTagsBlazorLib(this IServiceCollection services, Action<TagsBlazorBuilder>? config = null)
    {
        services.AddTagsBlazorLibCore(builder =>
        {
            // Default channel descriptor viewers/editors
            builder.AddS7ChannelDescriptorViewerAndEditor()
                   .AddModbusTcpChannelDescriptorViewerAndEditor()
                   .AddHjzkChannelDescriptorViewerAndEditor()
                   .AddComScannerChannelDescriptorViewerAndEditor()
                   .AddZLanChannelDescriptorViewerAndEditor()
                   .AddOpcUaChannelDescriptorViewerAndEditor();

            config?.Invoke(builder);
        });
        return services;
    }



}

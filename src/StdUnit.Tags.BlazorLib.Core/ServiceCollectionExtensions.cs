using StdUnit.Tags.BlazorLib.Components.Tags.Editors;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Microsoft.AspNetCore.Components;

namespace StdUnit.Tags.BlazorLib;

/// <summary>
/// extensions for DependencyInjection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 增加TagsBlazorLib的核心功能
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IServiceCollection AddTagsBlazorLibCore(this IServiceCollection services, Action<TagsBlazorBuilder>? config = null)
    {
        services.AddMudServices();
        var builder = new TagsBlazorBuilder(services);
        // default tag value editors
        builder.AddTagValueEditor<BoolTagValueEditor>()
               .AddTagValueEditor<NumericTagValueEditor>()
               .AddTagValueEditor<TextTagValueEditor>();

        // user configuration
        config?.Invoke(builder);
        builder.Build();
        return builder.Services;
    }
}

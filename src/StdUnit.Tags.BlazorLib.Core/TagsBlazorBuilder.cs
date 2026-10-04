using StdUnit.Tags.BlazorLib.Components.ChannelDescriptors.Editing;

using StdUnit.Tags.BlazorLib.Components.Tags.Editing;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.BlazorLib;

/// <summary>
/// TagBlazor Builder
/// </summary>
public class TagsBlazorBuilder
{
    /// <summary>
    /// The service collection
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="services"></param>
    public TagsBlazorBuilder(IServiceCollection services)
    {
        services.AddScoped<TagsBlazorLibJsInterop>();
        // Registry
        services.AddScoped<TagValueEditorRegistry>();
        services.AddScoped<TagChannelDescriptorRendererRegistry>();

        Services = services;
    }

    /// <summary>
    /// 注册 <see cref="ITagValueEditor"/>
    /// </summary>
    /// <typeparam name="TEditor"></typeparam>
    /// <returns></returns>
    public TagsBlazorBuilder AddTagValueEditor<TEditor>()
        where TEditor : class, ITagValueEditor
    {
        this.Services.AddScoped<ITagValueEditor, TEditor>();
        return this;
    }

    /// <summary>
    /// 注册 <see cref="ITagChannelDescriptorViewer"/>
    /// </summary>
    /// <typeparam name="TViewer"></typeparam>
    /// <returns></returns>
    public TagsBlazorBuilder AddTagChannelDescriptorViewer<TViewer>()
        where TViewer : class, ITagChannelDescriptorViewer
    {
        this.Services.AddScoped<ITagChannelDescriptorViewer, TViewer>();
        return this;
    }

    /// <summary>
    /// 注册 <see cref="ITagChannelDescriptorEditor"/>
    /// </summary>
    /// <typeparam name="TEditor"></typeparam>
    /// <returns></returns>
    public TagsBlazorBuilder AddTagChannelDescriptorEditor<TEditor>()
        where TEditor : class, ITagChannelDescriptorEditor
    {
        this.Services.AddScoped<ITagChannelDescriptorEditor, TEditor>();
        return this;
    }


    /// <summary>
    /// 构建
    /// </summary>
    public void Build()
    {
        return;
    }
}

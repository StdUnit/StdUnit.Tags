using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// extensions for <see cref="TagsProjectServiceBuilder"/> to add SimpleFiles support
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 登记一个 <see cref="JsonSerializerContext"/>，作为 SimpleFiles 的 JSON 测点（<c>type="JSON"</c>）的序列化元数据来源。<br/>
    /// <br/>
    /// <b>NativeAOT / 裁剪宿主必须登记</b>：这些宿主下 <c>System.Text.Json</c> 默认关闭反射序列化，
    /// 未登记时会在构建 JSON 测点时以 <see cref="TagsProjectConfigurationException"/> 快速失败；
    /// 普通 JIT 宿主可以不登记（走反射）。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="context">源生成的上下文，如 <c>MyJsonContext.Default</c>；可多次调用登记多个</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddJsonSerializerContext(
        this TagsProjectServiceBuilder builder,
        JsonSerializerContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        builder.Services.AddSingleton(context);
        return builder;
    }

    /// <summary>
    /// 注册SimpleFiles支持。是 <see cref="AddSimpleFilesChannel"/> 与 <see cref="AddSimpleFilesDirectTagBuilder"/> 的组合
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddSimpleFilesSupport(this TagsProjectServiceBuilder builder)
    {
        builder.Services.AddSingleton<ITagsProjectSchemaProvider, SimpleFilesSchemaProvider>();

        builder
            .AddSimpleFilesChannel()
            .AddSimpleFilesDirectTagBuilder();
        return builder;
    }

    #region 基本扩展
    /// <summary>
    /// 注册SimpleFiles支持——仅注册ChannelFactory，不注册DirectTagBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="SimpleFilesNames.DriverName"/> 时，会尝试构建一个通道。
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddSimpleFilesChannel(this TagsProjectServiceBuilder builder)
    {
        // register SimpleFiles channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, SimpleFilesTagChannelFactory>(SimpleFilesNames.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var simpleFilesFactory = sp.GetRequiredKeyedService<ITagChannelFactory>(SimpleFilesNames.DriverName);
            composite.AddFactory(simpleFilesFactory);
        });
        return builder;
    }

    /// <summary>
    /// 注册SimpleFiles支持——仅注册DirectTagBuilder，不注册ChannelFactory。<br/>
    /// 作用是在通道的驱动为 <see cref="SimpleFilesNames.DriverName"/> 时，会尝试构建一个测点；<br/>
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置DirectTagBuilder的回调</param>
    /// <param name="predicate">用于过滤DirectTagBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddSimpleFilesDirectTagBuilder(
        this TagsProjectServiceBuilder builder,
        Action<SimpleFilesDirectTagBuilder>? configure = null,
        Func<SimpleFilesDirectTagBuilder, bool>? predicate = null
        )
    {
        // register SimpleFiles tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddDirectTagBuilder<SimpleFilesDirectTagBuilder>(
                SimpleFilesNames.DriverName,
                configure: tagBuilder =>
                {
                    var contexts = sp.GetServices<JsonSerializerContext>()?.ToList();
                    tagBuilder.UseJsonSerializerContexts(contexts);
                    configure?.Invoke(tagBuilder);
                },
                predicate: predicate);
        });

        return builder;
    }
    #endregion
}

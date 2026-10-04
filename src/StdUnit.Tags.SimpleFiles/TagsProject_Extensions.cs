using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// extensions for <see cref="TagsProjectServiceBuilder"/> to add SimpleFiles support
/// </summary>
public static class TagsProject_Extensions
{
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
            composite.AddDirectTagBuilder<SimpleFilesDirectTagBuilder>(SimpleFilesNames.DriverName, configure, predicate);
        });

        return builder;
    }
    #endregion
}

using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.S7;

/// <summary>
/// extensions for <see cref="TagsProjectServiceBuilder"/> to add S7 support
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 注册S7支持。是 <see cref="AddS7Channel"/>、<see cref="AddS7TagCbntBuilder"/> 与 <see cref="AddS7DirectTagBuilder"/> 的组合
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddS7Support(this TagsProjectServiceBuilder builder)
    {

        builder.Services.AddSingleton<ITagsProjectSchemaProvider, S7SchemaProvider>();

        builder
            .AddS7Channel()
            .AddS7TagCbntBuilder()
            .AddS7DirectTagBuilder();
        return builder;
    }

    #region 基本扩展
    /// <summary>
    /// 注册S7支持——仅注册ChannelFactory，不注册DirectTagBuilder/TagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="S7Names.DriverName"/> 时，会尝试构建一个通道。
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddS7Channel(this TagsProjectServiceBuilder builder)
    {
        // register S7 channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, S7TagChannelFactory>(S7Names.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var s7Factory = sp.GetRequiredKeyedService<ITagChannelFactory>(S7Names.DriverName);
            composite.AddFactory(s7Factory);
        });
        return builder;
    }

    /// <summary>
    /// 注册S7支持——仅注册测点组合构建器（TagCbntBuilder），不注册ChannelFactory/DirectTagBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="S7Names.DriverName"/> 时，会尝试构建一个测点组合。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置TagCbntBuilder的回调</param>
    /// <param name="predicate">用于过滤TagCbntBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddS7TagCbntBuilder(
        this TagsProjectServiceBuilder builder,
        Action<S7TagCbntBuilder>? configure = null,
        Func<S7TagCbntBuilder, bool>? predicate = null
        )
    {
        // register S7 cbnt loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<S7TagCbntBuilder>(S7Names.DriverName, configure, predicate);
        });
        return builder;
    }

    /// <summary>
    /// 注册S7支持——仅注册直接测点构建器（DirectTagBuilder），不注册ChannelFactory/TagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="S7Names.DriverName"/> 时，会尝试构建一个测点。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置DirectTagBuilder的回调</param>
    /// <param name="predicate">用于过滤DirectTagBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddS7DirectTagBuilder(
        this TagsProjectServiceBuilder builder,
        Action<S7DirectTagBuilder>? configure = null,
        Func<S7DirectTagBuilder, bool>? predicate = null
        )
    {
        // register S7 tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddDirectTagBuilder<S7DirectTagBuilder>(S7Names.DriverName, configure, predicate);
        });
        return builder;
    }
    #endregion
}

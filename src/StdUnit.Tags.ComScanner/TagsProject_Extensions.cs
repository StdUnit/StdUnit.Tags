using StdUnit.Tags.ComScanner.Channels;
using StdUnit.Tags.ComScanner.Tags;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.ComScanner;

/// <summary>
/// extensions for TagsProjectServiceBuilder to add COM scanner support
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 注册COM支持。
    /// 是 <see cref="AddComScannerChannel"/> 与 <see cref="AddComScannerDirectTagBuilder"/> 的组合
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddComScannerSupport(this TagsProjectServiceBuilder builder)
    {
        // 注册 COM schema 提供者（EnableXmlSchemaValidation 时把 com.xsd 合并进校验）
        builder.Services.AddSingleton<ITagsProjectSchemaProvider, ComScannerSchemaProvider>();

        builder
            .AddComScannerChannel()
            .AddComScannerDirectTagBuilder();
        return builder;
    }

    #region 基本扩展
    /// <summary>
    /// 注册COM 支持——仅注册ChannelFactory，不注册DirectTagBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="ComDriverNames.DriverName"/> 时，会尝试构建一个通道。
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddComScannerChannel(this TagsProjectServiceBuilder builder)
    {
        // register channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, ComChannelFactory>(ComDriverNames.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>(ComDriverNames.DriverName);
            composite.AddFactory(factory);
        });
        return builder;
    }

    /// <summary>
    /// 注册COM 支持——仅注册DirectTagBuilder，不注册ChannelFactory。<br/>
    /// 作用是在通道的驱动为 <see cref="ComDriverNames.DriverName"/> 时，会尝试构建一个测点；<br/>
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置DirectTagBuilder的回调</param>
    /// <param name="predicate">用于过滤DirectTagBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddComScannerDirectTagBuilder(
        this TagsProjectServiceBuilder builder,
        Action<ComDirectTagBuilder>? configure = null,
        Func<ComDirectTagBuilder, bool>? predicate = null
        )
    {
        // register tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddDirectTagBuilder<ComDirectTagBuilder>(ComDriverNames.DriverName, configure, predicate);
        });

        return builder;
    }
    #endregion
}

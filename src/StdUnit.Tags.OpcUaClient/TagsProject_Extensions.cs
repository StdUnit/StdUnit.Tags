using StdUnit.Tags.OpcUaClient.Cbnts;
using StdUnit.Tags.OpcUaClient.DirectTags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opc.Ua;

namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// extensions for DependencyInjection
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 添加 OpcUaClient 支持。是 <see cref="AddOpcUaClientChannel"/>、<see cref="AddOpcUaClientTagCbntBuilder"/> 与 <see cref="AddOpcUaClientDirectTagBuilder"/> 的组合
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="checkIsFailed">
    /// 判定"某节点的读取是否算失败"的委托；不传则用内置口径 <see cref="OpcUaValueQuality.IsFailed"/>
    /// （状态码 <c>Bad</c> 才算失败，<c>Uncertain</c> 照原样采集）。
    /// </param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddOpcUaClientSupport(
        this TagsProjectServiceBuilder builder,
        Func<ServiceResult?, DataValue?, bool>? checkIsFailed = null)
    {

        builder.Services.AddSingleton<ITagsProjectSchemaProvider, OpcUaClientSchemaProvider>();

        builder
            .AddOpcUaClientChannel(checkIsFailed)
            .AddOpcUaClientTagCbntBuilder()
            .AddOpcUaClientDirectTagBuilder();
        return builder;
    }

    #region 基本扩展
    /// <summary>
    /// 注册OpcUaClient支持——仅注册ChannelFactory，不注册DirectTagBuilder/TagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="OpcUaClientNames.DriverName"/> 时，会尝试构建一个通道。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="checkIsFailed">
    /// 判定"某节点的读取是否算失败"的委托；不传则用内置口径 <see cref="OpcUaValueQuality.IsFailed"/>
    /// （状态码 <c>Bad</c> 才算失败，<c>Uncertain</c> 照原样采集）。<br/>
    /// 传 <c>(_, _) =&gt; false</c> 表示"任何状态都照原样采集"（读取永不因质量失败，但值可能是 <c>null</c>）；
    /// 传 <c>(err, value) =&gt; ...</c> 可把 <c>Uncertain</c> 也当作失败。
    /// </param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddOpcUaClientChannel(
        this TagsProjectServiceBuilder builder,
        Func<ServiceResult?, DataValue?, bool>? checkIsFailed = null)
    {
        // 用显式工厂闭包而不是 AddKeyedSingleton<ITagChannelFactory, OpcUaClientTagChannelFactory>()：
        // 委托是注册时给定的普通参数，不必（也不该）作为一个服务类型塞进容器。
        builder.Services.AddKeyedSingleton<ITagChannelFactory>(
            OpcUaClientNames.DriverName,
            (sp, _) => new OpcUaClientTagChannelFactory(
                sp.GetRequiredService<ILoggerFactory>(),
                checkIsFailed));
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>(OpcUaClientNames.DriverName);
            composite.AddFactory(factory);
        });
        return builder;
    }

    /// <summary>
    /// 注册OpcUaClient支持——仅注册测点组合构建器（TagCbntBuilder），不注册ChannelFactory/DirectTagBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="OpcUaClientNames.DriverName"/> 时，会尝试构建一个测点组合。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置TagCbntBuilder的回调</param>
    /// <param name="predicate">用于过滤TagCbntBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddOpcUaClientTagCbntBuilder(
        this TagsProjectServiceBuilder builder,
        Action<OpcUaClientTagCbntBuilder>? configure = null,
        Func<OpcUaClientTagCbntBuilder, bool>? predicate = null
        )
    {
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<OpcUaClientTagCbntBuilder>(OpcUaClientNames.DriverName, configure, predicate);
        });
        return builder;
    }

    /// <summary>
    /// 注册OpcUaClient支持——仅注册直接测点构建器（DirectTagBuilder），不注册ChannelFactory/TagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="OpcUaClientNames.DriverName"/> 时，会尝试构建一个测点。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置DirectTagBuilder的回调</param>
    /// <param name="predicate">用于过滤DirectTagBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddOpcUaClientDirectTagBuilder(
        this TagsProjectServiceBuilder builder,
        Action<OpcUaClientDirectTagBuilder>? configure = null,
        Func<OpcUaClientDirectTagBuilder, bool>? predicate = null
        )
    {
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddDirectTagBuilder<OpcUaClientDirectTagBuilder>(OpcUaClientNames.DriverName, configure, predicate);
        });
        return builder;
    }
    #endregion
}

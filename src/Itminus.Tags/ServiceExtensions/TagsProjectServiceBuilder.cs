using Itminus.Tags.Core.Projects;
using Itminus.Tags.Logicets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itminus.Tags;

/// <summary>
///  builder for configuring and building services related to tags projects,
///  including 
///     channel factories, 
///     tag loaders, 
///     and logicet load options.
/// </summary>
public class TagsProjectServiceBuilder
{
    /// <summary>
    /// c'tor
    /// </summary>
    public TagsProjectServiceBuilder(IServiceCollection services)
    {

        this.Services = services;

        this.ChannelFactories = new CompositeTagChannelFactory();
        this.ChannelFactoriesConfiguration = new List<Action<IServiceProvider, CompositeTagChannelFactory>>();

        this.TagsLoaders = new CompositeTagsLoader();
        this.TagsLoadersConfiguration = new List<Action<IServiceProvider, CompositeTagsLoader>>();

        this.LogicetLoadOptionsBuilder = this.Services.AddOptions<LogicetLoadOptions>();
    }

    /// <summary>
    /// 服务
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 使用默认加载器和工厂
    /// </summary>
    public bool UseDefaults { get; set; } = true;

    #region ChannelFactories
    /// <summary>
    /// 通道工厂
    /// </summary>
    public CompositeTagChannelFactory ChannelFactories { get; set; }

    /// <summary>
    /// 通道工厂配置，用于配置<see cref="ChannelFactories"/>
    /// </summary>
    protected IList<Action<IServiceProvider, CompositeTagChannelFactory>> ChannelFactoriesConfiguration { get; set; }


    /// <summary>
    /// 配置ChannelFactory
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public TagsProjectServiceBuilder ConfigChannelsFactory(Action<IServiceProvider, CompositeTagChannelFactory> config)
    {
        this.ChannelFactoriesConfiguration.Add(config);
        return this;
    }

    /// <summary>
    /// 应用 <see cref="ChannelFactoriesConfiguration"/> 里的配置
    /// </summary>
    /// <param name="sp"></param>
    private void ApplyChannelFactoriesConfiguration(IServiceProvider sp)
    {
        foreach (var config in this.ChannelFactoriesConfiguration)
        {
            config.Invoke(sp, this.ChannelFactories);
        }
    }
    #endregion



    #region TagLoaders
    /// <summary>
    /// 通道加载器
    /// </summary>
    public CompositeTagsLoader TagsLoaders { get; set; }

    /// <summary>
    /// 通道加载器的配置
    /// </summary>
    protected IList<Action<IServiceProvider, CompositeTagsLoader>> TagsLoadersConfiguration { get; set; }

    /// <summary>
    /// 配置 <see cref="TagsLoaders"/>
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public TagsProjectServiceBuilder ConfigTagsLoader(Action<IServiceProvider, CompositeTagsLoader> config)
    {
        this.TagsLoadersConfiguration.Add(config);
        return this;
    }

    /// <summary>
    /// 应用 <see cref="TagsLoadersConfiguration"/> 中的配置
    /// </summary>
    /// <param name="sp"></param>
    private void ApplyTagsLoadersConfiguration(IServiceProvider sp)
    {
        foreach (var config in this.TagsLoadersConfiguration)
        {
            config.Invoke(sp, this.TagsLoaders);
        }
    }
    #endregion


    #region
    /// <summary>
    /// 业务逻辑选项的构建器
    /// </summary>
    public OptionsBuilder<LogicetLoadOptions> LogicetLoadOptionsBuilder { get; }
    #endregion

    #region 加载期校验
    /// <summary>
    /// 启用加载期 XSD 校验（可选功能）。<br/>
    /// 启用后，<see cref="ITagsProjectFactory.Create"/> / <c>MakeProject</c> 在加载通道与测点之前，
    /// 会用嵌入程序集的 XSD（<see cref="TagsProjectSchema"/>）校验项目 XML；
    /// 不通过时抛出 <see cref="TagsProjectSchemaException"/>。<br/>
    /// 等价于 <c>AddValidation&lt;TagsProjectSchemaValidator&gt;()</c>。
    /// </summary>
    /// <returns></returns>
    public TagsProjectServiceBuilder EnableXmlSchemaValidation() =>
        this.AddValidation<TagsProjectSchemaValidator>();

    /// <summary>
    /// 启用加载期交叉引用校验。
    /// 会注册一些内置的验证器：
    /// <see cref="ChannelCrossReferenceValidator"/>（channel 属性指向已声明的 Channel）、
    /// <see cref="ChannelDriverFactoryValidator"/>（driver 有对应已注册工厂）、
    /// <see cref="NestedEntryValidator"/>（嵌套在入口内的 isEntry 不生效）、
    /// <see cref="EntryChannelExclusivityValidator"/>（同一个通道不被多个入口共用）。<br/>
    /// 启用后，<see cref="ITagsProjectFactory.Create"/> / <c>MakeProject</c> 在加载通道与测点之前，
    /// 校验 XSD 无法表达的引用关系与结构约束；不通过时抛出 <see cref="TagsProjectSchemaException"/>，
    /// 错误带完整路径上下文，在加载期报错而不是运行时才暴露。<br/>
    /// <b>默认已启用</b>（见 <see cref="UseDefaults"/> 路径下的 AddDefaults）；
    /// 本方法为幂等显式调用（语义文档化）。
    /// </summary>
    /// <returns></returns>
    public TagsProjectServiceBuilder EnableCrossReferenceValidation()
    {
        this.AddValidation<ChannelCrossReferenceValidator>();
        this.AddValidation<ChannelDriverFactoryValidator>();
        this.AddValidation<NestedEntryValidator>();
        this.AddValidation<EntryChannelExclusivityValidator>();
        return this;
    }

    /// <summary>
    /// 注册一个加载期项目校验器实现（可选功能）。<br/>
    /// 每个实现负责一类独立校验，可注册多个（按注册顺序执行），第三方也可追加自己的校验器：
    /// <code>
    /// b.AddValidation&lt;TagsProjectSchemaValidator&gt;();         // 内置：XSD 校验
    /// b.AddValidation&lt;ChannelCrossReferenceValidator&gt;();     // 内置：channel 引用校验
    /// b.AddValidation&lt;MyValidator&gt;();                         // 自定义校验器
    /// </code>
    /// 校验器在 <see cref="ITagsProjectFactory.Create"/> / <c>MakeProject</c> 加载通道与测点之前执行，
    /// 不通过时抛出异常（如 <see cref="TagsProjectSchemaException"/>）。
    /// </summary>
    /// <typeparam name="TValidator">校验器实现类型（实现 <see cref="ITagsProjectValidator"/>）</typeparam>
    /// <returns></returns>
    public TagsProjectServiceBuilder AddValidation<TValidator>()
        where TValidator : class, ITagsProjectValidator
    {
        this.Services.AddSingleton<ITagsProjectValidator, TValidator>();
        return this;
    }
    #endregion


    private TagsProjectServiceBuilder AddDefaults()
    {
        this.Services.AddSingleton<ITagGrpRunnerFactory, TagGrpRunnerFactory>();
        this.Services.AddSingleton<ILogicetsLoader, LogicetLoader>();
        this.Services.AddScoped<ITagsProjectFactory, TagsProjectFactory>();

        // 默认启用加载期交叉引用校验
        this.EnableCrossReferenceValidation();

        return this;
    }

    /// <summary>
    /// 构建服务，注册必要的服务和配置。<br/>
    /// </summary>
    public void Build()
    {
        // channels/tags
        this.Services.AddSingleton<ITagChannelsLoader, TagChannelsLoader>();
        this.Services.AddSingleton<ITagChannelFactory>(sp =>
        {
            this.ApplyChannelFactoriesConfiguration(sp);
            return this.ChannelFactories;
        });
        this.Services.AddSingleton<ITagsLoader>(sp =>
        {
            this.ApplyTagsLoadersConfiguration(sp);
            return this.TagsLoaders;
        });


        // defaults
        if (this.UseDefaults)
        {
            this.AddDefaults();
        }
    }


}

/// <summary>
/// 业务逻辑加载选项，用于配置 Logicet 插件的加载行为。<br/>
/// </summary>
/// <remarks>
/// 插件加载能力因目标框架而异：<br/>
/// * <c>net8.0</c>：基于 <c>AssemblyLoadContext</c> 按目录隔离加载，支持卸载；
///   <see cref="SharedTypesFilter"/> 用于声明宿主与插件之间共享的类型。<br/>
/// * <c>net472</c>：退化为 <c>Assembly.LoadFrom</c>，插件仍可独立编译并挂载，但
///   <b>无依赖隔离、无卸载能力</b>；<see cref="SharedTypesFilter"/> 仍会被调用但不产生效果
///   （同名程序集天然共享同一实例），加载时会输出 WARNING 日志。<br/>
/// 上述限制不影响在宿主内注册逻辑组件（<c>TryAddLogicet&lt;TLogicet&gt;</c>），该方式在两个框架下均可用。
/// </remarks>
public class LogicetLoadOptions
{
    /// <summary>
    /// 共享类型过滤器。仅 <c>net8.0</c> 生效；<c>net472</c> 下会被调用但结果被忽略（见类备注）。
    /// </summary>
    public LogicetSharedTypesFilter? SharedTypesFilter { get; set; }
}

/// <summary>
/// 共享类型过滤器，用于在加载 Logicet 插件时指定哪些类型需要在主程序和插件之间共享。
/// 通过实现这个委托，用户可以动态地添加或修改共享类型列表
/// </summary>
/// <param name="dll">dll 文件路径</param>
/// <param name="sharedTypes">共享类型列表</param>
public delegate void LogicetSharedTypesFilter(string dll, List<Type> sharedTypes);
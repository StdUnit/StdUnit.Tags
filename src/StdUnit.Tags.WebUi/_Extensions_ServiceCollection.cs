using StdUnit.Tags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StdUnit.Tags.WebUi.Internal;


namespace StdUnit.Tags.WebUi;

/// <summary>
/// 观测流（外加设计器元数据）的 DI 注册入口：<see cref="AddTagsStream"/> / <see cref="AddKeyedTagsStream"/>。<br/>
/// 端点映射不在这里，见 <see cref="TagsStreamExtensions_Endpoints"/> 里的 <c>MapTagsStream</c>。
/// </summary>
public static class TagsStreamExtensions_ServiceCollection
{
    /// <summary>
    /// 注册<b>不具名</b>那一路观测流：观测由 <c>AddTagsProjectServices</c> 注册的 <see cref="ITagsProjectCtrl"/>，
    /// 端点落在 <c>{prefix}/tree|stream|value|rescan|ui</c>。<br/>
    /// 一个容器里只能有一路不具名流；要多路请用 <see cref="AddKeyedTagsStream"/>。<br/>
    /// <br/>
    /// <b>启动顺序</b>：hub 以 <see cref="IHostedService"/> 注册，随宿主一起启动。它在启动时按
    /// <see cref="ITagsProjectCtrl.Project"/> 对账，所以<b>晚启动也能补上</b>：即使项目已在轮询，
    /// 结构树与值流都正常（轮次回调运行期也能挂，从下一轮开始生效）。
    /// 想尽早拿到结构树，就把轮询放在宿主启动之后（例如在请求里/更晚注册的 hosted service 里 <c>StartPollAsync</c>）。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">这一路流的配置回调</param>
    /// <returns></returns>
    public static IServiceCollection AddTagsStream(this IServiceCollection services, Action<TagsStreamOptions>? configure = null)
    {
        if (services.Any(d => d.ServiceType == typeof(TagsStreamDescriptor)
            && ((TagsStreamDescriptor)d.ImplementationInstance!).Key is null))
        {
            throw new InvalidOperationException(
                "不具名观测流只能注册一路；需要多路请改用 AddKeyedTagsStream(key) 注册具名流。");
        }

        var options = AddTagsStreamCommon(services, configure);

        services.AddSingleton(new TagsStreamDescriptor(key: null, options));

        // 同一实例注册为 IHostedService：用工厂而不是 AddHostedService<T>()——后者会另开
        // IHostedService → T 的描述符再 new 一个，宿主启动的 hub 与端点看到的不是同一份数据。
        services.AddSingleton<IHostedService>(
            sp => sp.GetRequiredService<TagsStreamRegistry>()
                    .GetRequired(null)
                    .HostedService
        );

        // 不具名那一路按 ITagsStreamHub 解析（具名那一路是 keyed 服务）。
        services.TryAddSingleton<ITagsStreamHub>(
            sp => sp.GetRequiredService<TagsStreamRegistry>()
                    .GetRequired(null)
                    .HostedService
        );

        return services;
    }

    /// <summary>
    /// 注册一路<b>具名</b>观测流：观测由 <c>AddKeyedTagsProjectCtrl(key)</c> 注册的 <see cref="ITagsProjectCtrl"/>，
    /// 端点落在 <c>{prefix}/{key}/tree|stream|value|rescan|ui</c>（监控页各一份）。<br/>
    /// 可与 <see cref="AddTagsStream"/> 并存；同一个 key 只能注册一路。启动顺序等约束同 <see cref="AddTagsStream"/>。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="projctrlKey">
    /// 控制器的注册键，同时作为路由里的 <c>{keyed}</c> 段：允许字母/数字/<c>.</c>/<c>-</c>/<c>_</c>，
    /// 且不能是 <c>ui</c>/<c>designer</c>/<c>tree</c>/<c>stream</c>/<c>value</c>/<c>rescan</c>/<c>catalog</c>（会与不具名流的端点撞车）。
    /// </param>
    /// <param name="configure">这一路流的配置回调（每次调用各一份，互不影响）</param>
    /// <returns></returns>
    public static IServiceCollection AddKeyedTagsStream( this IServiceCollection services, string projctrlKey, Action<TagsStreamOptions>? configure = null) 
    {
        ArgumentException.ThrowIfNullOrEmpty(projctrlKey);
        ValidateStreamKey(services, projctrlKey);

        var options = AddTagsStreamCommon(services, configure);

        services.AddSingleton(new TagsStreamDescriptor(projctrlKey, options));

        // 同一实例注册为 IHostedService / keyed ITagsStreamHub（原因见 AddTagsStream 里的注释）。
        services.AddSingleton<IHostedService>(
            sp => sp.GetRequiredService<TagsStreamRegistry>()
                    .GetRequired(projctrlKey)
                    .HostedService
        );
        services.AddKeyedSingleton<ITagsStreamHub>(
            projctrlKey,
            (sp, _) => sp.GetRequiredService<TagsStreamRegistry>()
                        .GetRequired(projctrlKey)
                        .HostedService
        );

        return services;
    }

    /// <summary>
    /// 两条注册路径的公共部分：建这一路的 <see cref="TagsStreamOptions"/> 并保证 registry 存在。<br/>
    /// 返回这份 options —— 它要交给 <see cref="TagsStreamDescriptor"/>，所以不能在两个公开方法里各建一份。
    /// </summary>
    private static TagsStreamOptions AddTagsStreamCommon(IServiceCollection services, Action<TagsStreamOptions>? configure)
    {
        var options = new TagsStreamOptions();
        configure?.Invoke(options);

        // 每路流都有自己的一份 options；这里只登记第一条（服务面上仍可解析到它）。
        services.TryAddSingleton(options);
        services.TryAddSingleton<TagsStreamRegistry>();

        // 设计器元数据（{prefix}/catalog）与具体流无关：核心 schema + 各驱动注册的
        // ITagsProjectSchemaProvider，解析时一次性内省并缓存。
        services.TryAddSingleton<DesignerCatalog>();
        return options;
    }

    /// <summary>
    /// 校验 URL 段形式的 key：只含 URL 安全字符、不与不具名流的端点名撞车、不重复。
    /// </summary>
    private static void ValidateStreamKey(IServiceCollection services, string key)
    {
        foreach (var c in key)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            {
                throw new ArgumentException(
                    $"观测流的 key \"{key}\" 只能包含字母/数字/'.'/'-'/'_'：它会直接成为 URL 的一段。",
                    nameof(key));
            }
        }

        if (ReservedStreamKeys.Contains(key))
        {
            throw new ArgumentException(
                $"观测流的 key \"{key}\" 是保留字：会与不具名流的端点（{string.Join(" / ", ReservedStreamKeys)}）撞车。",
                nameof(key));
        }

        if (services.Any(d => d.ServiceType == typeof(TagsStreamDescriptor)
            && ((TagsStreamDescriptor)d.ImplementationInstance!).Key == key))
        {
            throw new ArgumentException(
                $"观测流的 key \"{key}\" 已经注册过：每个 key 只能对应一路流。",
                nameof(key));
        }
    }

    private static readonly HashSet<string> ReservedStreamKeys =
        new(new[] { "ui", "designer", "tree", "stream", "value", "rescan", "catalog" }, StringComparer.OrdinalIgnoreCase);
}

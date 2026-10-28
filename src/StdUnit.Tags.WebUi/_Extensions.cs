using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StdUnit.Tags.WebUi.Internal;


namespace StdUnit.Tags.WebUi;

/// <summary>
/// 只读观测流的入口：<see cref="AddTagsStream"/> 做 DI 注册，<see cref="MapTagsStream"/> 映射端点。
/// </summary>
public static class TagsStreamServiceCollection
{
    /// <summary>
    /// 注册<b>不具名</b>那一路观测流：观测由 <c>AddTagsProjectServices</c> 注册的 <see cref="ITagsProjectCtrl"/>，
    /// 端点落在 <c>{prefix}/tree|stream|value|rescan|ui</c>。<br/>
    /// 一个容器里只能有一路不具名流；要多路请用 <see cref="AddKeyedTagsStream"/>。<br/>
    /// <br/>
    /// <b>启动顺序</b>：hub 以 <see cref="IHostedService"/> 注册，随宿主一起启动。它需要在<b>项目开始轮询之前</b>
    /// 拿到 <see cref="ITagsProject"/> 才能挂上"轮次完成"回调，所以请让轮询在宿主启动之后才开始
    /// （例如在请求里/更晚注册的 hosted service 里 <c>StartPollAsync</c>）。
    /// 若 hub 启动时项目已在运行，结构树仍可观测，但值观测会缺失并记一条 Warning。
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
    /// 且不能是 <c>ui</c>/<c>tree</c>/<c>stream</c>/<c>value</c>/<c>rescan</c>（会与不具名流的端点撞车）。
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
        new(new[] { "ui", "tree", "stream", "value", "rescan" }, StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// 映射端点（全部挂在 <paramref name="prefix"/> 下，相对路径固定）：<br/>
    /// <b>不具名流</b>（<c>AddTagsStream()</c>）：
    /// - <c>GET {prefix}/tree</c>：结构树（一次性 JSON，按 epoch 缓存）；<br/>
    /// - <c>GET {prefix}/stream?entries=a,b</c>：值流（<c>text/event-stream</c>），<c>entries</c> 省略表示全部入口；<br/>
    /// - <c>GET {prefix}/ui</c>：内置的极简监控页（单文件 HTML + 内嵌 JS，无需 npm 工程）；<br/>
    /// - <c>POST {prefix}/value</c>：改测点值；<br/>
    /// - <c>POST {prefix}/rescan</c>：标记未扫描。<br/>
    /// <b>具名流</b>（<c>AddKeyedTagsStream("dev")</c>）：同一套形状，键作为一段路径，
    /// 即 <c>{prefix}/{key}/tree</c>、<c>{prefix}/{key}/stream</c> ……；未知 key 返回 404。<br/>
    /// <br/>
    /// 外部只可改<b>前缀</b>：各端点的相对路径（<c>/tree</c>、<c>/stream</c> …）是线协议的一部分，
    /// 监控页注入的也是这些相对路径，真正的 URL 由页面里的 <c>&lt;base href&gt;</c> 解析；
    /// 而 base 是按<b>请求路径</b>生成的，所以具名流的页面自动指向 <c>{prefix}/{key}/…</c>，
    /// 把本组挂到别的路由组下（<c>app.MapGroup("/monitor").MapTagsStream()</c>）或配了 <c>UsePathBase</c> 也都正常。
    /// <br/>
    /// <b>鉴权</b>：这些端点<b>不内置任何鉴权</b>——两个写端点会把任意测点改成任意值。
    /// 返回值是路由组，映射完直接对整组加策略即可：
    /// <code>
    /// app.MapTagsStream().RequireAuthorization();                        // 整组走默认策略
    /// app.MapTagsStream("/internal/tags").RequireAuthorization("ops");   // 自定义前缀 + 命名策略
    /// </code>
    /// 只想让读端点匿名时，可用反向代理挡住 <c>{prefix}/value</c> 与 <c>{prefix}/rescan</c>。
    /// </summary>
    /// <param name="endpoints">端点构建器</param>
    /// <param name="prefix">
    /// 路由前缀，默认 <see cref="EndpointConstants.DefaultPrefix"/>；
    /// 传 <c>""</c> 或 <c>"/"</c> 表示不挂前缀（端点直接落在根上）。
    /// </param>
    /// <returns>路由组：可继续 <c>RequireAuthorization()</c> / <c>WithTags()</c> 等整组配置。</returns>
    public static RouteGroupBuilder MapTagsStream(this IEndpointRouteBuilder endpoints, string prefix = EndpointConstants.DefaultPrefix)
    {
        var group = endpoints.MapGroup(prefix.Length == 0 ? "/" : prefix);

        // 不具名流：{prefix}/tree|stream|value|rescan|ui
        group.MapGet(EndpointConstants.TreePath, TreeEndpoint.HandleAsync);
        group.MapGet(EndpointConstants.StreamPath, StreamEndpoint.HandleAsync);
        group.MapPost(EndpointConstants.WriteValuePath, CommandsEndpoint.WriteValueAsync);
        group.MapPost(EndpointConstants.RescanPath, CommandsEndpoint.RescanAsync);

        // 具名流：{prefix}/{keyed}/…（同形）；未知 key 由端点在运行时返回 404。
        group.MapGet(EndpointConstants.Keyed(EndpointConstants.TreePath), TreeEndpoint.HandleAsync);
        group.MapGet(EndpointConstants.Keyed(EndpointConstants.StreamPath), StreamEndpoint.HandleAsync);
        group.MapPost(EndpointConstants.Keyed(EndpointConstants.WriteValuePath), CommandsEndpoint.WriteValueAsync);
        group.MapPost(EndpointConstants.Keyed(EndpointConstants.RescanPath), CommandsEndpoint.RescanAsync);

        // 页面只注入**相对路径**（与 C# 常量同源），基路径按请求路径生成：
        //     {prefix}/ui → base={prefix}/，
        //     {prefix}/{key}/ui → base={prefix}/{key}/，
        // 所以一份页面服务所有流。
        var page = TagsStreamUi.BuildPage(
            EndpointConstants.TreePath,
            EndpointConstants.StreamPath,
            EndpointConstants.WriteValuePath,
            EndpointConstants.RescanPath
        );
        group.MapGet(EndpointConstants.UiPath, context => TagsStreamUi.WriteAsync(context, page));
        group.MapGet(EndpointConstants.Keyed(EndpointConstants.UiPath), context => TagsStreamUi.WriteAsync(context, page));

        return group;
    }
}

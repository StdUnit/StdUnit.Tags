using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi.Internal;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// extensions for Endpoints
/// </summary>
public static class TagsStreamExtensions_Endpoints
{
    /// <summary>
    /// 映射端点（全部挂在 <paramref name="prefix"/> 下，相对路径固定）：<br/>
    /// <b>不具名流</b>（<c>AddTagsStream()</c>）：
    /// - <c>GET {prefix}/tree</c>：结构树（一次性 JSON，按 epoch 缓存）；<br/>
    /// - <c>GET {prefix}/stream?entries=a,b</c>：值流（<c>text/event-stream</c>），<c>entries</c> 省略表示全部入口；<br/>
    /// - <c>GET {prefix}/ui</c>：内置的极简监控页（单文件 HTML + 内嵌 JS，无需 npm 工程）；<br/>
    /// - <c>GET {prefix}/designer</c>：内置的通道/测点设计器（同一套单文件页面，导出项目 XML）；<br/>
    /// - <c>GET {prefix}/catalog</c>：设计器元数据（核心 + 驱动 schema 的内省结果）；<br/>
    /// - <c>POST {prefix}/value</c>：改测点值；<br/>
    /// - <c>POST {prefix}/rescan</c>：标记未扫描。<br/>
    /// <b>具名流</b>（<c>AddKeyedTagsStream("dev")</c>）：同一套形状，键作为一段路径，
    /// 即 <c>{prefix}/{key}/tree</c>、<c>{prefix}/{key}/stream</c> ……；未知 key 返回 404。
    /// （<c>designer</c> / <c>catalog</c> 与具体流无关，只有不具名的一份。）<br/>
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
    /// <br/>
    /// <b>设计器元数据</b>：<c>GET {prefix}/catalog</c> 把核心 + 驱动 schema 内省成 JSON，
    /// <c>GET {prefix}/designer</c> 就是用它渲染的设计器页面。两者都与具体观测流无关，
    /// 只有这一份、没有具名版本；只读，不涉及项目数据。
    /// <br/>
    /// </summary>
    /// <param name="endpoints">端点构建器</param>
    /// <param name="prefix">
    /// 路由前缀，默认 <see cref="EndpointConstants.DefaultPrefix"/>；
    /// 传 <c>""</c> 或 <c>"/"</c> 表示不挂前缀（端点直接落在根上）。
    /// </param>
    /// <returns>路由组：可继续 <c>RequireAuthorization()</c> / <c>WithTags()</c> 等整组配置。</returns>
    public static RouteGroupBuilder MapTagsStream(this IEndpointRouteBuilder endpoints, string prefix = EndpointConstants.DefaultPrefix)
    {
        // 观测流的**唯一启动点**：映射端点必然早于任何请求，也早于 StartPollAsync，
        // 所以 hub 不需要注册成 IHostedService，也不依赖 Generic Host 的启动顺序。
        // 只 map 不 AddTagsStream 时是 no-op（端点会在运行时回 404）。
        endpoints.ServiceProvider.GetService<TagsStreamRegistry>()?.StartAll();

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
            EndpointConstants.RescanPath,
            EndpointConstants.DesignerPath
        );
        group.MapGet(EndpointConstants.UiPath, context => TagsStreamUi.WriteAsync(context, page));
        group.MapGet(EndpointConstants.Keyed(EndpointConstants.UiPath), context => TagsStreamUi.WriteAsync(context, page));

        // 设计器：与具体流无关（schema 是全局的），所以既没有具名版本，也不需要 {keyed}。
        group.MapGet(EndpointConstants.CatalogPath, DesignerCatalogEndpoint.HandleAsync);
        var designerPage = DesignerUi.BuildPage(EndpointConstants.CatalogPath, EndpointConstants.UiPath);
        group.MapGet(EndpointConstants.DesignerPath, context => DesignerUi.WriteAsync(context, designerPage));

        return group;
    }
}

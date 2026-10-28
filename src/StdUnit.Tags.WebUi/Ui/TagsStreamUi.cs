using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 内置监控页：把 <c>Ui/index.html</c> 作为嵌入资源取出，注入 <c>&lt;base href&gt;</c> 后整体返回。<br/>
/// 页面里的相对 URL（<c>tree</c> / <c>stream</c> / <c>value</c> / <c>rescan</c>）都相对这个基路径解析，
/// 所以端点挂在什么前缀下、被套进几层路由组，前端都不用改。<br/>
/// 不用静态文件中间件、不依赖宿主配置，也不引入任何前端工程。
/// </summary>
internal static class TagsStreamUi
{
    private const string ResourceName = "StdUnit.Tags.WebUi.Ui.index.html";
    private const string BasePlaceholder = "%%BASE%%";
    private const string TreePlaceholder = "%%TREE_URL%%";
    private const string StreamPlaceholder = "%%STREAM_URL%%";
    private const string WritePlaceholder = "%%WRITE_URL%%";
    private const string RescanPlaceholder = "%%RESCAN_URL%%";

    /// <summary>嵌入资源原文（含全部占位符），只读一次。</summary>
    private static readonly string Template = LoadTemplate();

    /// <summary>
    /// 渲染监控页：只填<b>相对路径</b>（来自 <see cref="EndpointConstants"/> 的 <c>*Path</c>），
    /// <b>不</b>拼前缀——前缀/挂载点由请求决定，见 <see cref="WriteAsync"/>。<br/>
    /// 页面里 JS 再用 <c>new URL(相对路径, document.baseURI)</c> 算出真正的地址。
    /// </summary>
    public static string BuildPage(
        string treePath,
        string streamPath,
        string writeValuePath,
        string rescanPath)
    {
        return Template
            .Replace(TreePlaceholder, ToRelative(treePath), StringComparison.Ordinal)
            .Replace(StreamPlaceholder, ToRelative(streamPath), StringComparison.Ordinal)
            .Replace(WritePlaceholder, ToRelative(writeValuePath), StringComparison.Ordinal)
            .Replace(RescanPlaceholder, ToRelative(rescanPath), StringComparison.Ordinal);
    }

    /// <summary>
    /// 路由 pattern 是 <c>/tree</c> 这种以 <c>/</c> 开头的形式；页面的相对 URL 不能带它——
    /// 否则 <c>new URL('/tree', base)</c> 会连同 base 里的前缀一起丢掉，直接落到站点根上。
    /// </summary>
    private static string ToRelative(string path) => path.TrimStart('/');

    /// <summary>
    /// 把渲染好的页面写进响应，并按<b>本次请求的路径</b>填 <c>&lt;base href&gt;</c>。<br/>
    /// 基路径 = <see cref="HttpRequest.PathBase"/> + <see cref="HttpRequest.Path"/> 去掉最后一段
    /// （<c>/stdunit.tags/ui</c> → <c>/stdunit.tags/</c>），而不是映射时传进来的前缀——只有请求路径才等于真正生效的基路径
    /// （嵌套路由组 / <c>UsePathBase</c> 都在里面）。
    /// </summary>
    public static async Task WriteAsync(HttpContext context, string page)
    {
        var html = page.Replace(BasePlaceholder, GetBaseHref(context), StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(html);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    /// <summary>
    /// 页面自己的 URL 去掉最后一段就是同级端点所在的目录：<c>/stdunit.tags/ui</c> → <c>/stdunit.tags/</c>。<br/>
    /// 末尾多余的 <c>/</c> 先去掉，免得把 <c>/ui/</c> 当成目录。
    /// </summary>
    private static string GetBaseHref(HttpContext context)
    {
        var path = context.Request.PathBase.Add(context.Request.Path).Value ?? "/";
        path = path.TrimEnd('/');

        var slash = path.LastIndexOf('/');
        var basePath = slash < 0 ? "/" : path[..(slash + 1)];

        // 合法 URL 里不会出现需要转义的字符，但它是属性值：万一路径段里解码出 '&' 之类也不至于破坏页面。
        return HtmlEncoder.Default.Encode(basePath);
    }

    private static string LoadTemplate()
    {
        using var stream = typeof(TagsStreamUi).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"嵌入资源缺失：{ResourceName}");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

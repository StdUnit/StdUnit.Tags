using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 内置页面的公共部分（监控页与设计器共用）：模板里的 <c>%%BASE%%</c> 按<b>本次请求路径</b>填
/// <c>&lt;base href&gt;</c>，页面里的相对 URL 再由浏览器按它解析。<br/>
/// 这样端点挂在什么前缀下、被套进几层路由组、有没有 <c>UsePathBase</c>，前端都不用改。
/// </summary>
internal static class WebPage
{
    /// <summary>页面模板里的基路径占位符。</summary>
    public const string BasePlaceholder = "%%BASE%%";

    /// <summary>
    /// 把渲染好的页面写进响应，并按本次请求的路径填 <c>&lt;base href&gt;</c>。<br/>
    /// 基路径 = <see cref="HttpRequest.PathBase"/> + <see cref="HttpRequest.Path"/> 去掉最后一段
    /// （<c>/stdunit.tags/designer</c> → <c>/stdunit.tags/</c>），而不是映射时传进来的前缀——
    /// 只有请求路径才等于真正生效的基路径（嵌套路由组 / <c>UsePathBase</c> 都在里面）。
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
    public static string GetBaseHref(HttpContext context)
    {
        var path = context.Request.PathBase.Add(context.Request.Path).Value ?? "/";
        path = path.TrimEnd('/');

        var slash = path.LastIndexOf('/');
        var basePath = slash < 0 ? "/" : path[..(slash + 1)];

        // 合法 URL 里不会出现需要转义的字符，但它是属性值：万一路径段里解码出 '&' 之类也不至于破坏页面。
        return HtmlEncoder.Default.Encode(basePath);
    }

    /// <summary>取出程序集里嵌入的页面模板（只读一次）。</summary>
    public static string LoadTemplate(string resourceName)
    {
        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入资源缺失：{resourceName}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

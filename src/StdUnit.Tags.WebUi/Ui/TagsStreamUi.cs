using System.Text;
using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 内置监控页：把 <c>Ui/index.html</c> 作为嵌入资源取出，注入 <c>&lt;base href&gt;</c> 后整体返回。<br/>
/// 页面里的相对 URL（<c>tree</c> / <c>stream</c> / <c>value</c> / <c>rescan</c> / <c>designer</c>）都相对这个基路径解析，
/// 所以端点挂在什么前缀下、被套进几层路由组，前端都不用改。<br/>
/// 不用静态文件中间件、不依赖宿主配置，也不引入任何前端工程。
/// </summary>
internal static class TagsStreamUi
{
    private const string ResourceName = "StdUnit.Tags.WebUi.Ui.index.html";
    private const string TreePlaceholder = "%%TREE_URL%%";
    private const string StreamPlaceholder = "%%STREAM_URL%%";
    private const string WritePlaceholder = "%%WRITE_URL%%";
    private const string RescanPlaceholder = "%%RESCAN_URL%%";
    private const string DesignerPlaceholder = "%%DESIGNER_URL%%";

    /// <summary>嵌入资源原文（含全部占位符），只读一次。</summary>
    private static readonly string Template = WebPage.LoadTemplate(ResourceName);

    /// <summary>
    /// 渲染监控页：只填<b>相对路径</b>（来自 <see cref="EndpointConstants"/> 的 <c>*Path</c>），
    /// <b>不</b>拼前缀——前缀/挂载点由请求决定，见 <see cref="WebPage.WriteAsync"/>。<br/>
    /// 页面里 JS 再用 <c>new URL(相对路径, document.baseURI)</c> 算出真正的地址。
    /// </summary>
    public static string BuildPage(
        string treePath,
        string streamPath,
        string writeValuePath,
        string rescanPath,
        string designerPath)
    {
        return Template
            .Replace(TreePlaceholder, ToRelative(treePath), StringComparison.Ordinal)
            .Replace(StreamPlaceholder, ToRelative(streamPath), StringComparison.Ordinal)
            .Replace(WritePlaceholder, ToRelative(writeValuePath), StringComparison.Ordinal)
            .Replace(RescanPlaceholder, ToRelative(rescanPath), StringComparison.Ordinal)
            .Replace(DesignerPlaceholder, ToRelative(designerPath), StringComparison.Ordinal);
    }

    /// <summary>
    /// 路由 pattern 是 <c>/tree</c> 这种以 <c>/</c> 开头的形式；页面的相对 URL 不能带它——
    /// 否则 <c>new URL('/tree', base)</c> 会连同 base 里的前缀一起丢掉，直接落到站点根上。
    /// </summary>
    private static string ToRelative(string path) => path.TrimStart('/');

    /// <summary>把渲染好的页面写进响应（基路径由 <see cref="WebPage.WriteAsync"/> 按请求路径填）。</summary>
    public static Task WriteAsync(HttpContext context, string page) => WebPage.WriteAsync(context, page);
}

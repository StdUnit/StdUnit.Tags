using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 内置设计器页：<c>Ui/designer.html</c> 作为嵌入资源，注入 <c>&lt;base href&gt;</c> 与 catalog 的相对路径后返回。<br/>
/// 页面只做前端组装（通道 + 测点树 + Logicet），字段从哪里来/长什么样完全由
/// <see cref="DesignerCatalogEndpoint"/> 给的元数据决定——所以"新增一个驱动"不需要改这个页面。
/// </summary>
internal static class DesignerUi
{
    private const string ResourceName = "StdUnit.Tags.WebUi.Ui.designer.html";
    private const string CatalogPlaceholder = "%%CATALOG_URL%%";
    private const string MonitorPlaceholder = "%%MONITOR_URL%%";

    /// <summary>嵌入资源原文（含全部占位符），只读一次。</summary>
    private static readonly string Template = WebPage.LoadTemplate(ResourceName);

    /// <summary>
    /// 渲染设计器页：只填<b>相对路径</b>（与 <see cref="EndpointConstants"/> 同源），前缀由请求决定。
    /// </summary>
    public static string BuildPage(string catalogPath, string monitorPath) =>
        Template
            .Replace(CatalogPlaceholder, catalogPath.TrimStart('/'), StringComparison.Ordinal)
            .Replace(MonitorPlaceholder, monitorPath.TrimStart('/'), StringComparison.Ordinal);

    /// <summary>把渲染好的页面写进响应（基路径由 <see cref="WebPage.WriteAsync"/> 按请求路径填）。</summary>
    public static Task WriteAsync(HttpContext context, string page) => WebPage.WriteAsync(context, page);
}

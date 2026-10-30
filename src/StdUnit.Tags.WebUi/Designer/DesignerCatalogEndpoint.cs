using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 设计器元数据端点（<c>GET {prefix}/catalog</c>）：把核心 + 驱动 schema 内省成一份 JSON，
/// 供通道/测点设计器渲染表单与导出 XML。<br/>
/// 与其它端点的区别：它<b>不针对某一路观测流</b>（schema 与项目无关），所以既没有不具名/具名之分，
/// 也不需要路由里的 <c>{keyed}</c>。
/// </summary>
internal static class DesignerCatalogEndpoint
{
    public static async Task HandleAsync(HttpContext context)
    {
        var catalog = context.RequestServices.GetRequiredService<DesignerCatalog>().Catalog;

        // 自己序列化：不走 MVC / 无参序列化，AOT/裁剪下没有反射路径。
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, TagsStreamJsonContext.Default.DesignerCatalogDto);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 结构树端点（<c>GET /stdunit.tags/tree</c>）：一次性返回扁平结构树。
/// </summary>
internal static class TreeEndpoint
{
    public static async Task HandleAsync(HttpContext context)
    {
        var entry = context.GetKeyedTagsStreamEntry();
        if (entry is null)
        {
            await context.WriteKeyedStreamNotFoundAsync();
            return;
        }

        // 自己序列化：不走 MVC / 无参序列化，AOT/裁剪下没有反射路径。
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry.HostedService.GetTree(), TagsStreamJsonContext.Default.TreeDto);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}

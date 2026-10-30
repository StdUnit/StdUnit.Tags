using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace StdUnit.Tags.WebUi.Internal;

/// <summary>
/// 值流端点（<c>GET /stdunit.tags/stream?entries=a,b</c>）：<c>text/event-stream</c>，<c>entries</c> 省略表示全部入口。
/// </summary>
internal static class StreamEndpoint
{
    public static async Task HandleAsync(HttpContext context)
    {
        var entry = context.GetKeyedTagsStreamEntry();
        if (entry is null)
        {
            await context.WriteKeyedStreamNotFoundAsync();
            return;
        }

        // 反代/服务器不能缓冲：否则"实时推送"会变成"攒一波再发"。
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache, no-transform";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // nginx

        var entries = ParseEntries(context.Request.Query["entries"]);
        using var piper = entry.HostedService.Subscribe(entries);

        try
        {
            await piper.PipeFramesToStreamAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开/请求中止
        }
    }

    /// <summary>
    /// 解析 <c>?entries=a,b</c>：逗号分隔、去空白、去重、忽略空段。<br/>
    /// 返回 <c>null</c> 表示"订阅全部入口"（参数缺失 / 全是空白 / 全是空段）。<br/>
    /// 这是 HTTP 线上的写法，所以留在端点这一层，hub 只收 <see cref="HashSet{T}"/>。
    /// </summary>
    internal static HashSet<string>? ParseEntries(string? entries)
    {
        if (string.IsNullOrWhiteSpace(entries))
        {
            return null;
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in entries.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result.Count == 0 ? null : result;
    }
}

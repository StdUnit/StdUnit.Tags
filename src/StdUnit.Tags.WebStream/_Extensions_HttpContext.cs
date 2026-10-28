using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 端点层的 HTTP 适配：<see cref="HttpContext"/> / 路由值 / 404 响应都留在这里，
/// <see cref="TagsStreamRegistry"/> 只认 key，不认识 HTTP。
/// </summary>
internal static class TagsStreamEndpointExtensions
{
    /// <summary>
    /// 取当前请求对应的那一路观测流：路由里有 <c>{keyed}</c> 就按它找，否则用不具名那一路。<br/>
    /// 找不到返回 <c>null</c>（由调用方决定响应，通常是 <see cref="WriteKeyedStreamNotFoundAsync"/>）。
    /// </summary>
    public static TagsStreamEntry? GetKeyedTagsStreamEntry(this HttpContext context) =>
        context.RequestServices
            .GetRequiredService<TagsStreamRegistry>()
            .Get(context.Request.RouteValues[EndpointConstants.KeyedRouteValue] as string);

    /// <summary>请求里的 key 没有对应的观测流（或压根没注册不具名那一路）时返回 404。</summary>
    public static Task WriteKeyedStreamNotFoundAsync(this HttpContext context)
    {
        var key = context.Request.RouteValues[EndpointConstants.KeyedRouteValue] as string;

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(
            key is null
                ? "未注册不具名观测流（AddTagsStream 未调用）。"
                : $"未注册 key = \"{key}\" 的观测流。",
            context.RequestAborted);
    }
}

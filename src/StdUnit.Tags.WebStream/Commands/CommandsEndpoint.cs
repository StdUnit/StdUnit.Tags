using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 写命令端点：改测点值（<c>POST /stdunit.tags/value</c>）、标记未扫描（<c>POST /stdunit.tags/rescan</c>）。
/// </summary>
internal static class CommandsEndpoint
{
    public static async Task WriteValueAsync(HttpContext context)
    {
        var request = await ReadBodyAsync(context, TagsStreamJsonContext.Default.WriteTagValueRequest)
            .ConfigureAwait(false);
        if (request is null)
        {
            await WriteResultAsync(context, CommandOutcome.BadRequest("请求体不是合法的 JSON")).ConfigureAwait(false);
            return;
        }

        var ctrl = context.RequestServices.GetRequiredService<ITagsProjectCtrl>();
        var options = context.RequestServices.GetRequiredService<TagsStreamOptions>();
        var outcome = await TagsStreamCommands
            .WriteValueAsync(ctrl, request, options.CommandTimeout)
            .ConfigureAwait(false);

        await WriteResultAsync(context, outcome).ConfigureAwait(false);
    }

    public static async Task RescanAsync(HttpContext context)
    {
        var request = await ReadBodyAsync(context, TagsStreamJsonContext.Default.RescanTagRequest)
            .ConfigureAwait(false);
        if (request is null)
        {
            await WriteResultAsync(context, CommandOutcome.BadRequest("请求体不是合法的 JSON")).ConfigureAwait(false);
            return;
        }

        var ctrl = context.RequestServices.GetRequiredService<ITagsProjectCtrl>();
        var options = context.RequestServices.GetRequiredService<TagsStreamOptions>();
        var outcome = await TagsStreamCommands.RescanAsync(ctrl, request, options.CommandTimeout).ConfigureAwait(false);

        await WriteResultAsync(context, outcome).ConfigureAwait(false);
    }

    private static async Task WriteResultAsync(HttpContext context, CommandOutcome outcome)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new CommandResult(outcome.Error is null, outcome.Error),
            TagsStreamJsonContext.Default.CommandResult);

        context.Response.StatusCode = outcome.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpContext context, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return await JsonSerializer
                .DeserializeAsync(context.Request.Body, typeInfo, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

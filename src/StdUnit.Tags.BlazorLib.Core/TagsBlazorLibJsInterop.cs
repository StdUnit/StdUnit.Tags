using Microsoft.JSInterop;

namespace StdUnit.Tags.BlazorLib;

/// <summary>
/// interop with JavaScript for TagsBlazorLib
/// </summary>
public class TagsBlazorLibJsInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="jsRuntime"></param>
    public TagsBlazorLibJsInterop(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/StdUnit.Tags.BlazorLib/tags.JsInterop.js").AsTask());
    }


    /// <summary>
    /// Prompt a message to the user and return the input string.
    /// </summary>
    /// <param name="message">The message to display in the prompt.</param>
    /// <returns>The input string from the user.</returns>
    public async ValueTask<string> Prompt(string message)
    {
        var module = await moduleTask.Value;
        return await module.InvokeAsync<string>("showPrompt", message);
    }

    /// <summary>
    /// Download a text file with the specified filename and content.
    /// </summary>
    /// <param name="filename">The name of the file to download.</param>
    /// <param name="text">The content of the file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask DownloadTextFile(string filename, string text)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("downloadTextFile", new[] { filename, text });
    }

    /// <summary>
    /// Dispose
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}

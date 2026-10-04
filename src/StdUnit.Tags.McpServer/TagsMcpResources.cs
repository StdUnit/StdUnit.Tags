using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

namespace StdUnit.Tags.McpServer;

/// <summary>
/// 基于 StdUnit.Tags 的 MCP 提示集，提供对已运行项目的测点读写操作的提示信息。
/// </summary>
public class TagsMcpResources
{
    private const string ToolGuideUri = "docs://stdunit.tags/tool_guide";
    private const string ToolGuideResourceNameSuffix = ".guide.md";

    /// <summary>
    /// 在 MCP 服务器中获取工具使用指南。
    /// </summary>
    /// <returns></returns>
    [McpServerResource(Name = "stdunit_tags_tool_guide", Title = "指导如何使用核心工具以及背后的原理", UriTemplate = ToolGuideUri, MimeType = "text/markdown")]
    public static async Task<TextResourceContents> GetToolGuideAsync()
    {
        var assembly = typeof(TagsMcpResources).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(ToolGuideResourceNameSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded MCP guide resource 'guide.md' was not found in the assembly.");

        // 用同步 using 而非 await using：net472 的 Stream 不实现 IAsyncDisposable
        // （该接口是 netstandard2.1 / .NET Core 3.0+；net472 需额外引入 Microsoft.Bcl.AsyncInterfaces）。
        // 这里是一次性的内嵌资源读取，同步释放语义完全等价。
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Failed to open embedded MCP guide resource '{resourceName}'.");

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        return new TextResourceContents
        {
            Uri = ToolGuideUri,
            MimeType = "text/markdown",
            Text = content
        };
    }
}


using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace StdUnit.Tags.McpServer;

/// <summary>
/// Extension methods 
/// </summary>
public static class TagsMcpServerServiceExtensions
{
    /// <summary>
    /// 注册MCP工具集 <see cref="TagsMcpServerTools"/> 到MCP服务器中，使其可用于远程调用。
    /// </summary>
    public static IMcpServerBuilder AddTagsMcp(this IMcpServerBuilder builder)
    {
        // Register the tool type with the MCP server
        builder.WithTools<TagsMcpServerTools>();
        builder.WithResources<TagsMcpResources>();

        return builder;
    }
}

using StdUnit.Tags.BlazorLib;
using StdUnit.Tags.McpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WpfDemo.Tags;

namespace WpfDemo;

public static class STARTUP
{
    public static void ConfigureServies(this IServiceCollection services)
    {
        services.AddLogging();
        //services.AddSerilog((sp, lc) => lc
        //    .ReadFrom.Services(sp)
        //    .WriteTo.Console()
        //    .WriteTo.File("logs/log.txt", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
        //    .Enrich.FromLogContext()
        //);
        services.AddAntiforgery();
        services.AddWpfDemoTags();

        services.AddMcpServer()
            .WithHttpTransport(opts =>
            {
                opts.Stateless = true;
            })
            .AddTagsMcp();

        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddTagsBlazorLibCore();
        services.AddAuthentication();
    }

    public static void ConfigureMiddlewares(this WebApplication app)
    {
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAntiforgery();
        app.MapRazorComponents<StdUnit.Tags.BlazorLib.Apps.TagsApp>()
            .AddInteractiveServerRenderMode();
        app.MapMcp("/mcp");
    }
}

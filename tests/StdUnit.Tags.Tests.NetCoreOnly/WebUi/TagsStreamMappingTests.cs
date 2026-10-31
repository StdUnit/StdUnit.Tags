using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebUi;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

public class TagsStreamMappingTests
{
    private static (WebApplication App, IEndpointRouteBuilder Builder) CreateApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        return (app, app);
    }

    private static string[] Patterns(IEndpointRouteBuilder builder) =>
        builder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText!)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToArray();

    [Theory]
    [InlineData("/stdunit.tags")]
    [InlineData("/monitor")]
    [InlineData("/stdunit.tags/")]
    [InlineData("/")]
    [InlineData("")]
    public async Task MapTagsStream_PutsEveryEndpointUnderThePrefix(string prefix)
    {
        var (app, builder) = CreateApp();
        try
        {
            builder.MapTagsStream(prefix);

            var normalized = prefix.TrimEnd('/');
            var expected = new[]
            {
                // 不具名流（含与流无关的 catalog / designer）
                $"{normalized}/catalog",
                $"{normalized}/designer",
                $"{normalized}/rescan",
                $"{normalized}/stream",
                $"{normalized}/tree",
                $"{normalized}/ui",
                $"{normalized}/value",
                // 具名流（同一套形状，多一段 {keyed}）
                $"{normalized}/{{keyed}}/rescan",
                $"{normalized}/{{keyed}}/stream",
                $"{normalized}/{{keyed}}/tree",
                $"{normalized}/{{keyed}}/ui",
                $"{normalized}/{{keyed}}/value",
            };

            Assert.Equal(
                expected.OrderBy(p => p, System.StringComparer.Ordinal),
                Patterns(builder));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// <c>MapTagsStream</c> 是观测流的唯一启动点：映射之后（不需要宿主启动、也不需要任何请求）
    /// hub 就已经在观测，并且能补上"映射之前项目就已经在跑"的状态。
    /// </summary>
    [Fact]
    public async Task MapTagsStream_StartsEveryHub()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        var unnamed = new FakeProjectCtrl();
        var named = new FakeProjectCtrl();
        builder.Services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        builder.Services.AddSingleton<ITagsProjectCtrl>(unnamed);   // 后注册的覆盖默认实现
        builder.Services.AddKeyedSingleton<ITagsProjectCtrl>("dev", named);
        builder.Services.AddTagsStream();
        builder.Services.AddKeyedTagsStream("dev");

        using var app = builder.Build();
        using var project = app.Services.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a"))));

        // 项目先跑起来，之后才挂端点。
        unnamed.RaiseStarted(project);
        named.RaiseStarted(project);

        app.MapTagsStream();   // ← 唯一启动点

        var hub = app.Services.GetRequiredService<ITagsStreamHub>();
        Assert.True(hub.GetTree().IsStarted);
        Assert.NotEmpty(hub.GetTree().Nodes);

        var devHub = app.Services.GetRequiredKeyedService<ITagsStreamHub>("dev");
        Assert.True(devHub.GetTree().IsStarted);
        Assert.NotEmpty(devHub.GetTree().Nodes);
    }

    [Theory]
    // 路径 → 期望的 <base href>（页面自己的 URL 去掉最后一段）
    [InlineData("/stdunit.tags/ui", "", "/stdunit.tags/")]
    [InlineData("/monitor/ui", "", "/monitor/")]
    [InlineData("/ui", "", "/")]
    // 具名流：{prefix}/{key}/ui → base 自动落到 {prefix}/{key}/
    [InlineData("/monitor/dev/ui", "", "/monitor/dev/")]
    // 末尾多一个 '/' 也不能当成目录
    [InlineData("/monitor/ui/", "", "/monitor/")]
    [InlineData("/monitor/dev/ui/", "", "/monitor/dev/")]
    // PathBase（UsePathBase / 反向代理前缀）必须原样带上
    [InlineData("/stdunit.tags/ui", "/app", "/app/stdunit.tags/")]
    public async Task UiPage_BaseHrefComesFromTheRequestPath(string path, string pathBase, string expectedBase)
    {
        var (app, builder) = CreateApp();
        try
        {
            builder.MapTagsStream("/monitor");

            var ui = Assert.Single(
                builder.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>(),
                e => e.RoutePattern.RawText == "/monitor/ui");

            var context = new DefaultHttpContext { RequestServices = app.Services };
            context.Request.PathBase = pathBase;
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            await ui.RequestDelegate!(context);

            var html = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
            Assert.Contains($"<base href=\"{expectedBase}\" />", html);

            // 页面里只注入相对路径（与 C# 常量同源），真正的 URL 由 JS 相对 <base> 解析。
            Assert.Contains("new URL('tree', document.baseURI).href", html);
            Assert.Contains("new URL('stream', document.baseURI).href", html);
            Assert.DoesNotContain("%%", html);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task DesignerPage_IsServedWithRelativeCatalogAndBase()
    {
        var (app, builder) = CreateApp();
        try
        {
            builder.MapTagsStream("/monitor");

            var designerPage = Assert.Single(
                builder.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>(),
                e => e.RoutePattern.RawText == "/monitor/designer");

            var context = new DefaultHttpContext { RequestServices = app.Services };
            context.Request.Path = "/monitor/designer";
            context.Response.Body = new MemoryStream();
            await designerPage.RequestDelegate!(context);

            var html = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

            Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
            Assert.Contains("<base href=\"/monitor/\" />", html);
            // catalog / 监控页都是相对路径（模板占位符必须全部替换干净）。
            Assert.Contains("new URL('catalog', document.baseURI).href", html);
            Assert.Contains("new URL('ui', document.baseURI).href", html);
            // 四个页签（通道 / 测点 / 导入 / 导出）与导入解析入口都在嵌入资源里。
            Assert.Contains("data-tab=\"channels\"", html);
            Assert.Contains("data-tab=\"tags\"", html);
            Assert.Contains("data-tab=\"import\"", html);
            Assert.Contains("data-tab=\"export\"", html);
            Assert.Contains("parseProjectXml", html);
            Assert.DoesNotContain("%%", html);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task MapTagsStream_CanBeMountedOnAnotherGroup()
    {
        var (app, builder) = CreateApp();
        try
        {
            // 挂到父级路由组下：路由自动带上前缀，监控页的 <base> 由请求路径算出，不需要额外配置。
            builder.MapGroup("/monitor").MapTagsStream();

            var expected = new[]
            {
                "/monitor/stdunit.tags/catalog",
                "/monitor/stdunit.tags/designer",
                "/monitor/stdunit.tags/rescan",
                "/monitor/stdunit.tags/stream",
                "/monitor/stdunit.tags/tree",
                "/monitor/stdunit.tags/ui",
                "/monitor/stdunit.tags/value",
                "/monitor/stdunit.tags/{keyed}/rescan",
                "/monitor/stdunit.tags/{keyed}/stream",
                "/monitor/stdunit.tags/{keyed}/tree",
                "/monitor/stdunit.tags/{keyed}/ui",
                "/monitor/stdunit.tags/{keyed}/value",
            };

            Assert.Equal(
                expected.OrderBy(p => p, System.StringComparer.Ordinal),
                Patterns(builder));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task MapTagsStream_ReturnsAGroupThatCanRequireAuthorization()
    {
        var (app, builder) = CreateApp();
        try
        {
            // 文档里的示例用法：映射完直接对整组加策略。
            builder.MapTagsStream().RequireAuthorization();

            var endpoints = builder.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToArray();
            Assert.Equal(12, endpoints.Length);
            Assert.All(endpoints, e => Assert.Contains(e.Metadata, m => m is IAuthorizeData));

            // 不传 prefix 时必须落在默认前缀下（= EndpointConstants.DefaultPrefix）。
            Assert.Contains(
                $"{EndpointConstants.DefaultPrefix}/tree",
                endpoints.Select(e => e.RoutePattern.RawText));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task MapTagsStream_GroupCanRequireANamedPolicy()
    {
        var (app, builder) = CreateApp();
        try
        {
            // 文档里的第二条示例：自定义前缀 + 命名策略
            builder.MapTagsStream("/internal/tags").RequireAuthorization("ops");

            var endpoints = builder.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToArray();
            Assert.Equal(12, endpoints.Length);
            Assert.All(endpoints, e => Assert.Contains(
                e.Metadata.OfType<IAuthorizeData>(),
                m => m.Policy == "ops"));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}


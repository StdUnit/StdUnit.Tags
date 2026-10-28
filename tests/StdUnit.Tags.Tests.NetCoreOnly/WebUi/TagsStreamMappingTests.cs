using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using StdUnit.Tags.WebUi;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <see cref="TagsStreamServiceCollection.MapTagsStream"/>：对外只能改前缀，相对路径固定；
/// 返回的路由组能整组加鉴权。
/// </summary>
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
                // 不具名流
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
    public async Task MapTagsStream_CanBeMountedOnAnotherGroup()
    {
        var (app, builder) = CreateApp();
        try
        {
            // 挂到父级路由组下：路由自动带上前缀，监控页的 <base> 由请求路径算出，不需要额外配置。
            builder.MapGroup("/monitor").MapTagsStream();

            var expected = new[]
            {
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
            Assert.Equal(10, endpoints.Length);
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
            Assert.Equal(10, endpoints.Length);
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

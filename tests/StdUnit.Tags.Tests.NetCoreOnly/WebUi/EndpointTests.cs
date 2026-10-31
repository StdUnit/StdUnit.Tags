using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// 端点层（HTTP 适配）：直接喂 <see cref="DefaultHttpContext"/>，不起 Kestrel。<br/>
/// 覆盖 <c>/tree</c>、<c>/stream</c>、<c>/catalog</c>、<c>/value</c>、<c>/rescan</c> 的处理函数、
/// "未注册观测流 → 404" 的两条文案，以及这些响应里的 DTO 序列化（源生成的 <c>TagsStreamJsonContext</c>）。
/// </summary>
public class EndpointTests
{
    private static XElement SingleTagProject() => new(
        "Project",
        StreamTestContext.Channel("fake"),
        StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a")),
        // 非入口组里的测点：会被轮询跳过，所以写命令必须拒绝（"不在任何入口内"）。
        new XElement(
            "TagGrp",
            new XAttribute("name", "plain"),
            new XAttribute("isEnabled", "true"),
            new XAttribute("channel", "fake"),
            StreamTestContext.Tag("b")));

    [Fact]
    public async Task Tree_ReturnsEmptyTreeBeforeProjectStarts()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext();
        await TreeEndpoint.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);

        var json = EndpointHost.ReadBody(context);
        Assert.Contains("\"isStarted\":false", json);
        Assert.Contains("\"nodes\":[]", json);
    }

    [Fact]
    public async Task Tree_ReturnsFlatNodesAfterProjectStarts()
    {
        using var host = EndpointHost.Create();
        var hub = host.StartHub();
        using var project = host.StartProject(SingleTagProject());
        try
        {
            var context = host.CreateContext();
            await TreeEndpoint.HandleAsync(context);

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal(context.Response.ContentLength, ((MemoryStream)context.Response.Body).Length);

            var json = EndpointHost.ReadBody(context);
            Assert.Contains("\"isStarted\":true", json);
            Assert.Contains("\"rootName\":", json);
            Assert.Contains("\"depth\":0", json);
            // 三种节点都出现在扁平列表里（grp 的入口标识、叶测点的 kind/address）。
            Assert.Contains("\"kind\":\"grp\"", json);
            Assert.Contains("\"kind\":\"tag\"", json);
            Assert.Contains("\"path\":\"g1/a\"", json);
            Assert.Contains("\"entryKey\":\"g1\"", json);
        }
        finally
        {
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Tree_DescribesTagCbntNodes()
    {
        using var host = EndpointHost.Create();
        var hub = host.StartHub();
        using var project = host.StartProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup(
                "g1",
                new XElement(
                    "TagCbnt",
                    new XAttribute("name", "c"),
                    new XAttribute("address", "0"),
                    StreamTestContext.Tag("x", address: "0"),
                    StreamTestContext.Tag("y", address: "1")))));

        try
        {
            var context = host.CreateContext();
            await TreeEndpoint.HandleAsync(context);

            var json = EndpointHost.ReadBody(context);
            Assert.Contains("\"kind\":\"cbnt\"", json);
            Assert.Contains("\"path\":\"g1/c\"", json);
            Assert.Contains("\"startAddress\":\"0\"", json);
            Assert.Contains("\"path\":\"g1/c/x\"", json);
            Assert.Contains("\"path\":\"g1/c/y\"", json);
        }
        finally
        {
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Tree_ServesKeyedStream()
    {
        using var host = EndpointHost.Create(withDefaultStream: false, keyedStream: "dev");
        var hub = host.StartHub("dev");
        using var project = host.StartProject(SingleTagProject());
        try
        {
            var context = host.CreateContext(key: "dev");
            await TreeEndpoint.HandleAsync(context);

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Contains("\"path\":\"g1/a\"", EndpointHost.ReadBody(context));
        }
        finally
        {
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Tree_UnknownKey_IsNotFound()
    {
        using var host = EndpointHost.Create(withDefaultStream: false, keyedStream: "dev");
        _ = host.StartHub("dev");

        var context = host.CreateContext(key: "nope");
        await TreeEndpoint.HandleAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", context.Response.ContentType);
        Assert.Contains("未注册 key = \"nope\" 的观测流", EndpointHost.ReadBody(context));
    }

    [Fact]
    public async Task Tree_DefaultStreamNotRegistered_IsNotFound()
    {
        using var host = EndpointHost.Create(withDefaultStream: false, keyedStream: "dev");
        _ = host.StartHub("dev");

        var context = host.CreateContext();
        await TreeEndpoint.HandleAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Contains("未注册不具名观测流", EndpointHost.ReadBody(context));
    }

    [Fact]
    public async Task Catalog_ReturnsDesignerMetadataJson()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext();
        await DesignerCatalogEndpoint.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);

        var json = EndpointHost.ReadBody(context);
        Assert.Contains("\"version\":1", json);
        Assert.Contains("\"root\":", json);
        // 核心 schema 的全部节点都应出现在 catalog 里（设计器靠它渲染表单）。
        Assert.Contains("\"name\":\"Channel\"", json);
        Assert.Contains("\"name\":\"TagCbnt\"", json);
        Assert.Contains("\"name\":\"Logicet\"", json);
        Assert.Contains("\"tagTypes\":", json);
    }

    [Fact]
    public async Task Stream_PushesFramesUntilRequestAborted()
    {
        using var host = EndpointHost.Create();
        var hub = host.StartHub();
        using var project = host.StartProject(SingleTagProject());

        using var output = new RecordingStream();
        using var cts = new CancellationTokenSource();
        var context = host.CreateContext(queryString: "?entries=g1", abort: cts.Token);
        context.Response.Body = output;
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

        var handling = StreamEndpoint.HandleAsync(context);
        try
        {
            Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("event: state")));
            Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"entry\":\"g1\"")));

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("text/event-stream; charset=utf-8", context.Response.ContentType);
            Assert.Equal("no-cache, no-transform", context.Response.Headers.CacheControl);
            Assert.Equal("no", context.Response.Headers["X-Accel-Buffering"]);
        }
        finally
        {
            // 客户端断开：端点内部吞掉 OCE，正常返回。
            cts.Cancel();
            await handling;
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Stream_UnknownKey_IsNotFound()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext(key: "nope");
        await StreamEndpoint.HandleAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Contains("nope", EndpointHost.ReadBody(context));
    }

    [Fact]
    public async Task WriteValue_AppliesTheValueWithinTheEntry()
    {
        using var host = EndpointHost.Create();
        var hub = host.StartHub();
        using var project = host.StartProject(SingleTagProject());

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            var context = host.CreateContext(body: """{"path":"g1/a","raw":"7"}""");
            await CommandsEndpoint.WriteValueAsync(context);

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
            Assert.Contains("\"ok\":true", EndpointHost.ReadBody(context));
            Assert.Equal((short)7, StreamTestContext.TagAt(project, "g1/a").Value);
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task WriteValue_MalformedJson_IsBadRequest()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext(body: "not-a-json");
        await CommandsEndpoint.WriteValueAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        AssertMalformedJson(context);
    }

    [Fact]
    public async Task Rescan_MalformedJson_IsBadRequest()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext(body: "{}oops");
        await CommandsEndpoint.RescanAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        AssertMalformedJson(context);
    }

    /// <summary>序列化会转义非 ASCII，所以按 JSON 取属性再断言文案。</summary>
    private static void AssertMalformedJson(HttpContext context)
    {
        using var document = JsonDocument.Parse(EndpointHost.ReadBody(context));
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Contains("请求体不是合法的 JSON", root.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Rescan_MarksTheTagUnscanned()
    {
        using var host = EndpointHost.Create();
        var hub = host.StartHub();
        using var project = host.StartProject(SingleTagProject());

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            var context = host.CreateContext(body: """{"path":"g1/a"}""");
            await CommandsEndpoint.RescanAsync(context);

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Contains("\"ok\":true", EndpointHost.ReadBody(context));
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Commands_UseTheKeyedStreamController()
    {
        using var host = EndpointHost.Create(withDefaultStream: false, keyedStream: "dev");
        var hub = host.StartHub("dev");
        using var project = host.StartProject(SingleTagProject());

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            var context = host.CreateContext(key: "dev", body: """{"path":"g1/a","raw":"9"}""");
            await CommandsEndpoint.WriteValueAsync(context);

            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal((short)9, StreamTestContext.TagAt(project, "g1/a").Value);
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Commands_UnknownKey_IsNotFound()
    {
        using var host = EndpointHost.Create();
        _ = host.StartHub();

        var context = host.CreateContext(key: "nope", body: """{"path":"g1/a","raw":"1"}""");
        await CommandsEndpoint.WriteValueAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Contains("nope", EndpointHost.ReadBody(context));
    }
}

/// <summary>
/// 端点测试用的宿主：真实 DI + 真实 <c>TagsProject</c>（假驱动），只把 <see cref="ITagsProjectCtrl"/>
/// 换成可控的假实现（决定"何时启动/停止"）。
/// </summary>
internal sealed class EndpointHost : IDisposable
{
    private EndpointHost(ServiceProvider root, FakeProjectCtrl ctrl)
    {
        this.Root = root;
        this.Ctrl = ctrl;
    }

    public ServiceProvider Root { get; }

    public FakeProjectCtrl Ctrl { get; }

    public static EndpointHost Create(bool withDefaultStream = true, string? keyedStream = null)
    {
        var ctrl = new FakeProjectCtrl();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });

        // 端点的启停由测试决定：换成假的控制器（注册顺序保证后注册的生效）。
        services.RemoveAll<ITagsProjectCtrl>();
        services.AddSingleton<ITagsProjectCtrl>(ctrl);

        if (withDefaultStream)
        {
            services.AddTagsStream(FastOptions);
        }

        if (keyedStream is not null)
        {
            services.AddKeyedSingleton<ITagsProjectCtrl>(keyedStream, ctrl);
            services.AddKeyedTagsStream(keyedStream, FastOptions);
        }

        return new EndpointHost(services.BuildServiceProvider(), ctrl);
    }

    public ITagsProject CreateProject(XElement xml) =>
        this.Root.GetRequiredService<ITagsProjectFactory>().Create(StreamTestContext.ProjectRoot, xml);

    /// <summary>创建并对控制器派发"已启动"（不跑轮询）。</summary>
    public ITagsProject StartProject(XElement xml)
    {
        var project = this.CreateProject(xml);
        this.Ctrl.RaiseStarted(project);
        return project;
    }

    public TagsStreamHub StartHub(string? key = null)
    {
        var hub = this.Root.GetRequiredService<TagsStreamRegistry>().GetRequired(key).Hub;
        hub.Start();
        return hub;
    }

    public HttpContext CreateContext(
        string? key = null,
        string? queryString = null,
        string? body = null,
        CancellationToken abort = default)
    {
        var context = new DefaultHttpContext { RequestServices = this.Root };
        context.Request.Method = body is null ? HttpMethods.Get : HttpMethods.Post;
        context.Request.QueryString = new QueryString(queryString ?? string.Empty);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? string.Empty));
        context.RequestAborted = abort;
        context.Response.Body = new MemoryStream();

        if (key is not null)
        {
            context.Request.RouteValues[EndpointConstants.KeyedRouteValue] = key;
        }

        return context;
    }

    public static string ReadBody(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    public void Dispose() => this.Root.Dispose();

    private static void FastOptions(TagsStreamOptions options)
    {
        options.ReconcileInterval = TimeSpan.FromMilliseconds(20);
        options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    }
}

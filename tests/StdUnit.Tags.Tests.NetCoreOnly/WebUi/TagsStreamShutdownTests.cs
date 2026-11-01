using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StdUnit.Tags.Tests.Fakes;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// 观测流的<b>收尾</b>：宿主停机（<c>ApplicationStopping</c>）与容器释放都必须停掉 hub，
/// 并让在途 SSE 订阅者读到"流结束"——否则连接要等服务器的关机超时才会断，后台循环则永远留在进程里。
/// </summary>
public class TagsStreamShutdownTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static TagsStreamOptions FastOptions() => new()
    {
        ReconcileInterval = TimeSpan.FromMilliseconds(50),
        HeartbeatInterval = TimeSpan.FromSeconds(30),
    };

    [Fact]
    public async Task Stop_EndsOpenSubscriptions()
    {
        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHub(ctrl, FastOptions());
        hub.Start();

        using var output = new RecordingStream();
        using var piper = hub.Subscribe(entries: null);
        var pipe = piper.PipeFramesToStreamAsync(output, CancellationToken.None);

        // 订阅之后写循环阻塞在队列上（这一步证明"停之前流是开着的"）。
        Assert.False(pipe.IsCompleted);

        hub.Stop();

        await pipe.WaitAsync(Timeout);
        Assert.Contains("retry:", output.Text);
    }

    [Fact]
    public async Task Stop_IsIdempotent_AndNewSubscribersEndImmediately()
    {
        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHub(ctrl, FastOptions());
        hub.Start();

        hub.Stop();
        hub.Stop();   // 幂等：容器释放与 ApplicationStopping 各调一次

        // 停机窗口里还能进来的订阅：不能留在集合里（否则永远没人摘），但同样要立刻结束。
        using var output = new RecordingStream();
        using var piper = hub.Subscribe(entries: null);
        await piper.PipeFramesToStreamAsync(output, CancellationToken.None).WaitAsync(Timeout);
        Assert.Contains("retry:", output.Text);
    }

    [Fact]
    public async Task Start_AfterStop_DoesNotResurrect()
    {
        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHub(ctrl, FastOptions());
        hub.Start();
        hub.Stop();

        // 与宿主语义一致：Generic Host 停过就不能再启动，所以 hub 也保持"停即终态"。
        hub.Start();

        using var output = new RecordingStream();
        using var piper = hub.Subscribe(entries: null);
        await piper.PipeFramesToStreamAsync(output, CancellationToken.None).WaitAsync(Timeout);

        Assert.DoesNotContain("event: state", output.Text);
    }

    [Fact]
    public async Task ApplicationStopping_StopsEveryHubAndEndsStreams()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        var ctrl = new FakeProjectCtrl();
        builder.Services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        builder.Services.AddSingleton<ITagsProjectCtrl>(ctrl);   // 后注册的覆盖默认实现
        builder.Services.AddTagsStream();

        await using var app = builder.Build();
        app.MapTagsStream();

        var hub = app.Services.GetRequiredService<TagsStreamRegistry>().GetRequired(null).Hub;
        using var project = app.Services.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a"))));
        ctrl.RaiseStarted(project);

        using var output = new RecordingStream();
        using var piper = hub.Subscribe(entries: null);
        var pipe = piper.PipeFramesToStreamAsync(output, CancellationToken.None);
        Assert.False(pipe.IsCompleted);

        // 宿主开始停机：ApplicationStopping 早于"停 HostedService""停服务器"。
        app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        await pipe.WaitAsync(Timeout);
        Assert.Contains("event: state", output.Text);
    }

    [Fact]
    public async Task ContainerDispose_AlsoStopsHubs()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        var ctrl = new FakeProjectCtrl();
        builder.Services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });
        builder.Services.AddSingleton<ITagsProjectCtrl>(ctrl);
        builder.Services.AddTagsStream();

        var app = builder.Build();
        app.MapTagsStream();

        var hub = app.Services.GetRequiredService<TagsStreamRegistry>().GetRequired(null).Hub;

        using var output = new RecordingStream();
        using var piper = hub.Subscribe(entries: null);
        var pipe = piper.PipeFramesToStreamAsync(output, CancellationToken.None);
        Assert.False(pipe.IsCompleted);

        // 没走 ApplicationStopping（宿主没启动就释放）：容器释放是最后一道兜底。
        await app.DisposeAsync();

        await pipe.WaitAsync(Timeout);
    }
}

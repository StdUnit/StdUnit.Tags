using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <see cref="TagsStreamHostedService"/> 的端到端行为：状态（Replay 1）、全量基线、增量、按入口过滤、迟到订阅者。<br/>
/// 用真实的 <c>TagsProject</c> + 真实 <c>TagGrpRunner</c>（假驱动），只把 <see cref="ITagsProjectCtrl"/> 换成可控的假实现，
/// 以便精确决定"什么时候启动/停止"。
/// </summary>
public class TagsStreamHubTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public TagsStreamHubTests()
    {
        this._root = StreamTestContext.CreateServiceProvider();
        this._scope = this._root.CreateScope();
    }

    public void Dispose()
    {
        this._scope.Dispose();
        this._root.Dispose();
    }

    private ITagsProject CreateProject(XElement xml) =>
        this._scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(StreamTestContext.ProjectRoot, xml);

    private static ITagsProject CreateSingleEntryProject(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(
            StreamTestContext.ProjectRoot,
            new XElement("Project",
                StreamTestContext.Channel("fake"),
                StreamTestContext.EntryGroup("g1", "fake",
                    StreamTestContext.Tag("a"))));

    private static TagsStreamOptions FastOptions() => new()
    {
        ReconcileInterval = TimeSpan.FromMilliseconds(50),
        HeartbeatInterval = TimeSpan.FromSeconds(30),
        PiperQueueCapacity = 256,
    };

    [Fact]
    public async Task Hub_PushesStateBaselineAndDelta()
    {
        using var project = CreateSingleEntryProject(this._scope);
        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHostedService(ctrl, FastOptions());
        await hub.StartAsync(default);

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        // 订阅即刻拿到当前状态（项目未启动）。
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("event: state")));
        Assert.Contains("\"isStarted\":false", output.Text);

        // 启动：先状态，再各入口全量基线。
        ctrl.RaiseStarted(project);
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"isStarted\":true")));
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"full\":true")));

        // 跑真实的轮询：改一个值，应只收到该点的增量。
        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);

        StreamTestContext.TagAt(project, "g1/a").Value = (short)42;
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"raw\":\"42\"")));

        var deltaFrame = FrameContaining(output.Text, "\"raw\":\"42\"");
        Assert.NotNull(deltaFrame);
        Assert.Contains("\"path\":\"g1/a\"", deltaFrame);
        Assert.Contains("\"full\":false", deltaFrame);

        // 停止：状态变为未启动。
        ctrl.RaiseStopped();
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"isStarted\":false")));

        await StreamTestContext.StopAsync(runCts, run);
        await StopSubscriberAsync(piperCts, writer);
        await hub.StopAsync(default);
    }

    [Fact]
    public async Task Subscribe_FiltersByEntry()
    {
        using var project = this.CreateProject(new XElement("Project",
            StreamTestContext.Channel("fake1"),
            StreamTestContext.Channel("fake2"),
            StreamTestContext.EntryGroup("g1", "fake1", StreamTestContext.Tag("a")),
            StreamTestContext.EntryGroup("g2", "fake2", StreamTestContext.Tag("a"))));

        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHostedService(ctrl, FastOptions());
        await hub.StartAsync(default);

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        // 只订阅 g2
        using var piper = hub.Subscribe(new HashSet<string>(StringComparer.Ordinal) { "g2" });
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        ctrl.RaiseStarted(project);
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"entry\":\"g2\"")));

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);

        StreamTestContext.TagAt(project, "g1/a").Value = (short)11;
        StreamTestContext.TagAt(project, "g2/a").Value = (short)22;

        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"raw\":\"22\"")));
        await Task.Delay(150);

        Assert.DoesNotContain("\"raw\":\"11\"", output.Text);

        await StreamTestContext.StopAsync(runCts, run);
        await StopSubscriberAsync(piperCts, writer);
        await hub.StopAsync(default);
    }

    [Fact]
    public async Task SubscribeAfterStart_ReceivesBaselineImmediately()
    {
        using var project = CreateSingleEntryProject(this._scope);
        var ctrl = new FakeProjectCtrl();
        using var hub = new TagsStreamHostedService(ctrl, FastOptions());
        await hub.StartAsync(default);

        // 先启动（不跑轮询），再订阅：订阅者应立即拿到状态 + 全量基线。
        ctrl.RaiseStarted(project);

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"isStarted\":true")));
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"full\":true")));

        await StopSubscriberAsync(piperCts, writer);
        await hub.StopAsync(default);
    }

    [Fact]
    public async Task Hub_StartedAfterProject_StillAttachesTurnHandlers()
    {
        using var project = CreateSingleEntryProject(this._scope);
        var ctrl = new FakeProjectCtrl();

        // 项目先跑起来，hub 还没启动（顺序反了：以前这里会挂不上轮次回调，只能观测结构）。
        ctrl.RaiseStarted(project);
        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);

        using var hub = new TagsStreamHostedService(ctrl, FastOptions());
        await hub.StartAsync(default);
        Assert.True(hub.GetTree().IsStarted);

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        // 订阅即拿到全量基线（只读当前值，不能证明回调挂上了）。
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"full\":true")));

        // 再改一次值：能收到增量就说明轮次回调确实在运行期补挂上了。
        StreamTestContext.TagAt(project, "g1/a").Value = (short)99;
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"raw\":\"99\"")));

        await StreamTestContext.StopAsync(runCts, run);
        await StopSubscriberAsync(piperCts, writer);
        await hub.StopAsync(default);
    }

    [Fact]
    public async Task Piper_MarkResync_WakesIdleWriteLoop()
    {
        // 回归：MarkResync 只置标志时，写循环可能刚好在"读完队列里的最后一帧"之后重新阻塞，
        // 这一次唤醒就丢了，重同步要等到下一次心跳/增量才处理（并行跑测试时表现为偶发拿不到基线）。
        var hub = new SyncCountingHub();
        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = new SseFramesPiper(hub, entries: null, capacity: 8);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        // 先入队一帧并等它写完：此后写循环阻塞在 WaitToReadAsync 上，没有新帧不会自己醒。
        piper.Enqueue(SseFrames.Ping);
        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains(": ping")));
        await Task.Delay(100);

        piper.MarkResync();
        Assert.True(StreamTestContext.WaitUntil(() => hub.SyncCalls > 0));

        await StopSubscriberAsync(piperCts, writer);
    }

    /// <summary>取出包含指定片段的那一帧（以空行分隔）。</summary>
    private static string? FrameContaining(string text, string needle)
    {
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = text.LastIndexOf("\n\n", index, StringComparison.Ordinal);
        start = start < 0 ? 0 : start + 2;
        var end = text.IndexOf("\n\n", index, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static async Task StopSubscriberAsync(CancellationTokenSource cts, Task writer)
    {
        cts.Cancel();
        try
        {
            await writer;
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

}

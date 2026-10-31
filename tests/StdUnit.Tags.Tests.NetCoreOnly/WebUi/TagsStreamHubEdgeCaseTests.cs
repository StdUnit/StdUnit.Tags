using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;
using Xunit;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// <see cref="TagsStreamHub"/> 的边界行为：参数校验、启停幂等、无订阅者时的零开销、
/// 心跳、按 <see cref="TagsStreamOptions.MaxValuesPerFrame"/> 拆帧、容器标志单独成帧，
/// 以及 <see cref="SseFramesPiper"/> 队列溢出时的"丢弃 + 重同步"（绝不反压）。
/// </summary>
public class TagsStreamHubEdgeCaseTests : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public TagsStreamHubEdgeCaseTests()
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

    private static TagsStreamOptions Options() => new()
    {
        ReconcileInterval = TimeSpan.FromMilliseconds(20),
        HeartbeatInterval = TimeSpan.FromSeconds(30),
        PiperQueueCapacity = 256,
    };

    [Fact]
    public void Ctor_RejectsNullAndOutOfRangeArguments()
    {
        var ok = Options();

        Assert.Throws<ArgumentNullException>(() => new TagsStreamHub(null!, ok));
        Assert.Throws<ArgumentNullException>(() => new TagsStreamHub(new FakeProjectCtrl(), null!));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TagsStreamHub(new FakeProjectCtrl(), new TagsStreamOptions { ReconcileInterval = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TagsStreamHub(new FakeProjectCtrl(), new TagsStreamOptions { HeartbeatInterval = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TagsStreamHub(new FakeProjectCtrl(), new TagsStreamOptions { PiperQueueCapacity = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TagsStreamHub(new FakeProjectCtrl(), new TagsStreamOptions { PendingQueueCapacity = 0 }));
    }

    [Fact]
    public async Task StartAndStop_AreIdempotent()
    {
        using var hub = new TagsStreamHub(new FakeProjectCtrl(), Options());

        hub.Start();
        hub.Start();   // 第二次什么也不做
        await hub.StopAsync(default);
        await hub.StopAsync(default);    // 第二次时 pump/heartbeat 已为 null
    }

    [Fact]
    public async Task OnTurn_WithoutSubscribers_AndAfterStop_IsIgnored()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Tag("a"))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var hub = new TagsStreamHub(ctrl, Options());
        hub.Start();

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            // 没有订阅者：轮次回调的第一件事就是零开销返回。
            StreamTestContext.TagAt(project, "g1/a").Value = (short)1;
            Assert.True(StreamTestContext.WaitUntil(
                () => Equals((short)1, StreamTestContext.TagAt(project, "g1/a").Value)));

            // 已停止（索引被清空）但轮询还在跑：同样必须忽略，不能抛异常。
            ctrl.RaiseStopped();
            Assert.False(hub.GetTree().IsStarted);

            StreamTestContext.TagAt(project, "g1/a").Value = (short)2;
            Assert.True(StreamTestContext.WaitUntil(
                () => Equals((short)2, StreamTestContext.TagAt(project, "g1/a").Value)));
            Assert.False(hub.GetTree().IsStarted);
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Heartbeat_SendsPingToSubscribers()
    {
        var options = Options();
        options.HeartbeatInterval = TimeSpan.FromMilliseconds(50);

        using var hub = new TagsStreamHub(new FakeProjectCtrl(), options);
        hub.Start();

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains(": ping")));

        piperCts.Cancel();
        try
        {
            await writer;
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }

        await hub.StopAsync(default);
    }

    [Fact]
    public async Task Delta_IsSplitByMaxValuesPerFrame()
    {
        var options = Options();
        options.MaxValuesPerFrame = 1;

        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup(
                "g1",
                StreamTestContext.Tag("a"),
                StreamTestContext.Tag("b"),
                StreamTestContext.Tag("c"))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var hub = new TagsStreamHub(ctrl, options);
        hub.Start();

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"full\":true")));

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            StreamTestContext.TagAt(project, "g1/a").Value = (short)1;
            StreamTestContext.TagAt(project, "g1/b").Value = (short)2;
            StreamTestContext.TagAt(project, "g1/c").Value = (short)3;

            Assert.True(StreamTestContext.WaitUntil(() =>
                output.Text.Contains("\"raw\":\"1\"", StringComparison.Ordinal)
                && output.Text.Contains("\"raw\":\"2\"", StringComparison.Ordinal)
                && output.Text.Contains("\"raw\":\"3\"", StringComparison.Ordinal)));

            // 单帧上限是 1：任何一个增量帧里都不能出现两个值（这条断言不受"分几轮采到"影响）。
            var deltas = Frames(output.Text).Where(f => f.Contains("\"full\":false", StringComparison.Ordinal)).ToArray();
            Assert.NotEmpty(deltas);
            Assert.All(deltas, f => Assert.True(CountOccurrences(f, "\"path\":") <= 1, f));

            // 序号按写出顺序严格递增（拆帧不会打乱 seq）。
            var seqs = deltas.Select(Seq).ToArray();
            Assert.Equal(seqs.OrderBy(s => s).ToArray(), seqs);
            Assert.Equal(seqs.Length, seqs.Distinct().Count());
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            piperCts.Cancel();
            try
            {
                await writer;
            }
            catch (OperationCanceledException)
            {
                // 正常停机
            }

            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task ContainerFlagChange_IsPublishedWithoutValues()
    {
        using var project = this.CreateProject(new XElement(
            "Project",
            StreamTestContext.Channel("fake"),
            StreamTestContext.EntryGroup("g1", StreamTestContext.Group("sub", StreamTestContext.Tag("a")))));

        var ctrl = new FakeProjectCtrl();
        ctrl.RaiseStarted(project);

        using var hub = new TagsStreamHub(ctrl, Options());
        hub.Start();

        using var output = new RecordingStream();
        using var piperCts = new CancellationTokenSource();
        using var piper = hub.Subscribe(entries: null);
        var writer = piper.PipeFramesToStreamAsync(output, piperCts.Token);

        Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"full\":true")));

        using var runCts = new CancellationTokenSource();
        var run = project.RunAsync(runCts.Token);
        try
        {
            // 使能状态变化：值没变，所以是一帧"只有容器标志"的增量。
            project.Tags.Descendant("g1/sub").AsTagGrp().IsEnabled = false;

            Assert.True(StreamTestContext.WaitUntil(() => output.Text.Contains("\"containerFlags\":[{\"path\":\"g1/sub\",\"isEnabled\":false}]")));

            var frame = Frames(output.Text).Single(f => f.Contains("\"containerFlags\"", StringComparison.Ordinal));
            Assert.Contains("\"values\":[]", frame);
            Assert.Contains("\"full\":false", frame);
        }
        finally
        {
            await StreamTestContext.StopAsync(runCts, run);
            piperCts.Cancel();
            try
            {
                await writer;
            }
            catch (OperationCanceledException)
            {
                // 正常停机
            }

            await hub.StopAsync(default);
        }
    }

    [Fact]
    public async Task Piper_QueueOverflow_MarksResyncInsteadOfBlocking()
    {
        var hub = new SyncCountingHub();
        using var piper = new SseFramesPiper(hub, entries: null, capacity: 2);

        // 写循环还没开始：把队列灌满，再多入一帧必然失败 —— 那一帧被丢弃并标记重同步。
        piper.Enqueue(SseFrames.Ping);
        piper.Enqueue(SseFrames.Ping);
        piper.Enqueue(SseFrames.Ping);

        using var output = new RecordingStream();
        using var cts = new CancellationTokenSource();
        var writer = piper.PipeFramesToStreamAsync(output, cts.Token);

        Assert.True(StreamTestContext.WaitUntil(() => hub.SyncCalls > 0));

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

    /// <summary>按 SSE 帧分隔符（空行）切分。</summary>
    private static string[] Frames(string text) =>
        text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static long Seq(string frame)
    {
        const string Marker = "\"seq\":";
        var index = frame.IndexOf(Marker, StringComparison.Ordinal);
        Assert.True(index >= 0, "帧里没有 seq：" + frame);
        var start = index + Marker.Length;
        var end = start;
        while (end < frame.Length && (char.IsAsciiDigit(frame[end]) || frame[end] == '-'))
        {
            end++;
        }

        return long.Parse(frame[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }
}

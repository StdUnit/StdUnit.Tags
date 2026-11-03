using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Projects;

/// <summary>
/// 验证 <see cref="ITagsProject.RunnerStarted"/> / <see cref="ITagsProject.RunnerCrashed"/> 的<b>多播</b>语义：<br/>
/// - 多个订阅者按<b>注册顺序串行</b>执行，且每个订阅者的 <see cref="Task"/> 都被 await；<br/>
/// - 任一订阅者抛异常即<b>中断</b>本次通知（排在它后面的订阅者不再被调用），异常<b>原样</b>抛出并走入口故障路径；<br/>
/// - 运行期（<see cref="ITagsProject.RunAsync"/> 之后）订阅仍能收到<b>下一次</b>通知（不是"转发时抓一次快照"）；<br/>
/// - 运行期退订从下一次通知起生效。
/// </summary>
public class TagsProjectRunnerEventTests : IDisposable
{
    private const int TimeoutMs = 10_000;
    private const int ScanIntervalMs = 10;

    private readonly ServiceProvider _root;
    private readonly string _projectRoot;

    public TagsProjectRunnerEventTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });

        _root = services.BuildServiceProvider();
        _projectRoot = TestPaths.TempPath("TagsProjectRunnerEventTests");
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        _root.Dispose();
    }

    private static XElement Channel(string name) =>
        new("Channel", new XAttribute("name", name), new XAttribute("driver", "fake"));

    private static XElement Entry(string name, string channelName) =>
        new("TagGrp",
            new XAttribute("name", name),
            new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", "true"),
            new XAttribute("channel", channelName),
            new XAttribute("scanInterval", ScanIntervalMs),
            new XElement("Tag",
                new XAttribute("name", "v"),
                new XAttribute("type", "INT16"),
                new XAttribute("access", "RW"),
                new XAttribute("address", "0")));

    private ITagsProject CreateSingleEntryProject(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>().Create(
            _projectRoot,
            new XElement("Project", Channel("fake"), Entry("g1", "fake")));

    private static async Task WaitOrFailAsync(Task task, string what)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeoutMs)).ConfigureAwait(false);
        Assert.True(ReferenceEquals(completed, task), $"等待 {what} 超时（{TimeoutMs}ms）");
    }

    private static async Task StopAsync(CancellationTokenSource cts, Task run)
    {
        cts.Cancel();

        var finished = await Task.WhenAny(run, Task.Delay(TimeoutMs)).ConfigureAwait(false);
        Assert.True(ReferenceEquals(finished, run), "取消后轮询循环仍未退出");

        try
        {
            await run.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消时轮询循环以 OCE 退出，这是既有设计（见 LogicetCancellationTests）。
        }
    }

    [Fact]
    public async Task RunnerStarted_MultipleSubscribers_InvokedSequentiallyInRegistrationOrder()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var log = new ConcurrentQueue<string>();
        var firstCompleted = false;
        var secondSawFirstCompleted = false;
        var secondCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.RunnerStarted += async (grp, ch, ct) =>
        {
            await Task.Delay(50, ct);
            log.Enqueue("A:" + grp.TagName());
            Volatile.Write(ref firstCompleted, true);
        };
        proj.RunnerStarted += (grp, ch, ct) =>
        {
            Volatile.Write(ref secondSawFirstCompleted, Volatile.Read(ref firstCompleted));
            log.Enqueue("B:" + grp.TagName());
            secondCalled.TrySetResult(true);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(secondCalled.Task, "第二个 RunnerStarted 订阅者被调用");

        Assert.True(
            secondSawFirstCompleted,
            "第二个订阅者必须在第一个订阅者的 Task 完成后才被调用：多播必须逐个 await，而不是只等最后一个");
        Assert.Equal(new[] { "A:g1", "B:g1" }, log.ToArray());

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task RunnerCrashed_FirstSubscriberThrows_LaterSubscribersNotInvoked()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        // 触发崩溃：轮次回调抛异常（与 Logicet 抛异常同义）。
        proj.SetTurnProcessHandler("g1", (_, _, _) => throw new InvalidOperationException("observer boom"));

        var log = new ConcurrentQueue<string>();
        var secondCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            log.Enqueue("A");
            throw new InvalidOperationException("A boom");
        };
        proj.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            log.Enqueue("B");
            secondCalled.TrySetResult(true);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 异常原样按"该入口停机"处理：runner 记录 Critical 后重抛，RunAsync 因此结束。
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("A boom", thrown.Message);

        // 第一个订阅者抛出后，本次通知就此中断：后续订阅者不会被调用。
        Assert.Equal(new[] { "A" }, log.ToArray());
        Assert.False(secondCalled.Task.IsCompleted);
    }

    [Fact]
    public async Task LateSubscribers_ReceiveNextCrashAndReconnectStarted()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var firstTurn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var crashOn = false;
        var turnCount = 0;

        proj.SetTurnProcessHandler("g1", (_, _, _) =>
        {
            if (Volatile.Read(ref crashOn))
            {
                throw new InvalidOperationException("late boom");
            }
            if (Interlocked.Increment(ref turnCount) >= 1)
            {
                firstTurn.TrySetResult(true);
            }
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 首轮跑完 ⇒ PrepareEntries 早已完成（runner 回调那时就转发好了）。
        await WaitOrFailAsync(firstTurn.Task, "首轮完成");

        // 运行期补挂：修复前这两个订阅永远收不到任何通知（转发抓的是 PrepareEntries 那一刻的快照）。
        var crashSeen = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var restarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            crashSeen.TrySetResult(ex);
            return Task.CompletedTask;
        };
        proj.RunnerStarted += (grp, ch, ct) =>
        {
            restarted.TrySetResult(true);
            return Task.CompletedTask;
        };

        Volatile.Write(ref crashOn, true);

        await WaitOrFailAsync(crashSeen.Task, "晚挂的 RunnerCrashed 收到崩溃");
        Assert.Equal("late boom", (await crashSeen.Task).Message);

        // 只崩一次，避免继续刷崩溃
        Volatile.Write(ref crashOn, false);
        await WaitOrFailAsync(restarted.Task, "晚挂的 RunnerStarted 收到重连后的再次启动");

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task UnsubscribedWhileRunning_NotInvokedFromNextNotification()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var aCount = 0;
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunnerStarted handler = (grp, ch, ct) =>
        {
            if (Interlocked.Increment(ref aCount) == 1)
            {
                firstStarted.TrySetResult(true);
            }
            return Task.CompletedTask;
        };
        proj.RunnerStarted += handler;

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(firstStarted.Task, "首次 RunnerStarted");
        Assert.Equal(1, Volatile.Read(ref aCount));

        proj.RunnerStarted -= handler;

        // 换一个新订阅者作为"重连确实发生了"的探针（晚挂也必须能收到）。
        var reconnect = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerStarted += (grp, ch, ct) =>
        {
            reconnect.TrySetResult(true);
            return Task.CompletedTask;
        };

        // 触发一次崩溃，让运行器重连（重连时会再次触发 RunnerStarted）。
        var crashOnce = 1;
        proj.SetTurnProcessHandler("g1", (_, _, _) =>
        {
            if (Interlocked.Exchange(ref crashOnce, 0) == 1)
            {
                throw new InvalidOperationException("once boom");
            }
            return Task.CompletedTask;
        });

        await WaitOrFailAsync(reconnect.Task, "重连后的 RunnerStarted");
        Assert.Equal(1, Volatile.Read(ref aCount));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task RunnerStarted_FirstSubscriberThrows_LaterSubscribersNotInvoked_AndEntryFails()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var secondCalled = false;
        proj.RunnerStarted += (grp, ch, ct) => throw new InvalidOperationException("A boom");
        proj.RunnerStarted += (grp, ch, ct) =>
        {
            Volatile.Write(ref secondCalled, true);
            return Task.CompletedTask;
        };

        var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerCrashed += (grp, ch, ex, ct) =>
        {
            crashed.TrySetResult(ex);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(crashed.Task, "订阅者的异常被视为该入口本轮失败");

        var captured = await crashed.Task;
        var ex = Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("A boom", ex.Message);

        // 第一个订阅者抛出后，本次通知就此中断（不是"先收集、后只抛一个"）。
        Assert.False(Volatile.Read(ref secondCalled));

        await StopAsync(cts, run);
    }
}

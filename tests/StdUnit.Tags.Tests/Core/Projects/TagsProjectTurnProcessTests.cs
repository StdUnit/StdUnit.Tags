using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using StdUnit.Tags.Tests.Fakes;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Projects;

/// <summary>
/// 验证 <see cref="ITagsProject.SubscribeTurnProcess"/>：<br/>
/// - 按入口订阅，某入口每一轮触发一次，且<b>只</b>触发该入口的订阅者；<br/>
/// - 排在 Logicets <b>之后</b>（订阅者看到的是逻辑处理完成后的值）；<br/>
/// - 每个入口可以有多个订阅者：按<b>注册顺序串行</b> await，任一订阅者抛异常即中断后续（本轮失败）；<br/>
/// - 运行前/运行中都能订阅：运行期订阅与退订都<b>从下一轮开始生效</b>；<br/>
/// - 句柄 <c>Dispose</c> 即退订（幂等），项目已释放后再 <c>Dispose</c> 是无操作；<br/>
/// - 项目已释放后订阅抛 <see cref="ObjectDisposedException"/>；<br/>
/// - 入口被禁用时不会产生轮次，因此不会触发；<br/>
/// - 未知入口 / null 入口 / null 回调按契约抛异常。
/// </summary>
public class TagsProjectTurnProcessTests : IDisposable
{
    private const int TimeoutMs = 10_000;
    private const int ScanIntervalMs = 10;

    private readonly ServiceProvider _root;
    private readonly string _projectRoot;

    public TagsProjectTurnProcessTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
            b.AddFakedTagSupport();
        });

        _root = services.BuildServiceProvider();
        _projectRoot = TestPaths.TempPath("TagsProjectTurnProcessTests");
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        _root.Dispose();
    }

    /// <summary>每轮把指定测点写成固定值的逻辑组件。</summary>
    private sealed class SetValueLogicet : LogicetBase
    {
        private readonly ITag _tag;
        private readonly object _value;

        public SetValueLogicet(ITagsProject project, ITag tag, object value) : base(project)
        {
            _tag = tag;
            _value = value;
        }

        public override int Order => 0;

        public override bool MatchEntry(ITagGrp entry) => true;

        public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel, CancellationToken ct)
        {
            _tag.Value = _value;
            return Task.CompletedTask;
        }
    }

    private static XElement Channel(string name) =>
        new("Channel", new XAttribute("name", name), new XAttribute("driver", "fake"));

    private static XElement Entry(string name, string channelName, bool isEnabled = true) =>
        new("TagGrp",
            new XAttribute("name", name),
            new XAttribute("isEntry", "true"),
            new XAttribute("isEnabled", isEnabled ? "true" : "false"),
            new XAttribute("channel", channelName),
            new XAttribute("scanInterval", ScanIntervalMs),
            new XElement("Tag",
                new XAttribute("name", "v"),
                new XAttribute("type", "INT16"),
                new XAttribute("access", "RW"),
                new XAttribute("address", "0")));

    private ITagsProject CreateProject(IServiceProvider sp, XElement xml) =>
        sp.GetRequiredService<ITagsProjectFactory>().Create(_projectRoot, xml);

    private ITagsProject CreateSingleEntryProject(IServiceScope scope) =>
        CreateProject(scope.ServiceProvider,
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
    public async Task Subscription_FiresEveryTurn_ForRegisteredEntry()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var count = new int[1];
        var threeTurns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Assert.Equal("g1", entry.TagName());
            if (Interlocked.Increment(ref count[0]) >= 3)
            {
                threeTurns.TrySetResult(true);
            }
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(threeTurns.Task, "连续三轮回调");
        Assert.True(Volatile.Read(ref count[0]) >= 3);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Subscription_IsScopedToItsEntry()
    {
        // 通道不能跨入口共用（一个通道 = 一个轮询回路），所以每个入口用自己的通道实例。
        using var scope = _root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider, new XElement("Project",
            Channel("fake1"),
            Channel("fake2"),
            Entry("g1", "fake1"),
            Entry("g2", "fake2")));

        var g1Calls = new int[1];
        var g2Calls = new int[1];
        var g1Turns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var g2Turns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            if (Interlocked.Increment(ref g1Calls[0]) >= 2)
            {
                g1Turns.TrySetResult(true);
            }
            return Task.CompletedTask;
        });
        proj.SubscribeTurnProcess("g2", (entry, channel, _) =>
        {
            if (Interlocked.Increment(ref g2Calls[0]) >= 2)
            {
                g2Turns.TrySetResult(true);
            }
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(g1Turns.Task, "g1 的连续两轮回调");
        await WaitOrFailAsync(g2Turns.Task, "g2 的连续两轮回调");

        Assert.True(Volatile.Read(ref g1Calls[0]) >= 2);
        Assert.True(Volatile.Read(ref g2Calls[0]) >= 2);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Subscription_OnlyRegisteredEntryIsNotified()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider, new XElement("Project",
            Channel("fake1"),
            Channel("fake2"),
            Entry("g1", "fake1"),
            Entry("g2", "fake2")));

        var entries = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var g1Turns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            entries.AddOrUpdate(entry.TagName(), 1, (_, old) => old + 1);
            g1Turns.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(g1Turns.Task, "g1 的回调");
        await Task.Delay(ScanIntervalMs * 3);

        // 只为 g1 订阅：无论 g2 跑了多少轮，都不应收到 g2 的通知。
        Assert.True(entries.ContainsKey("g1"));
        Assert.DoesNotContain("g2", entries.Keys);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Subscription_RunsAfterLogicets()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var tag = proj.Tags.Descendant("g1/v").AsTag();
        const short written = 1234;

        proj.Logicets.Add(new SetValueLogicet(proj, tag, written));

        var observed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            observed.TrySetResult(tag.Value);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(observed.Task, "回调观察到逻辑组件的写入");
        var value = await observed.Task;
        Assert.Equal(written, Assert.IsType<short>(value));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task NoSubscription_DoesNotThrow()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 让它跑几轮：没有订阅者时，内部必须走"查不到就跳过"而不是空引用。
        await Task.Delay(ScanIntervalMs * 5);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task MultipleSubscriptions_InvokedInRegistrationOrder_AndEachTaskAwaited()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var log = new List<string>();
        var firstCompleted = false;
        var secondSawFirstCompleted = false;
        var secondCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 第一个订阅者异步完成：第二个必须等它返回的 Task 完成之后才被调用。
        proj.SubscribeTurnProcess("g1", async (entry, channel, ct) =>
        {
            await Task.Delay(30);
            lock (log) { log.Add("first"); }
            Volatile.Write(ref firstCompleted, true);
        });
        proj.SubscribeTurnProcess("g1", (entry, channel, ct) =>
        {
            Volatile.Write(ref secondSawFirstCompleted, Volatile.Read(ref firstCompleted));
            lock (log) { log.Add("second"); }
            secondCalled.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(secondCalled.Task, "第二个订阅者被调用");
        Assert.True(secondSawFirstCompleted, "多个订阅者必须逐个 await，而不是只等最后一个");

        string[] snapshot;
        lock (log) { snapshot = log.ToArray(); }
        Assert.Equal("first", snapshot[0]);
        Assert.Equal("second", snapshot[1]);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task SubscribeAfterRunStarted_TakesEffectFromNextRound()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 运行期订阅：下一轮开始生效（订阅表是写时复制的，轮内读取的是开始时拿到的快照）。
        var count = new int[1];
        var twoTurns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            if (Interlocked.Increment(ref count[0]) >= 2)
            {
                twoTurns.TrySetResult(true);
            }
            return Task.CompletedTask;
        });

        await WaitOrFailAsync(twoTurns.Task, "运行期订阅的回调从下一轮开始触发");
        Assert.True(Volatile.Read(ref count[0]) >= 2);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task DisposeHandle_StopsFiring()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var count = new int[1];
        var subscription = proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref count[0]);
            return Task.CompletedTask;
        });

        // 幂等：重复 Dispose 不应抛，也不应影响其它订阅者。
        subscription.Dispose();
        subscription.Dispose();

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await Task.Delay(ScanIntervalMs * 5);
        Assert.Equal(0, Volatile.Read(ref count[0]));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task DisposeHandle_RemovesOnlyThatSubscriber()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var first = new int[1];
        var second = new int[1];
        var secondCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstSubscription = proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref first[0]);
            return Task.CompletedTask;
        });
        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref second[0]);
            secondCalled.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);
        await WaitOrFailAsync(secondCalled.Task, "两个订阅者都开始工作");

        firstSubscription.Dispose();

        // 等在途轮次用掉旧快照后取基线，之后的轮次不应再进第一个订阅者。
        await Task.Delay(ScanIntervalMs * 3);
        var baselineFirst = Volatile.Read(ref first[0]);
        var baselineSecond = Volatile.Read(ref second[0]);

        await Task.Delay(ScanIntervalMs * 10);
        Assert.Equal(baselineFirst, Volatile.Read(ref first[0]));
        Assert.True(Volatile.Read(ref second[0]) > baselineSecond);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task DisposeWhileRunning_StopsFiringFromNextRound()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var count = new int[1];
        var firstTurn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref count[0]);
            firstTurn.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);
        await WaitOrFailAsync(firstTurn.Task, "回调先触发一次");

        // 运行期退订：等在途轮次用掉旧快照后取基线，之后的轮次都不应再进来。
        subscription.Dispose();
        await Task.Delay(ScanIntervalMs * 3);
        var baseline = Volatile.Read(ref count[0]);

        await Task.Delay(ScanIntervalMs * 10);
        Assert.Equal(baseline, Volatile.Read(ref count[0]));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task SubscribeOnDisposedProject_Throws_AndDisposeHandleIsNoOp()
    {
        using var scope = _root.CreateScope();
        var proj = CreateSingleEntryProject(scope);
        var subscription = proj.SubscribeTurnProcess("g1", (entry, channel, _) => Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);
        await StopAsync(cts, run);

        proj.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => proj.SubscribeTurnProcess("g1", (entry, channel, _) => Task.CompletedTask));

        // 项目释放后，退订句柄仍是安全的无操作（幂等）。
        subscription.Dispose();
        subscription.Dispose();
    }

    [Fact]
    public async Task ConcurrentSubscribeAndDisposeWhileRunning_DoesNotCorrupt()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        using var stopMutating = new CancellationTokenSource();
        var mutator = Task.Run(() =>
        {
            while (!stopMutating.IsCancellationRequested)
            {
                var subscription = proj.SubscribeTurnProcess("g1", (entry, channel, _) => Task.CompletedTask);
                subscription.Dispose();
            }
        });

        await Task.Delay(ScanIntervalMs * 20);
        stopMutating.Cancel();
        await mutator;

        // 并发订阅/退订不应让轮询崩掉（快照替换 + 轮询线程只读）。
        Assert.False(run.IsFaulted, run.Exception?.ToString());

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task SubscriberThrows_EntersRunnerCrashed()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        proj.SubscribeTurnProcess(
            "g1",
            (entry, channel, _) => throw new InvalidOperationException("observer boom"));

        var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerCrashed += (entry, channel, ex, ct) =>
        {
            crashed.TrySetResult(ex);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(crashed.Task, "回调的异常进入 RunnerCrashed");
        var captured = await crashed.Task;
        var ex = Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("observer boom", ex.Message);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task FirstSubscriberThrows_LaterSubscribersNotInvoked()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var secondCalled = false;
        proj.SubscribeTurnProcess("g1", (entry, channel, _) => throw new InvalidOperationException("first boom"));
        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Volatile.Write(ref secondCalled, true);
            return Task.CompletedTask;
        });

        var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerCrashed += (entry, channel, ex, ct) =>
        {
            crashed.TrySetResult(ex);
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(crashed.Task, "第一个订阅者的异常触发崩溃路径");

        var captured = await crashed.Task;
        var ex = Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("first boom", ex.Message);

        // 首个异常即中断：排在后面的订阅者本次不会被调用。
        Assert.False(Volatile.Read(ref secondCalled));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Subscription_DisabledEntry_NotFired()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider, new XElement("Project",
            Channel("fake"),
            Entry("g1", "fake", isEnabled: false)));

        var count = new int[1];
        proj.SubscribeTurnProcess("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref count[0]);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 被禁用的入口在 TagGrpRunner 里直接 Task.Delay(500) 跳过，根本不会产生轮次。
        await Task.Delay(300);
        Assert.Equal(0, Volatile.Read(ref count[0]));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Subscription_ReceivesProjectCancellationToken()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var observed = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SubscribeTurnProcess("g1", (entry, channel, ct) =>
        {
            observed.TrySetResult(ct);
            return Task.CompletedTask;
        });

        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(observed.Task, "回调收到取消令牌");
        var captured = await observed.Task;

        // 不必与 RunAsync 的入参是同一个实例：框架用内部 linked 令牌启动各入口（启动失败时要能回滚已启动的入口）。
        // 契约是"取消时机"，不是引用相等。
        Assert.True(captured.CanBeCanceled);
        Assert.False(captured.IsCancellationRequested, "取消前回调拿到的令牌不应已取消");

        await StopAsync(cts, run);

        Assert.True(captured.IsCancellationRequested, "项目停止后，回调拿到的令牌也应取消");
    }

    [Fact]
    public void Subscribe_UnknownEntry_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        Assert.Throws<KeyNotFoundException>(
            () => proj.SubscribeTurnProcess("nope", (entry, channel, _) => Task.CompletedTask));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        // 退订必须用 Dispose 句柄，而不是传 null。
        Assert.Throws<ArgumentNullException>(() => proj.SubscribeTurnProcess("g1", null!));
    }

    [Fact]
    public void Subscribe_NullEntry_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        Assert.Throws<ArgumentNullException>(
            () => proj.SubscribeTurnProcess(null!, (entry, channel, _) => Task.CompletedTask));
    }
}

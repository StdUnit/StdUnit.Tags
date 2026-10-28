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
/// 验证 <see cref="ITagsProject.SetTurnProcessHandler"/>：<br/>
/// - 按入口设置，某入口每一轮触发一次，且<b>只</b>触发该入口的回调；<br/>
/// - 排在 Logicets <b>之后</b>（回调看到的是逻辑处理完成后的值）；<br/>
/// - 每个入口最多一个回调：重复设置是替换，多个观察者由调用方自己组合（顺序显式）；<br/>
/// - 只能在项目运行<b>之前</b>设置（运行后抛 <see cref="InvalidOperationException"/>），传 <c>null</c> 表示清除；<br/>
/// - 回调抛出的异常不被吞掉（走 <see cref="ITagsProject.RunnerCrashed"/> 的恢复路径）；<br/>
/// - 入口被禁用时不会产生轮次，因此不会触发；<br/>
/// - 未知入口 / null 入口按契约抛异常。
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
    public async Task Handler_FiresEveryTurn_ForRegisteredEntry()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var count = new int[1];
        var threeTurns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
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
    public async Task Handler_IsScopedToItsEntry()
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

        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
        {
            if (Interlocked.Increment(ref g1Calls[0]) >= 2)
            {
                g1Turns.TrySetResult(true);
            }
            return Task.CompletedTask;
        });
        proj.SetTurnProcessHandler("g2", (entry, channel, _) =>
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
    public async Task Handler_OnlyRegisteredEntryIsNotified()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider, new XElement("Project",
            Channel("fake1"),
            Channel("fake2"),
            Entry("g1", "fake1"),
            Entry("g2", "fake2")));

        var entries = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var g1Turns = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
        {
            entries.AddOrUpdate(entry.TagName(), 1, (_, old) => old + 1);
            g1Turns.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(g1Turns.Task, "g1 的回调");
        await Task.Delay(ScanIntervalMs * 3);

        // 只为 g1 注册：无论 g2 跑了多少轮，都不应收到 g2 的通知。
        Assert.True(entries.ContainsKey("g1"));
        Assert.DoesNotContain("g2", entries.Keys);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Handler_RunsAfterLogicets()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var tag = proj.Tags.Descendant("g1/v").AsTag();
        const short written = 1234;

        proj.Logicets.Add(new SetValueLogicet(proj, tag, written));

        var observed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
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
    public async Task Handler_NotRegistered_DoesNotThrow()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 让它跑几轮：没有注册回调时，内部必须走"查不到就跳过"而不是空引用。
        await Task.Delay(ScanIntervalMs * 5);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Set_SecondCall_ReplacesPreviousHandler()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var first = new int[1];
        var second = new int[1];
        var secondCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref first[0]);
            return Task.CompletedTask;
        });
        // 替换语义：后设置的生效，前一个不再被调用。
        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref second[0]);
            secondCalled.TrySetResult(true);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(secondCalled.Task, "后设置的回调被调用");

        Assert.True(Volatile.Read(ref second[0]) >= 1);
        Assert.Equal(0, Volatile.Read(ref first[0]));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Set_ComposedHandler_InvokesObserversInDeclaredOrder()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var order = new List<string>();
        var composed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 多个观察者由调用方自己组合：顺序在组合处显式可见，而不是依赖"注册顺序"。
        proj.SetTurnProcessHandler("g1", async (entry, channel, ct) =>
        {
            await Task.CompletedTask;
            lock (order) { order.Add("first"); }

            await Task.CompletedTask;
            lock (order) { order.Add("second"); }

            composed.TrySetResult(true);
        });

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(composed.Task, "组合回调被执行");

        string[] snapshot;
        lock (order) { snapshot = order.ToArray(); }
        Assert.Equal(new[] { "first", "second" }, snapshot);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Set_Null_ClearsHandler()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        var count = new int[1];
        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
        {
            Interlocked.Increment(ref count[0]);
            return Task.CompletedTask;
        });
        proj.SetTurnProcessHandler("g1", null);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        await Task.Delay(ScanIntervalMs * 5);
        Assert.Equal(0, Volatile.Read(ref count[0]));

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task SetTurnProcessHandler_AfterRunStarted_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var run = proj.RunAsync(cts.Token);

        // 运行期不再接受注册：入口列表在运行前就已固定，动态开关观测应在回调内部判断。
        var ex = Assert.Throws<InvalidOperationException>(
            () => proj.SetTurnProcessHandler("g1", (entry, channel, _) => Task.CompletedTask));
        Assert.Contains("运行之前", ex.Message);

        await StopAsync(cts, run);
    }

    [Fact]
    public async Task Handler_SubscriberThrows_EntersRunnerCrashed()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        proj.SetTurnProcessHandler(
            "g1",
            (entry, channel, _) => throw new InvalidOperationException("observer boom"));

        var crashed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        proj.RunnerCrashed += (entry, channel, ex) =>
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
    public async Task Handler_DisabledEntry_NotFired()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateProject(scope.ServiceProvider, new XElement("Project",
            Channel("fake"),
            Entry("g1", "fake", isEnabled: false)));

        var count = new int[1];
        proj.SetTurnProcessHandler("g1", (entry, channel, _) =>
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
    public async Task Handler_ReceivesProjectCancellationToken()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        using var cts = new CancellationTokenSource();
        var observed = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);

        proj.SetTurnProcessHandler("g1", (entry, channel, ct) =>
        {
            observed.TrySetResult(ct);
            return Task.CompletedTask;
        });

        var run = proj.RunAsync(cts.Token);

        await WaitOrFailAsync(observed.Task, "回调收到取消令牌");
        var captured = await observed.Task;
        Assert.True(captured.CanBeCanceled);
        Assert.Equal(cts.Token, captured);

        await StopAsync(cts, run);
    }

    [Fact]
    public void SetTurnProcessHandler_UnknownEntry_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        Assert.Throws<KeyNotFoundException>(
            () => proj.SetTurnProcessHandler("nope", (entry, channel, _) => Task.CompletedTask));
    }

    [Fact]
    public void SetTurnProcessHandler_NullHandler_IsAllowed()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        // null = 清除；在从未设置过的入口上清除也是合法的空操作。
        proj.SetTurnProcessHandler("g1", null);
        proj.SetTurnProcessHandler("g1", (entry, channel, _) => Task.CompletedTask);
        proj.SetTurnProcessHandler("g1", null);
    }

    [Fact]
    public void SetTurnProcessHandler_NullEntry_Throws()
    {
        using var scope = _root.CreateScope();
        using var proj = CreateSingleEntryProject(scope);

        Assert.Throws<ArgumentNullException>(() => proj.SetTurnProcessHandler(null!, (entry, channel, _) => Task.CompletedTask));
    }
}

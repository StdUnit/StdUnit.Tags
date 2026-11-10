using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.R3;
using StdUnit.Tags.Tests.Core;
using StdUnit.Tags.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R3;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

public class TagsProjectCtrlTests
{
    private readonly MockTagsProjectFactory _factory = new();

    /// <summary>
    /// 注册 <seealso cref="MockTagsProjectFactory "/>, 
    /// 并创建 <seealso cref="TagsProjectCtrl"/>。
    /// </summary>
    private (TagsProjectCtrl ctrl, ServiceProvider sp) CreateCtrl() => CreateCtrl(loggerProvider: null);

    /// <summary>
    /// 同 <see cref="CreateCtrl()"/>，但额外挂上 <paramref name="loggerProvider"/>，
    /// 以便断言清理路径「有意吞掉异常但必须留痕」。
    /// </summary>
    private (TagsProjectCtrl ctrl, ServiceProvider sp) CreateCtrl(ILoggerProvider? loggerProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITagsProjectFactory>(_factory);
        if (loggerProvider is null)
        {
            services.AddLogging();
        }
        else
        {
            services.AddLogging(b => b.AddProvider(loggerProvider));
        }
        var sp = services.BuildServiceProvider();
        var ssf = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = sp.GetRequiredService<ILogger<TagsProjectCtrl>>();
        var ctrl = new TagsProjectCtrl(ssf, logger);
        return (ctrl, sp);
    }

    /// <summary>
    /// 订阅 <c>StartedOrStopped</c> 的"已启动"事件，返回可等待的句柄。<br/>
    /// <b>必须在调用 <see cref="ITagsProjectCtrl.StartPollAsync"/> 之前调用</b>，否则事件可能先于订阅触发而丢失。<br/>
    /// <br/>
    /// 为什么不等 <c>ctrl.Project != null</c>、更不用 <c>Task.Delay</c> 猜：
    /// <list type="bullet">
    /// <item><c>Project</c> 在启动 hook 执行<b>之前</b>就已赋值，而 hook 里常常要向项目里补通道/逻辑组件；
    /// 只等 <c>Project != null</c> 就去 <c>StopAsync()</c>，可能与 hook 竞态（清理不到 hook 刚加的东西）。</item>
    /// <item>该事件是在 hook 执行<b>完成之后</b>才触发的，是"项目确实已就绪"的确定性信号；
    /// 用户侧观测启停用的也是它（R3/Rx 扩展的 <c>ObserveStartedOrStopped</c>，本测试即通过它订阅）。</item>
    /// </list>
    /// </summary>
    private static ProjectStartedWatcher WatchProjectStarted(TagsProjectCtrl ctrl) => new(ctrl);

    /// <summary>
    /// 等待 <see cref="TagsProjectCtrl.StartPollAsync"/> 真正就绪（<c>StartedOrStopped</c> 的 <c>IsStarted=true</c>）。<br/>
    /// 订阅走的是生产代码同一个入口——<see cref="TagsProjectCtrlExtensions.ObserveStartedOrStopped"/>，
    /// 不手搓 <c>+=</c>/<c>-=</c>。
    /// </summary>
    private sealed class ProjectStartedWatcher : IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly TaskCompletionSource<TagsProjectEventArgs> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProjectStartedWatcher(TagsProjectCtrl ctrl)
        {
            _subscription = ctrl.ObserveStartedOrStopped().Subscribe(args =>
            {
                if (args.IsStarted())
                {
                    _started.TrySetResult(args);
                }
            });
        }

        /// <summary>等待"已启动"；超时抛 <see cref="TimeoutException"/>（兜底，避免卡死测试）。</summary>
        public Task WaitAsync() => _started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public void Dispose() => _subscription.Dispose();
    }

    [Fact]
    public async Task Project_Initially_IsNull()
    {
        var (ctrl, sp) = CreateCtrl();
        Assert.Null(ctrl.Project);
        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_CreatesProjectAndInvokesHook()
    {
        var (ctrl, sp) = CreateCtrl();
        var hookInvoked = new TaskCompletionSource<bool>();
        ITagsProject? capturedProject = null;

        // 后台启动，因为 StartPollAsync 会阻塞在 RunAsync 上
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, sp, ct) =>
            {
                capturedProject = proj;
                hookInvoked.TrySetResult(true);
                return Task.CompletedTask;
            }));

        // 等待 hook 被调用
        await hookInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // 验证项目已创建、hook 已收到项目引用
        Assert.NotNull(ctrl.Project);
        Assert.NotNull(capturedProject);
        Assert.Same(ctrl.Project, capturedProject);
        Assert.Same(_factory.LastCreatedProject, capturedProject);
        Assert.Equal("test_dir", _factory.CapturedProjRoot);

        // 停止
        await ctrl.StopAsync();

        // StartPollAsync 应该已完成
        await startTask.WaitAsync(TimeSpan.FromSeconds(10));

        // 停止后 Project 应为 null
        Assert.Null(ctrl.Project);
        Assert.True(_factory.LastCreatedProject!.DisposeCallCount > 0, "项目应该被释放");

        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_FiresStartedEvent()
    {
        var (ctrl, sp) = CreateCtrl();
        var startEventFired = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (args.IsStarted())
            {
                startEventFired.TrySetResult(args);
            }
        };

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        var eventArgs = await startEventFired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(eventArgs.IsStarted());
        Assert.NotNull(eventArgs.Project);
        Assert.Same(ctrl.Project, eventArgs.Project);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        sp.Dispose();
    }

    [Fact]
    public async Task StopAsync_FiresStoppedEvent()
    {
        var (ctrl, sp) = CreateCtrl();
        var stopEventFired = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                stopEventFired.TrySetResult(args);
            }
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        // 等待"已启动"信号（在 hook 执行完之后触发），然后停止
        await started.WaitAsync();
        await ctrl.StopAsync();

        var eventArgs = await stopEventFired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(eventArgs.IsStarted());
        Assert.Null(eventArgs.Project);

        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_DoubleStart_ThrowsException()
    {
        var (ctrl, sp) = CreateCtrl();

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        // 确保第一个已启动
        await started.WaitAsync();

        // 第二次启动应抛出异常
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask));

        Assert.Contains("已经启动", ex.Message);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_DoubleStart_WithHandler_Handled_ReturnsWithoutThrowing()
    {
        var (ctrl, sp) = CreateCtrl();
        var handlerCalled = false;

        ctrl.OnStartingException = ex =>
        {
            handlerCalled = true;
            return Task.FromResult(true); // 已处理，不抛出
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();

        // 第二次启动，有 handler 且返回 true，不应抛出
        await ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask);

        Assert.True(handlerCalled);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_DoubleStart_WithHandler_NotHandled_Throws()
    {
        var (ctrl, sp) = CreateCtrl();

        ctrl.OnStartingException = ex => Task.FromResult(false); // 未处理

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask));

        Assert.Contains("已经启动", ex.Message);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        sp.Dispose();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var (ctrl, sp) = CreateCtrl();

        // 从未启动就停止，不应抛出异常
        await ctrl.StopAsync();

        Assert.Null(ctrl.Project);
        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_WhenHookThrows_ExceptionPropagates()
    {
        var (ctrl, sp) = CreateCtrl();

        // hook 抛异常 -> StartPollAsync 应传播异常
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync(
                dir: "test_dir",
                root: new XElement("Project"),
                hook: (proj, sp, ct) => throw new InvalidOperationException("hook 异常")));

        Assert.Contains("hook 异常", ex.Message);

        // 项目应被清理
        Assert.Null(ctrl.Project);
        // 如果项目已创建，应已被释放
        if (_factory.LastCreatedProject is not null)
        {
            Assert.Equal(1, _factory.LastCreatedProject.DisposeCallCount);
        }

        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_WhenFactoryThrows_OnStartingExceptionCalled()
    {
        var (ctrl, sp) = CreateCtrl();
        var handlerCalled = false;
        Exception? capturedException = null;

        _factory.CreateThrows = true;

        ctrl.OnStartingException = ex =>
        {
            handlerCalled = true;
            capturedException = ex;
            return Task.FromResult(true); // 已处理
        };

        // 工厂异常 -> OnStartingException 被调用
        await ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask);

        Assert.True(handlerCalled);
        Assert.NotNull(capturedException);
        Assert.IsType<InvalidOperationException>(capturedException);
        Assert.Null(ctrl.Project);

        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_WhenFactoryThrows_WithoutHandler_Rethrows()
    {
        var (ctrl, sp) = CreateCtrl();
        _factory.CreateThrows = true;

        // 没有注册 OnStartingException -> 异常向外传播
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask));

        Assert.Contains("模拟的工厂异常", ex.Message);
        Assert.Null(ctrl.Project);

        sp.Dispose();
    }

    [Fact]
    public async Task StartPollAsync_WhenFactoryThrows_WithHandlerNotHandled_Rethrows()
    {
        var (ctrl, sp) = CreateCtrl();
        _factory.CreateThrows = true;

        ctrl.OnStartingException = ex => Task.FromResult(false); // 未处理

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync("test_dir", new XElement("Project"), (_, _, _) => Task.CompletedTask));

        Assert.Contains("模拟的工厂异常", ex.Message);
        Assert.Null(ctrl.Project);

        sp.Dispose();
    }

    [Fact]
    public async Task StopAsync_DisconnectsChannels()
    {
        var (ctrl, sp) = CreateCtrl();
        var channelDisconnected = false;

        var mockChannel = new MockChannel
        {
            DisconnectAsyncImpl = ct =>
            {
                channelDisconnected = true;
                return Task.CompletedTask;
            }
        };

        // 让工厂创建的项目包含此通道
        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, sp, ct) =>
            {
                if (proj is MockTagsProject mock)
                {
                    mock.AddChannel(mockChannel);
                }
                return Task.CompletedTask;
            }));

        // 必须等"已启动"（hook 已跑完），否则可能与 hook 里 AddChannel 竞态
        await started.WaitAsync();
        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(channelDisconnected, "通道应被断开连接");
        sp.Dispose();
    }

    /// <summary>
    /// <see cref="TagsProjectCtrl.StopAsync"/> 必须在"轮询循环真正退出"之后才返回：
    /// 否则"已停止"事件不是确定性信号——调用方拿到它就去复用通道 / 换 XML，会与仍在收尾的循环撞车。
    /// </summary>
    [Fact]
    public async Task StopAsync_WaitsUntilPollingLoopExited()
    {
        var (ctrl, sp) = CreateCtrl();

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();
        await ctrl.StopAsync();

        // StopAsync 只保证"轮询循环已退出"（StartPollAsync 的 finally 已把信号置位、项目已释放、锁已释放），
        // 不保证外层 Task.Run 的包装任务此刻已转为 Completed——那需要一次额外的调度跳变。
        // 之前这里直接断言 startTask.IsCompleted，在 net472 上会稳定失败（net8.0 恰好赢得调度）。
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(ctrl.Project);
        Assert.Equal(1, _factory.LastCreatedProject!.DisposeCallCount);
        sp.Dispose();
    }

    /// <summary>
    /// 驱动不响应取消时，<see cref="TagsProjectCtrl.StopAsync"/> 不能永久挂住：等待轮询退出要有超时兜底，
    /// 超时后记 Warning 并继续清理（此时项目由 StopAsync 释放，且只释放一次）。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenPollingIgnoresCancellation_TimesOutAndStillCleans()
    {
        var logs = new CapturingLoggerProvider();
        var (ctrl, sp) = CreateCtrl(logs);
        ctrl.PollExitWaitTimeout = TimeSpan.FromMilliseconds(200);
        _factory.ProjectRunAsyncNeverCompletes = true;

        using var started = WatchProjectStarted(ctrl);
        _ = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();
        await ctrl.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); // 不应挂住

        Assert.Null(ctrl.Project);
        Assert.Equal(1, _factory.LastCreatedProject!.DisposeCallCount);
        var warning = Assert.Single(logs.Entries, e => e.Message.Contains("等待轮询循环退出超时"));
        Assert.Equal(LogLevel.Warning, warning.Level);
        sp.Dispose();
    }

    /// <summary>
    /// 清理路径有意吞掉 <see cref="ITagChannel.DisconnectAsync"/> 的异常：
    /// 不向外抛、不阻断其它通道的断开，但必须留下 Warning 日志（含通道名），否则连接没断干净也无人知晓。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenChannelDisconnectThrows_SwallowsContinuesAndLogs()
    {
        var logs = new CapturingLoggerProvider();
        var (ctrl, sp) = CreateCtrl(logs);
        var healthyChannelDisconnected = false;

        var throwingChannel = new MockChannel
        {
            DisconnectAsyncImpl = _ => throw new InvalidOperationException("断开失败-boom"),
        };
        var healthyChannel = new MockChannel
        {
            DisconnectAsyncImpl = _ =>
            {
                healthyChannelDisconnected = true;
                return Task.CompletedTask;
            },
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, _, _) =>
            {
                if (proj is MockTagsProject mock)
                {
                    mock.AddChannel(throwingChannel);
                    mock.AddChannel(healthyChannel);
                }
                return Task.CompletedTask;
            }));

        // 必须等"已启动"（hook 已跑完），否则可能与 hook 里 AddChannel 竞态
        await started.WaitAsync();
        await ctrl.StopAsync(); // 不应抛出
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(healthyChannelDisconnected, "一个通道断开失败不应阻断其它通道的清理");

        var warning = Assert.Single(logs.Entries, e => e.Exception?.Message == "断开失败-boom");
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("mock", warning.Message); // 消息里带上通道名
        sp.Dispose();
    }

    /// <summary>
    /// <c>StartPollAsync</c> 的 finally 里释放项目失败时：释放异常必须被吞掉，
    /// 不能覆盖轮询阶段抛出的原始异常；同时留痕。
    /// </summary>
    [Fact]
    public async Task StartPollAsync_WhenDisposeThrows_OriginalExceptionWinsAndWarningLogged()
    {
        var logs = new CapturingLoggerProvider();
        var (ctrl, sp) = CreateCtrl(logs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctrl.StartPollAsync(
                dir: "test_dir",
                root: new XElement("Project"),
                hook: (proj, _, _) =>
                {
                    var mock = (MockTagsProject)proj;
                    mock.DisposeThrows = true;
                    mock.RunAsyncThrows = true;
                    return Task.CompletedTask;
                }));

        Assert.Contains("模拟的 RunAsync 异常", ex.Message); // 原始异常胜出
        Assert.Null(ctrl.Project);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Exception?.Message == "模拟的 Dispose 异常");
        sp.Dispose();
    }

    /// <summary>
    /// <c>StartedOrStopped</c> 的事件处理器抛错时：不能让 <c>StopAsync</c> 失败（吞掉），但必须留痕。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenStartedOrStoppedHandlerThrows_SwallowsAndLogs()
    {
        var logs = new CapturingLoggerProvider();
        var (ctrl, sp) = CreateCtrl(logs);

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                throw new InvalidOperationException("停止通知处理器-boom");
            }
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();
        await ctrl.StopAsync(); // 不应抛出
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Exception?.Message == "停止通知处理器-boom");
        sp.Dispose();
    }

    [Fact]
    public async Task OnStartingException_Property_DefaultIsNull()
    {
        var (ctrl, sp) = CreateCtrl();
        Assert.Null(ctrl.OnStartingException);
        sp.Dispose();
    }

    [Fact]
    public async Task OnStartingException_Property_CanBeSet()
    {
        var (ctrl, sp) = CreateCtrl();
        Func<Exception, Task<bool>> handler = ex => Task.FromResult(true);
        ctrl.OnStartingException = handler;
        Assert.Same(handler, ctrl.OnStartingException);
        sp.Dispose();
    }

    /// <summary>
    /// 用同一个 ctrl 先后启动两次不同的项目（先停再启动第二次），不应抛出异常。
    /// </summary>
    [Fact]
    public async Task Start_Stop_Start_Works()
    {
        var (ctrl, sp) = CreateCtrl();

        // 第一次启动并停止
        using var started1 = WatchProjectStarted(ctrl);
        var startTask1 = Task.Run(() => ctrl.StartPollAsync(
            dir: "dir1",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started1.WaitAsync();
        await ctrl.StopAsync();
        await startTask1.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(ctrl.Project);

        // 第二次启动
        using var started2 = WatchProjectStarted(ctrl);
        var startTask2 = Task.Run(() => ctrl.StartPollAsync(
            dir: "dir2",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started2.WaitAsync();
        Assert.NotNull(ctrl.Project);

        await ctrl.StopAsync();
        await startTask2.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(ctrl.Project);

        Assert.Equal(2, _factory.TotalDisposeCallCount);
        sp.Dispose();
    }

    /// <summary>
    /// 状态机：<c>hook</c> 执行期间是 <see cref="TagsProjectState.Starting"/>（此时 Project 已非 null），
    /// <c>hook</c> 完成后是 <see cref="TagsProjectState.Running"/>，停止后回到 <see cref="TagsProjectState.Stopped"/>。
    /// 这条测试锁住"<c>Project != null</c> 不等于已就绪"的语义：就绪只能看 <c>State</c>。
    /// </summary>
    [Fact]
    public async Task State_IsStartingInsideHook_ThenRunning_ThenStopped()
    {
        var (ctrl, sp) = CreateCtrl();
        Assert.Equal(TagsProjectState.Stopped, ctrl.State);
        Assert.Null(ctrl.Project);

        TagsProjectState stateInsideHook = default;
        var projectInsideHookIsNull = true;

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, _, _) =>
            {
                stateInsideHook = ctrl.State;
                projectInsideHookIsNull = ctrl.Project is null;
                return Task.CompletedTask;
            }));

        await started.WaitAsync();

        // hook 期间：已经是 Starting、Project 已经赋值（所以不能凭 Project != null 判定就绪）
        Assert.Equal(TagsProjectState.Starting, stateInsideHook);
        Assert.False(projectInsideHookIsNull);

        // hook 完成之后：Running，且 RunId 非 0
        Assert.Equal(TagsProjectState.Running, ctrl.State);
        Assert.NotNull(ctrl.Project);
        Assert.True(ctrl.RunId > 0);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(TagsProjectState.Stopped, ctrl.State);
        Assert.Null(ctrl.Project);
        sp.Dispose();
    }

    /// <summary>
    /// 从未启动时调用 <c>StopAsync</c>：空操作，**不派发**"已停止"（否则观察者会收到一次没人宣布过的停止）。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenNeverStarted_DoesNotFireStopped()
    {
        var (ctrl, sp) = CreateCtrl();
        var stoppedCount = 0;

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                Interlocked.Increment(ref stoppedCount);
            }
        };

        await ctrl.StopAsync();
        await ctrl.StopAsync();

        Assert.Equal(0, stoppedCount);
        Assert.Equal(TagsProjectState.Stopped, ctrl.State);
        sp.Dispose();
    }

    /// <summary>
    /// 重复 <c>StopAsync</c> 只派发一次"已停止"。
    /// </summary>
    [Fact]
    public async Task StopAsync_CalledTwice_FiresStoppedOnce()
    {
        var (ctrl, sp) = CreateCtrl();
        var stoppedCount = 0;

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                Interlocked.Increment(ref stoppedCount);
            }
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await started.WaitAsync();
        await ctrl.StopAsync();
        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, stoppedCount);
        sp.Dispose();
    }

    /// <summary>
    /// 启动之后 <c>RunAsync</c> 抛异常（没有 StopAsync）：仍然必须补发一次"已停止"，
    /// 并且用 <see cref="TagsProjectEventArgs.Failure"/> 告诉观察者"这不是正常停机"。
    /// </summary>
    [Fact]
    public async Task StartPollAsync_WhenRunAsyncThrows_EmitsStoppedWithFailure()
    {
        var (ctrl, sp) = CreateCtrl();
        var startedArgs = new TaskCompletionSource<TagsProjectEventArgs>();
        var stoppedArgs = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (args.IsStarted())
            {
                startedArgs.TrySetResult(args);
            }
            else
            {
                stoppedArgs.TrySetResult(args);
            }
        };

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, _, _) =>
            {
                ((MockTagsProject)proj).RunAsyncThrows = true;
                return Task.CompletedTask;
            }));

        var started = await startedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => startTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("模拟的 RunAsync 异常", ex.Message);

        var stopped = await stoppedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TagsProjectState.Stopped, stopped.State);
        Assert.Equal(started.RunId, stopped.RunId);
        Assert.NotNull(stopped.Failure);
        Assert.Contains("模拟的 RunAsync 异常", stopped.Failure!.Message);
        Assert.Equal(TagsProjectState.Stopped, ctrl.State);
        sp.Dispose();
    }

    /// <summary>
    /// 启动之后 <c>RunAsync</c> 自行正常结束（没有 StopAsync）：同样补发"已停止"，只是 <c>Failure</c> 为 null。
    /// </summary>
    [Fact]
    public async Task StartPollAsync_WhenRunAsyncCompletesByItself_EmitsStoppedWithoutFailure()
    {
        var (ctrl, sp) = CreateCtrl();
        var stoppedArgs = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                stoppedArgs.TrySetResult(args);
            }
        };

        await ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, _, _) =>
            {
                ((MockTagsProject)proj).RunAsyncCompletesImmediately = true;
                return Task.CompletedTask;
            });

        var stopped = await stoppedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(stopped.Failure);
        Assert.Equal(TagsProjectState.Stopped, stopped.State);
        Assert.Equal(TagsProjectState.Stopped, ctrl.State);
        Assert.Null(ctrl.Project);
        sp.Dispose();
    }

    /// <summary>
    /// "已启动"的订阅者抛异常：<b>启动必须照常成功</b>，并且排在它后面的订阅者仍然收到通知。
    /// （项目级观测是纯旁路，不能因为一个写坏的观察者就让整个项目起不来。）
    /// </summary>
    [Fact]
    public async Task StartPollAsync_WhenStartedHandlerThrows_StartupSucceedsAndLaterHandlersRun()
    {
        var logs = new CapturingLoggerProvider();
        var (ctrl, sp) = CreateCtrl(logs);
        var laterHandlerFired = new TaskCompletionSource<bool>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (args.IsStarted())
            {
                throw new InvalidOperationException("启动通知处理器-boom");
            }
        };
        ctrl.StartedOrStopped += (_, args) =>
        {
            if (args.IsStarted())
            {
                laterHandlerFired.TrySetResult(true);
            }
        };

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await laterHandlerFired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TagsProjectState.Running, ctrl.State);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5)); // 不应抛出

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Exception?.Message == "启动通知处理器-boom");
        sp.Dispose();
    }

    /// <summary>
    /// "已启动"与"已停止"携带同一个 <see cref="TagsProjectEventArgs.RunId"/>；重新启动后 RunId 递增。
    /// </summary>
    [Fact]
    public async Task StartedAndStopped_ShareRunId_AndRunIdIncreasesPerStart()
    {
        var (ctrl, sp) = CreateCtrl();
        var events = new List<TagsProjectEventArgs>();
        var started = new SemaphoreSlim(0);
        var stopped = new SemaphoreSlim(0);

        ctrl.StartedOrStopped += (_, args) =>
        {
            lock (events)
            {
                events.Add(args);
            }

            if (args.IsStarted())
            {
                started.Release();
            }
            else
            {
                stopped.Release();
            }
        };

        for (var round = 0; round < 2; round++)
        {
            var startTask = Task.Run(() => ctrl.StartPollAsync(
                dir: "test_dir",
                root: new XElement("Project"),
                hook: (_, _, _) => Task.CompletedTask));

            Assert.True(await started.WaitAsync(TimeSpan.FromSeconds(5)));
            await ctrl.StopAsync();
            Assert.True(await stopped.WaitAsync(TimeSpan.FromSeconds(5)));
            await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        TagsProjectEventArgs[] snapshot;
        lock (events)
        {
            snapshot = events.ToArray();
        }

        Assert.Equal(4, snapshot.Length);
        Assert.Equal(snapshot[0].RunId, snapshot[1].RunId);
        Assert.Equal(snapshot[2].RunId, snapshot[3].RunId);
        Assert.True(snapshot[2].RunId > snapshot[0].RunId, "每次启动应分配新的 RunId");
        sp.Dispose();
    }

    /// <summary>
    /// <see cref="ITagsProjectCtrl.TryGetRunning"/>：一次读取就拿到配对的 <c>(Project, RunId)</c>，
    /// 且只在 <see cref="TagsProjectState.Running"/> 期间返回 true（<c>hook</c> 执行期间是 <c>Starting</c>，应为 false）。
    /// </summary>
    [Fact]
    public async Task TryGetRunning_ReturnsAtomicPairOnlyWhileRunning()
    {
        var (ctrl, sp) = CreateCtrl();
        var startedArgs = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (args.IsStarted())
            {
                startedArgs.TrySetResult(args);
            }
        };

        var insideHookResult = true;
        long insideHookRunId = -1;

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) =>
            {
                insideHookResult = ctrl.TryGetRunning(out _, out insideHookRunId);
                return Task.CompletedTask;
            }));

        var args = await startedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // hook 期间是 Starting：即便 Project 已赋值，也不该被当成"正在运行"
        Assert.False(insideHookResult);
        Assert.Equal(0, insideHookRunId);

        // 已启动之后：快照里的项目和 RunId 与"已启动"事件完全一致
        Assert.True(ctrl.TryGetRunning(out var project, out var runId));
        Assert.Same(ctrl.Project, project);
        Assert.Equal(args.RunId, runId);

        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(ctrl.TryGetRunning(out _, out var stoppedRunId));
        Assert.Equal(0, stoppedRunId);
        sp.Dispose();
    }

    /// <summary>
    /// 正常停机时，即便 <c>RunAsync</c> 以 <see cref="OperationCanceledException"/> 结束，"已停止"里的
    /// <see cref="TagsProjectEventArgs.Failure"/> 也必须为 <c>null</c> —— 判据是"令牌确实已取消"，
    /// 否则每次正常停机都会被误报成"崩了"。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenRunAsyncEndsWithOce_StoppedHasNoFailure()
    {
        var (ctrl, sp) = CreateCtrl();
        var stoppedArgs = new TaskCompletionSource<TagsProjectEventArgs>();

        ctrl.StartedOrStopped += (_, args) =>
        {
            if (!args.IsStarted())
            {
                stoppedArgs.TrySetResult(args);
            }
        };

        using var started = WatchProjectStarted(ctrl);
        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (proj, _, _) =>
            {
                ((MockTagsProject)proj).RunAsyncThrowsOceOnCancel = true;
                return Task.CompletedTask;
            }));

        await started.WaitAsync();
        await ctrl.StopAsync();

        // OCE 是停机引起的，不算失败：StartPollAsync 正常结束（不抛），停止事件也不带 Failure
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        var stopped = await stoppedArgs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(stopped.Failure);
        sp.Dispose();
    }

    /// <summary>
    /// 用于测试通道断开连接的模拟通道。
    /// </summary>
    private class MockChannel : ITagChannel
    {
        public TagChannelDescriptor Descriptor => new TagChannelDescriptor
        {
            Name = "mock",
            Driver = "mock",
        };
        public Func<CancellationToken, Task> DisconnectAsyncImpl { get; set; } = _ => Task.CompletedTask;
        public Func<bool, CancellationToken, Task> EnsureConnectedAsyncImpl { get; set; } = (_, _) => Task.CompletedTask;
        public Action DisposeImpl { get; set; } = () => { };

        public Task DisconnectAsync(CancellationToken ct) => DisconnectAsyncImpl(ct);
        public Task EnsureConnectedAsync(bool force, CancellationToken ct) => EnsureConnectedAsyncImpl(force, ct);
        public void Dispose() => DisposeImpl();
    }
}

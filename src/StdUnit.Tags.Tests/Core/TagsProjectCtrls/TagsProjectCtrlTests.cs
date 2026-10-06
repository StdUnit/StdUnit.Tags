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
                if (args.IsStarted)
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
            if (args.IsStarted)
            {
                startEventFired.TrySetResult(args);
            }
        };

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        var eventArgs = await startEventFired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(eventArgs.IsStarted);
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
            if (!args.IsStarted)
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

        Assert.False(eventArgs.IsStarted);
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
            if (!args.IsStarted)
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

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Exception?.Message == "停止通知处理器-boom");
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

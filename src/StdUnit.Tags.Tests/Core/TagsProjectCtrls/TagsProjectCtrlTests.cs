using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using StdUnit.Tags.Tests.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

public class TagsProjectCtrlTests
{
    private readonly MockTagsProjectFactory _factory = new();

    /// <summary>
    /// 注册 <seealso cref="MockTagsProjectFactory "/>, 
    /// 并创建 <seealso cref="TagsProjectCtrl"/>。
    /// </summary>
    private (TagsProjectCtrl ctrl, ServiceProvider sp) CreateCtrl()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITagsProjectFactory>(_factory);
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var ssf = sp.GetRequiredService<IServiceScopeFactory>();
        var logger = sp.GetRequiredService<ILogger<TagsProjectCtrl>>();
        var ctrl = new TagsProjectCtrl(ssf, logger);
        return (ctrl, sp);
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
        await startTask.WaitAsync(TimeSpan.FromSeconds(1));

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

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        // 等待启动，然后停止
        await Task.Delay(200);
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

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        // 确保第一个已启动
        await Task.Delay(200);

        // 第二次启动应抛出异常
        var ex = await Assert.ThrowsAsync<Exception>(() =>
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

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await Task.Delay(200);

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

        var startTask = Task.Run(() => ctrl.StartPollAsync(
            dir: "test_dir",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await Task.Delay(200);

        var ex = await Assert.ThrowsAsync<Exception>(() =>
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

        await Task.Delay(200);
        await ctrl.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(channelDisconnected, "通道应被断开连接");
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
        var startTask1 = Task.Run(() => ctrl.StartPollAsync(
            dir: "dir1",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await Task.Delay(200);
        await ctrl.StopAsync();
        await startTask1.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(ctrl.Project);

        // 第二次启动
        var startTask2 = Task.Run(() => ctrl.StartPollAsync(
            dir: "dir2",
            root: new XElement("Project"),
            hook: (_, _, _) => Task.CompletedTask));

        await Task.Delay(200);
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

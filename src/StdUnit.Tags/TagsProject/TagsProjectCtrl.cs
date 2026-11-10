using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点项目控制器
/// </summary>
internal class TagsProjectCtrl : ITagsProjectCtrl
{
    private readonly IServiceScopeFactory _ssf;
    private readonly ILogger<TagsProjectCtrl> _logger;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="ssf"></param>
    /// <param name="logger"></param>
    public TagsProjectCtrl(IServiceScopeFactory ssf, ILogger<TagsProjectCtrl> logger)
    {
        this._ssf = ssf;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public ITagsProject? Project => this._project;

    /// <inheritdoc/>
    public TagsProjectState State => (TagsProjectState)Volatile.Read(ref this._state);

    /// <inheritdoc/>
    public long RunId => Volatile.Read(ref this._runId);

    /// <inheritdoc/>
    public bool TryGetRunning(out ITagsProject project, out long runId)
    {
        // 单次 volatile 读：Project 与 RunId 来自同一个不可变快照，绝不会拼出"旧项目 + 新 RunId"。
        if (this._running is { } snapshot)
        {
            project = snapshot.Project;
            runId = snapshot.RunId;
            return true;
        }

        project = null!;
        runId = 0;
        return false;
    }

    /// <summary>
    /// "正在运行的项目 + 它的运行标识"的不可变快照。只在 <see cref="TagsProjectState.Running"/> 期间非 null，
    /// 用于让 <see cref="TryGetRunning"/> 能用<b>一次</b>读取取到配对的一对值。
    /// </summary>
    private sealed class RunSnapshot
    {
        internal RunSnapshot(ITagsProject project, long runId)
        {
            this.Project = project;
            this.RunId = runId;
        }

        internal ITagsProject Project { get; }

        internal long RunId { get; }
    }

    private volatile RunSnapshot? _running;

    private volatile ITagsProject? _project;

    /// <summary>
    /// <see cref="TagsProjectState"/> 的底层存储（用 <see cref="Volatile"/> 读写，保证 <see cref="State"/> 可跨线程查询）。
    /// </summary>
    private int _state = (int)TagsProjectState.Stopped;

    CancellationTokenSource? _cts;

    private int _lock = 0;

    #region 单次运行的会话状态
    // 下面这些字段都由 _lock 串行化：同一时刻最多只有一次运行，所以进入 StartPollAsync 后重置即可。
    // 但清理路径可能由 StartPollAsync 的 finally 或 StopAsync 之一执行，故用 Interlocked 保证"只做一次"。

    /// <summary>运行标识，每次启动递增；"已启动"/"已停止"携带同一个值，便于配对。</summary>
    private long _runId;

    /// <summary>本次运行是否已对外派发过"已启动"（只有派发过才需要补"已停止"）。</summary>
    private int _startedNotified;

    /// <summary>本次运行是否已派发过"已停止"（保证最多一次）。</summary>
    private int _stopNotified;

    /// <summary>通道是否已断开（保证最多一次）。</summary>
    private int _channelsDisconnected;

    /// <summary>是否已由 <see cref="StopAsync"/> 接管收尾（此时 <see cref="StartPollAsync"/> 的 finally 不再重复断开通道/派发事件）。</summary>
    private int _stopping;

    /// <summary>导致本次运行结束的异常；正常停机为 null。会随"已停止"事件一起派发。</summary>
    private volatile Exception? _failure;
    #endregion

    /// <summary>
    /// 离开 <see cref="TagsProjectState.Running"/> 时清空快照：此后 <see cref="TryGetRunning"/> 立刻返回 false，
    /// 不会在停机过程中继续交出项目。
    /// </summary>
    private void ClearRunning() => this._running = null;

    private void SetState(TagsProjectState state) => Volatile.Write(ref this._state, (int)state);

    /// <summary>
    /// "轮询循环已退出"的信号：由 <see cref="StartPollAsync"/> 的 finally 置位，<see cref="StopAsync"/> 等它。
    /// </summary>
    private TaskCompletionSource<bool>? _pollExitSource;

    /// <summary>
    /// <see cref="StopAsync"/> 等待轮询循环退出的上限。<br/>
    /// 取消是协作式的：正常情况下循环会在本轮结束后立刻退出（毫秒级），这个上限只用于兜住"驱动不响应取消"的极端情况——
    /// 超时不会永久挂住 <see cref="StopAsync"/>，只记 Warning 并继续清理。
    /// </summary>
    internal TimeSpan PollExitWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);


    /// <inheritdoc/>
    public async Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
    {
        // 防止重复启动
        if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
        {
            // 取一次快照再用：属性可能在启动过程中被替换
            var onStartingException = this.OnStartingException;
            if (onStartingException != null)
            {
                var handled = await onStartingException(new InvalidOperationException("当前测点项目已经启动！"));
                if (handled)
                {
                    return;
                }
            }

            throw new InvalidOperationException("当前测点项目已经启动！");
        }

        var runId = Interlocked.Increment(ref this._runId);
        this._startedNotified = 0;
        this._stopNotified = 0;
        this._channelsDisconnected = 0;
        this._stopping = 0;
        this._failure = null;
        this._running = null;
        this.SetState(TagsProjectState.Starting);

        // 先登记"轮询已退出"信号：StopAsync 靠它把"已停止"变成确定性信号（见 StopAsync 的注释）
        var exitSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this._pollExitSource = exitSource;

        // 这两个声明在 try 之外：
        // - ct 供 catch 的 when 过滤条件使用（try 块内声明的局部变量在 catch 里不在作用域内）；
        // - scope 交给 finally 释放（项目里的对象可能引用作用域内的服务，所以必须先放项目、再放作用域）。
        CancellationToken ct = default;
        IServiceScope? scope = null;

        try
        {
            scope = this._ssf.CreateScope();
            var sp = scope.ServiceProvider;

            var cts = new CancellationTokenSource();
            this._cts = cts;
            ct = cts.Token;
            var project = sp.MakeProject(dir, root);
            this._project = project;
            await hook(project, sp, ct);

            // hook 已执行完、轮询即将开始：这是唯一的"已就绪"时刻
            this.SetState(TagsProjectState.Running);
            this._running = new RunSnapshot(project, runId);
            Volatile.Write(ref this._startedNotified, 1);
            this.RaiseStartedOrStopped(new TagsProjectEventArgs(TagsProjectState.Running, project, runId, null));

            await project.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常停机：取消不是失败（与 RunnerCrashed 的判据一致）。不记 Error、不填 Failure、不外抛——
            // 否则 StopAsync 引发的 OCE 会让每次正常停机都表现成"启动失败"。
        }
        catch (Exception ex)
        {
            this._failure = ex;
            this._logger.LogError(ex, "TagsProjectCtrl.StartPollAsync error");

            // 取一次快照再用：属性可能在启动过程中被替换
            var onStartingException = this.OnStartingException;
            if (onStartingException == null)
            {
                throw;
            }

            var handled = await onStartingException(ex);
            // 如果用户处理了异常，则不再向外抛出
            if (handled)
            {
                return;
            }

            // 如果用户没有处理异常，则继续向外抛出
            throw;
        }
        finally
        {
            var project = this._project;
            if (project is not null)
            {
                try
                {
                    project.Dispose();
                }
                catch (Exception ex)
                {
                    // 有意吞掉：清理失败不应覆盖轮询阶段抛出的原始异常。但不能静默——留痕以便定位资源泄漏。
                    this._logger.LogWarning(ex, "释放测点项目失败（项目根目录={ProjectRoot}），资源可能未完全释放", project.ProjectRoot);
                }
                finally
                {
                    this._project = null;
                }
            }

            this._cts = null;

            // 正常停机由 StopAsync 接管收尾（它要在断开通道之后才派发"已停止"）。
            // 其余路径（启动失败、RunAsync 自行结束）没有 StopAsync，必须由这里补上收尾——
            // 否则"已启动"永远等不到"已停止"，观察者的状态会永远停在 Running，通道也不会断开。
            if (Volatile.Read(ref this._stopping) == 0)
            {
                this.SetState(TagsProjectState.Stopping);
                this.ClearRunning();
                await this.DisconnectChannelsAsync(project);
                this.RaiseStoppedOnce();
                this.SetState(TagsProjectState.Stopped);
            }

            Interlocked.Exchange(ref this._lock, 0);

            // 最后才置位：保证等到它的 StopAsync 看到的是"项目已释放、锁已释放"的完整状态
            exitSource.TrySetResult(true);
            if (ReferenceEquals(this._pollExitSource, exitSource))
            {
                this._pollExitSource = null;
            }

            // DI 作用域放在最后释放：通道实例可能来自作用域内的服务，必须先让它们断开（上面那步）、
            // 再放作用域。这也保持了原来 `using var scope` 的"方法结束才释放"的时序。
            if (scope is not null)
            {
                try
                {
                    scope.Dispose();
                }
                catch (Exception ex)
                {
                    // 有意吞掉：作用域释放失败不应覆盖原始异常。但不能静默——留痕。
                    this._logger.LogWarning(ex, "释放 DI 作用域失败，资源可能未完全释放");
                }
            }
        }
    }


    /// <inheritdoc/>
    public async Task StopAsync()
    {
        // 如果测点项目尚未运行，直接返回：重复调用也不应该重复派发"已停止"事件——观察者不该看到一次没人宣布过的停止。
        if (Volatile.Read(ref this._lock) == 0)
        {
            return;
        }

        // 声明"收尾由我负责"：StartPollAsync 的 finally 会据此跳过断开通道/派发事件，
        // 保证"已停止"发生在通道断开之后（调用方拿它当"可以复用通道/目录"的信号）。
        Interlocked.Exchange(ref this._stopping, 1);
        this.SetState(TagsProjectState.Stopping);
        this.ClearRunning();

        var project = this._project;
        var exitSource = this._pollExitSource;
        try
        {
            if (this._cts != null)
            {
                this._cts.Cancel();
            }
        }
        catch (Exception ex)
        {
            // 有意吞掉：取消失败（如 CTS 已释放）不应阻断后续的清理。但不能静默——留痕。
            this._logger.LogWarning(ex, "取消测点项目轮询时出错（项目根目录={ProjectRoot}）", project?.ProjectRoot);
        }

        // 取消是协作式的：先等轮询循环真正退出。不等它就直接释放/断开，会与循环里的读/写/重连并发——
        // "已停止"之后仍有通道活动，而调用方往往正是拿这个事件当"可以复用通道/目录/换配置"的信号。
        if (exitSource is not null && !await this.WaitPollExitAsync(exitSource))
        {
            // 超时兜底：循环没能退出 ⇒ StartPollAsync 的 finally 还没机会释放项目，这里代为释放。
            // 正常路径下项目已由 StartPollAsync 的 finally 释放并清空，不需要（也不能）重复释放。
            var timedOutProject = this._project;
            if (timedOutProject is not null)
            {
                this._project = null;
                try
                {
                    timedOutProject.Dispose();
                }
                catch (Exception ex)
                {
                    // 有意吞掉：释放失败不应阻断后续的通道断开。但不能静默——留痕以便定位资源泄漏。
                    this._logger.LogWarning(ex, "释放测点项目异常（项目根目录={ProjectRoot}），资源可能未完全释放", timedOutProject.ProjectRoot);
                }
            }
        }

        await this.DisconnectChannelsAsync(project);
        this.RaiseStoppedOnce();
        this.SetState(TagsProjectState.Stopped);

        Interlocked.Exchange(ref _lock, 0);
    }

    /// <summary>
    /// 逐个调用订阅者并<b>分别</b>捕获异常：项目级观测是纯旁路，不能让一个写坏的观察者把启动/停止搞挂，
    /// 也不能让排在它后面的订阅者收不到通知。异常只记日志（Error），因此这里永远不会抛。
    /// </summary>
    private void RaiseStartedOrStopped(TagsProjectEventArgs args)
    {
        var handlers = this.StartedOrStopped;
        if (handlers is null)
        {
            return;
        }

        foreach (TagsProjectStartedOrStopped handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                this._logger.LogError(
                    ex,
                    "StartedOrStopped 的订阅者抛出异常（State={State}, RunId={RunId}），已忽略：订阅者异常不影响项目启停",
                    args.State,
                    args.RunId);
            }
        }
    }

    /// <summary>
    /// 派发"已停止"：<b>最多一次</b>，且只在派发过"已启动"时才发（否则观察者从未被告知启动过，配不上对）。
    /// </summary>
    private void RaiseStoppedOnce()
    {
        if (Interlocked.Exchange(ref this._stopNotified, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref this._startedNotified) == 0)
        {
            return;
        }

        var args = new TagsProjectEventArgs(
            TagsProjectState.Stopped,
            project: null,
            Volatile.Read(ref this._runId),
            this._failure);

        this.RaiseStartedOrStopped(args);
    }

    /// <summary>
    /// 断开通道：<b>最多一次</b>。轮询从未开始时通道也没连过，跳过（避免驱动对"未连接的断开"报噪声）。
    /// </summary>
    private async Task DisconnectChannelsAsync(ITagsProject? project)
    {
        if (Interlocked.Exchange(ref this._channelsDisconnected, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref this._startedNotified) == 0)
        {
            return;
        }

        var channels = project?.Channels;
        if (channels is null)
        {
            return;
        }

        foreach (var ch in channels)
        {
            var channelName = ch?.ChannelName() ?? "null";
            try
            {
                if (ch is not null)
                {
                    await ch.DisconnectAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // 有意吞掉：一个通道断开失败不应阻断其它通道的清理。但不能静默——留痕以便确认连接是否真的断开了。
                this._logger.LogWarning(ex, "断开通道失败：通道={Channel}", channelName);
            }
        }
    }

    /// <summary>
    /// 等待轮询循环退出；返回是否在 <see cref="PollExitWaitTimeout"/> 内退出（未退出时记 Warning）。
    /// </summary>
    private async Task<bool> WaitPollExitAsync(TaskCompletionSource<bool> exitSource)
    {
        var exitTask = exitSource.Task;
        var timeout = this.PollExitWaitTimeout;
        if (exitTask.IsCompleted || timeout <= TimeSpan.Zero)
        {
            return exitTask.IsCompleted;
        }

        var completed = await Task.WhenAny(exitTask, Task.Delay(timeout));
        if (ReferenceEquals(completed, exitTask))
        {
            return true;
        }

        this._logger.LogWarning(
            "等待轮询循环退出超时（{Timeout}），继续清理；若底层驱动不响应取消，可能仍有通道活动。项目根目录={ProjectRoot}",
            timeout, this.Project?.ProjectRoot);
        return false;
    }

    /// <inheritdoc/>
    public event TagsProjectStartedOrStopped? StartedOrStopped;

    /// <inheritdoc/>
    public Func<Exception, Task<bool>>? OnStartingException { get; set; } = null;

}



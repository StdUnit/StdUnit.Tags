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
    public ITagsProject? Project => this.Read().Project;

    /// <inheritdoc/>
    public TagsProjectState State => this.Read().State;

    /// <inheritdoc/>
    public long RunId => this.Read().RunId;

    /// <inheritdoc/>
    public bool TryGetRunning(out ITagsProject project, out long runId)
    {
        // 一次读快照：状态、项目、运行号必然来自同一次运行。
        var current = this.Read();
        if (current.State == TagsProjectState.Running && current.Project is { } running)
        {
            project = running;
            runId = current.RunId;
            return true;
        }

        project = null!;
        runId = 0;
        return false;
    }

    #region 状态快照
    // 控制器的<b>全部</b>可变状态集中在一个不可变快照里，只有这一个字段。
    // 任何迁移 = 构造一个新快照 + CAS 替换，因此：
    //   * 读者一次读取就拿到"状态 + 项目 + 运行标识 + 运行手柄"的一致组合，不会撕裂；
    //   * "只做一次"的副作用（派发事件、断开通道）靠 CAS 认领，只有一个赢家；
    //   * 不再需要 volatile 关键字，也不再需要单独的"门"字段（Handles 非 null 即表示有运行在飞）。

    /// <summary>一次运行的两个手柄：取消源 + "轮询已退出"信号。</summary>
    private sealed class RunHandle
    {
        internal RunHandle(CancellationTokenSource cts, TaskCompletionSource<bool> exitSource)
        {
            this.Cts = cts;
            this.ExitSource = exitSource;
        }

        internal CancellationTokenSource Cts { get; }

        /// <summary>轮询循环已退出的信号：由 <see cref="StartPollAsync"/> 的 finally 置位。</summary>
        internal TaskCompletionSource<bool> ExitSource { get; }
    }

    /// <summary>
    /// 控制器状态的一次快照（不可变）。所有字段一起发布、一起替换。
    /// </summary>
    private sealed record Snapshot
    {
        /// <summary>当前状态。</summary>
        internal TagsProjectState State { get; init; } = TagsProjectState.Stopped;

        /// <summary>当前（或最近一次）运行的标识；每次启动递增。</summary>
        internal long RunId { get; init; }

        /// <summary>当前项目：<see cref="TagsProjectState.Starting"/> / <see cref="TagsProjectState.Running"/> 期间非 null。</summary>
        internal ITagsProject? Project { get; init; }

        /// <summary>当前运行的手柄；<b>非 null 即表示"有运行在飞"</b>，也就是旧实现里的那道门。</summary>
        internal RunHandle? Handles { get; init; }

        /// <summary>导致本次运行结束的异常；正常停机为 null。</summary>
        internal Exception? Failure { get; init; }

        /// <summary>本次运行是否已对外派发过"已启动"。</summary>
        internal bool StartedRaised { get; init; }

        /// <summary>本次运行是否已派发过"已停止"。</summary>
        internal bool StoppedRaised { get; init; }

        /// <summary>本次运行的通道是否已断开。</summary>
        internal bool ChannelsDisconnected { get; init; }

        /// <summary>收尾是否已由 <see cref="StopAsync"/> 接管（<see cref="StartPollAsync"/> 的 finally 据此跳过重复收尾）。</summary>
        internal bool StopOwnsCleanup { get; init; }
    }

    private Snapshot _snapshot = new();

    private Snapshot Read() => Volatile.Read(ref this._snapshot);

    /// <summary>
    /// 读取当前状态 curr，计算 mutate(curr) 得到新的状态 next，然后以CAS的手法更新快照。<br/>
    /// <br/>
    /// 函数 <paramref name="mutate"/> 返回 <c>null</c> 表示"条件不成立、什么也不做"； 返回非<c>null</c>表述需要把它的结果 CAS 进去。<br/>
    /// 如果 CAS 失败，就基于最新快照重试。<br/>
    /// <br/>
    /// <b>约定</b>：<paramref name="mutate"/> 必须是<b>纯函数</b>(只看快照、不产生副作用)，
    /// 因为它可能被反复重试。
    /// </summary>
    private bool TryUpdate(Func<Snapshot, Snapshot?> mutate)
    {
        while (true)
        {
            var current = this.Read();
            var next = mutate(current);
            if (next is null)
            {
                return false;
            }

            if (this.TryOptimSwap(current, next))
            {
                return true;
            }

            // 运行至此，说明有人抢先改了快照，重试
        }
    }

    /// <summary>
    /// 乐观锁实现：仅当快照仍是 <paramref name="expected"/> 时，才替换为 <paramref name="next"/>。
    /// </summary>
    /// <param name="expected">期望当前版本</param>
    /// <param name="next">要替换的新快照</param>
    /// <returns>如果替换成功返回 true，否则返回 false</returns>
    private bool TryOptimSwap(Snapshot expected, Snapshot next)
    {
        var original = Interlocked.CompareExchange(ref this._snapshot, next, expected);
        // 对于record，必须用 object.ReferenceEquals(a, b)，不能用 ==
        return ReferenceEquals(original, expected);
    }
    #endregion

    /// <summary>
    /// <see cref="StopAsync"/> 等待轮询循环退出的上限。<br/>
    /// 取消是协作式的：正常情况下循环会在本轮结束后立刻退出（毫秒级），这个上限只用于兜住"驱动不响应取消"的极端情况——
    /// 超时不会永久挂住 <see cref="StopAsync"/>，只记 Warning 并继续清理。
    /// </summary>
    internal TimeSpan PollExitWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);


    /// <inheritdoc/>
    public async Task StartPollAsync(string? dir, XElement? root, Func<ITagsProject, IServiceProvider, CancellationToken, Task> hook)
    {
        // 抢门
        var cts = new CancellationTokenSource();
        var exitSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!this.TryUpdate(current => current.Handles is null ? 
            current with {
                    State = TagsProjectState.Starting,
                    RunId = current.RunId + 1,
                    Handles = new RunHandle(cts, exitSource),
                    Project = null,
                    Failure = null,
                    StartedRaised = false,
                    StoppedRaised = false,
                    ChannelsDisconnected = false,
                    StopOwnsCleanup = false,
                }
            : null)
        )
        {
            // 失败
            cts.Dispose();
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

        // 运行至此，说明抢门成功，之后的整组值都从这里读，保证是"本次运行"的。
        var run = this.Read();
        var runId = run.RunId;
        var handles = run.Handles!;
        var ct = handles.Cts.Token;

        IServiceScope? scope = null;
        try
        {
            scope = this._ssf.CreateScope();
            var sp = scope.ServiceProvider;

            var project = sp.MakeProject(dir, root);
            this.TryUpdate(current => current with { Project = project });
            await hook(project, sp, ct);

            // hook 已执行完、轮询即将开始：这是唯一的"已就绪"时刻。
            // 一次迁移同时完成"进入 Running"与"已宣告启动"标记——只有本次运行的所有者会执行到这里，且只执行一次。
            this.TryUpdate(current => current with { State = TagsProjectState.Running, StartedRaised = true });
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
            this.TryUpdate(current => current with { Failure = ex });
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
            var current = this.Read();
            var project = current.Project;

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
                    this.TryUpdate(s => s with { Project = null });
                }
            }

            // 正常停机由 StopAsync 接管收尾（它要在断开通道之后才派发"已停止"）。
            // 其余路径（启动失败、RunAsync 自行结束）没有 StopAsync，必须由这里补上收尾——
            // 否则"已启动"永远等不到"已停止"，观察者的状态会永远停在 Running，通道也不会断开。
            if (!current.StopOwnsCleanup)
            {
                this.TryUpdate(s => s with { State = TagsProjectState.Stopping });
                await this.DisconnectChannelsAsync(project);
                this.RaiseStoppedOnce();
            }

            // 结束这次运行：清手柄（= 释放门）、状态归位。门只在这里释放，StopAsync 绝不碰。
            this.TryUpdate(s => s with
            {
                State = TagsProjectState.Stopped,
                Handles = null,
                Project = null,
            });

            // 最后才置位：保证等到它的 StopAsync 看到的是"项目已释放、门已释放"的完整状态
            handles.ExitSource.TrySetResult(true);

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
        // 一次读快照就拿到"有没有运行 + 手柄 + 项目"，不会读到半成品。
        var current = this.Read();
        var handles = current.Handles;
        if (handles is null)
        {
            // 未运行（或刚结束）：空操作，不派生"已停止"——观察者不该看到一次没人宣布过的停止。
            return;
        }

        // 声明"收尾由我负责"：StartPollAsync 的 finally 会据此跳过断开通道/派发事件，
        // 保证"已停止"发生在通道断开之后（调用方拿它当"可以复用通道/目录"的信号）。
        // 同时进入 Stopping：TryGetRunning 立刻返回 false，不再交出正在拆的项目。
        this.TryUpdate(s => s with { State = TagsProjectState.Stopping, StopOwnsCleanup = true });

        var project = current.Project;
        try
        {
            handles.Cts.Cancel();
        }
        catch (Exception ex)
        {
            // 有意吞掉：取消失败（如 CTS 已释放）不应阻断后续的清理。但不能静默——留痕。
            // 这里不记项目根目录：能走到这个 catch 的前提是 Cancel() 执行了注册回调并抛异常，
            // 那时项目必定已创建（回调只可能由 hook/项目自己注册），写 project?.ProjectRoot 是永远走不到的分支。
            this._logger.LogWarning(ex, "取消测点项目轮询时出错");
        }

        // 取消是协作式的：先等轮询循环真正退出。不等它就直接释放/断开，会与循环里的读/写/重连并发——
        // "已停止"之后仍有通道活动，而调用方往往正是拿这个事件当"可以复用通道/目录/换配置"的信号。
        if (!await this.WaitPollExitAsync(handles.ExitSource))
        {
            // 超时兜底：循环没能退出 ⇒ StartPollAsync 的 finally 还没机会释放项目，这里代为释放。
            // 正常路径下项目已由 StartPollAsync 的 finally 释放并清空，不需要（也不能）重复释放。
            var timedOutProject = this.Read().Project;
            if (timedOutProject is not null)
            {
                this.TryUpdate(s => s with { Project = null });
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
        this.TryUpdate(s => s with { State = TagsProjectState.Stopped });

        // 有意不碰 Handles：那是 StartPollAsync 的独占门，只有它的 finally 能释放。
        // 这里若顺手清掉，会开出两个真实的洞：
        //   ① 启动窗口里进来时（手柄刚发布、hook 还没跑）→ 替一次"还没开始"的运行开了门，新的启动就能挤进来并发跑；
        //   ② 正常停机时这里若在 StartPollAsync 之后才执行 → 把"刚启动的另一次运行"的门关回去。
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
        if (!this.TryUpdate(s => s.StoppedRaised || !s.StartedRaised ? null : s with { StoppedRaised = true }))
        {
            return;
        }

        var current = this.Read();
        var args = new TagsProjectEventArgs(
            TagsProjectState.Stopped,
            project: null,
            current.RunId,
            current.Failure);

        this.RaiseStartedOrStopped(args);
    }

    /// <summary>
    /// 断开通道：<b>最多一次</b>。轮询从未开始时通道也没连过，跳过（避免驱动对"未连接的断开"报噪声）。
    /// </summary>
    private async Task DisconnectChannelsAsync(ITagsProject? project)
    {
        // 没有项目就没有通道可断（启动阶段在 MakeProject 之前失败时会传 null）。
        if (project is null)
        {
            return;
        }

        // 认领"由我来断"：CAS 失败说明已经断过（或本次从未宣告过启动，通道根本没连过）。
        if (!this.TryUpdate(s => s.ChannelsDisconnected || !s.StartedRaised ? null : s with { ChannelsDisconnected = true }))
        {
            return;
        }

        // 接口声明 Channels 非 null，但第三方实现可能违反：此时同样没有通道可断。
        var channels = project.Channels;
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



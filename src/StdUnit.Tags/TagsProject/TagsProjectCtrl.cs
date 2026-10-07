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
    public ITagsProject? Project { get; private set; }

    CancellationTokenSource? _cts;

    private int _lock = 0;

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
        if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
        {
            if (this.OnStartingException != null)
            {
                var handled = await this.OnStartingException(new InvalidOperationException("当前测点项目已经启动！"));
                if (handled)
                {
                    return;
                }
            }

            throw new InvalidOperationException("当前测点项目已经启动！");
        }

        // 先登记"轮询已退出"信号：StopAsync 靠它把"已停止"变成确定性信号（见 StopAsync 的注释）
        var exitSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this._pollExitSource = exitSource;

        using var scope = this._ssf.CreateScope();
        var sp = scope.ServiceProvider;


        try
        {
            this._cts = new CancellationTokenSource();
            var project = sp.MakeProject(dir, root);
            this.Project = project;
            var ct = _cts.Token;
            await hook(project, sp, ct);
            this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(true, project));
            await project.RunAsync(ct);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "TagsProjectCtrl.StartPollAsync error");

            // 如果用户没有注册OnStartException回调，则直接抛出异常
            if (this.OnStartingException == null)
            {
                throw;
            }

            var handled = await this.OnStartingException(ex);

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
            var project = this.Project;
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
                    this.Project = null;
                }
            }

            this._cts = null;
            Interlocked.Exchange(ref this._lock, 0);

            // 最后才置位：保证等到它的 StopAsync 看到的是"项目已释放、锁已释放"的完整状态
            exitSource.TrySetResult(true);
            if (ReferenceEquals(this._pollExitSource, exitSource))
            {
                this._pollExitSource = null;
            }
        }
    }


    /// <inheritdoc/>
    public async Task StopAsync()
    {
        var project = this.Project;
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

        // 取消是协作式的：先等轮询循环真正退出（它会在退出路径里断开自己的通道）。
        // 不等它就直接释放/断开，会与循环里的读/写/重连并发——"已停止"之后仍有通道活动，
        // 而调用方往往正是拿这个事件当"可以复用通道/目录/换配置"的信号。
        if (exitSource is not null && !await this.WaitPollExitAsync(exitSource))
        {
            // 超时兜底：循环没能退出 ⇒ StartPollAsync 的 finally 还没机会释放项目，这里代为释放。
            // 正常路径下项目已由 StartPollAsync 的 finally 释放并清空，不需要（也不能）重复释放。
            var timedOutProject = this.Project;
            if (timedOutProject is not null)
            {
                this.Project = null;
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

        var oldchannels = project?.Channels;
        try
        {
            // disconnect from each channel
            if (oldchannels is not null)
            {
                foreach (var ch in oldchannels)
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

            this.StartedOrStopped?.Invoke(this, new TagsProjectEventArgs(false, null));
        }
        catch (Exception ex)
        {
            // 有意吞掉：StartedOrStopped 的事件处理器抛错不应让 StopAsync 失败。但不能静默——留痕。
            this._logger.LogWarning(ex, "向 StartedOrStopped 事件处理器派发\"已停止\"通知时出错");
        }

        Interlocked.Exchange(ref _lock, 0);
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



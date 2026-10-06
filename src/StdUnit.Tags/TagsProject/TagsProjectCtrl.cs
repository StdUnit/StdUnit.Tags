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
        }
    }


    /// <inheritdoc/>
    public async Task StopAsync()
    {
        var project = this.Project;
        try
        {
            if (this._cts != null)
            {
                this._cts.Cancel();
            }
            if (project is not null)
            {
                this.Project = null;
                try
                {
                    project.Dispose();
                }
                catch (Exception ex)
                {
                    // 有意吞掉：释放失败不应阻断后续的通道断开。但不能静默——留痕以便定位资源泄漏。
                    this._logger.LogWarning(ex, "释放测点项目异常（项目根目录={ProjectRoot}），资源可能未完全释放", project.ProjectRoot);
                }
            }
        }
        catch (Exception ex)
        {
            // 有意吞掉：取消失败（如 CTS 已释放）不应阻断后续的通道断开。但不能静默——留痕。
            this._logger.LogWarning(ex, "取消测点项目轮询时出错（项目根目录={ProjectRoot}）", project?.ProjectRoot);
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

    /// <inheritdoc/>
    public event TagsProjectStartedOrStopped? StartedOrStopped;

    /// <inheritdoc/>
    public Func<Exception, Task<bool>>? OnStartingException { get; set; } = null;

}



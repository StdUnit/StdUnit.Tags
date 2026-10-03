using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Itminus.Tags;

/// <summary>
/// <see cref="ITagGrpRunner"/> 的默认实现。<br/>
/// <b>多通道</b>：入口子树中所有会被用到的通道（见 <see cref="ITagGrpExtensions.CollectChannels"/>）
/// 会在每轮读写之前逐一建连；入口自身解析出的通道额外称为<b>主通道</b>，即各事件委托中的 channel 参数。<br/>
/// <b>通道独占约束</b>：清理路径会断开入口相关的<b>全部</b>通道，因此同一通道实例不得被多个入口
/// （含嵌套入口）共用，否则一方崩溃/取消会掐掉另一方正在使用的连接。
/// </summary>
internal class TagGrpRunner : ITagGrpRunner
{
    private readonly ITagsProject _project;
    private readonly ILogger<TagGrpRunner> _logger;
    private readonly ITagGrpRunnerRetryStrategy _retryStrategy;
    private readonly ITagGrpRunnerPollDelayStrategy _pollDelayStrategy;
    private readonly ITagGrpRunnerDisconnectStrategy _disconnectStrategy;
    private int _consecutiveFailures;

    /// <summary>
    /// c'tor
    /// </summary>
    public TagGrpRunner(
        ITagsProject project,
        ILogger<TagGrpRunner> logger,
        ITagGrpRunnerRetryStrategy? retryStrategy = null,
        ITagGrpRunnerPollDelayStrategy? pollDelayStrategy = null,
        ITagGrpRunnerDisconnectStrategy? disconnectStrategy = null)
    {
        this._project = project;
        this._logger = logger;
        this._retryStrategy = retryStrategy ?? new DefaultTagGrpRunnerRetryStrategy();
        this._pollDelayStrategy = pollDelayStrategy ?? new AdaptivePollDelayStrategy();
        this._disconnectStrategy = disconnectStrategy ?? new DefaultTagGrpRunnerDisconnectStrategy();
    }

    /// <inheritdoc/>
    public event RunnerStarted? RunnerStarted;

    /// <inheritdoc/>
    public event TurnProcess? TurnProcess;

    /// <inheritdoc/>
    public event RunnerCrashed? RunnerCrashed;

    /// <inheritdoc/>
    public virtual async Task StartAsync(ITagGrp entry, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 入口主通道。各事件委托中的 channel 参数恒为它。
            ITagChannel? channel = null;
            // 本次启动需要确保连接的全部通道（含主通道，主通道排在最前）。
            // 用空集合初始化，保证即使“解析通道”阶段抛异常，清理路径也不会空引用。
            IReadOnlyList<ITagChannel> channels = Array.Empty<ITagChannel>();
            // 本次启动是否失败？如果入口被禁用，不会被视为失败，即禁用会复位失败计数器
            var failed = false;
            try
            {
                if (!entry.IsEnabled)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                channel = entry.SearchChannel();
                channels = entry.CollectChannels();
                if (RunnerStarted is not null)
                {
                    await RunnerStarted(entry, channel);
                }

                // 开始轮询
                var sw = new Stopwatch();
                while (!ct.IsCancellationRequested)
                {
                    sw.Restart();

                    // 确保本轮会用到的所有通道都已连接：主通道 + 入口子树中的辅通道。
                    // 单入口多通道场景下，辅通道（如入口下某个子组自己声明的 channel）必须在这里就建连，
                    // 否则要到读/写该子树的测点时才失败——此时前面的读写已经做完，整轮作废。
                    // EnsureConnectedAsync(force:false) 是幂等的：已连接时各驱动直接返回，开销极小。
                    foreach (var ch in channels)
                    {
                        await ch.EnsureConnectedAsync(force: false, ct);
                    }

                    await this.DrainWriteIntentsAsync(entry, ct);
                    if (entry.IsDirty())
                    {
                        await entry.WriteAsync(ct);
                    }
                    await entry.ReadAsync(ct);
                    if (TurnProcess is not null)
                    {
                        await TurnProcess(entry, channel);
                    }
                    await entry.WriteAsync(ct);

                    sw.Stop();
                    var span = TimeSpan.FromMilliseconds(entry.SearchScanInterval() ?? 0);
                    var delay = this._pollDelayStrategy.GetDelay(span, sw.Elapsed);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, ct);
                    }
                    else
                    {
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception ex)
            {
                failed = true;
                _consecutiveFailures++;

                try
                {
                    if (RunnerCrashed is not null)
                    {
                        try
                        {
                            await RunnerCrashed(entry, channel, ex);
                        }
                        catch (Exception handlingError)
                        {
                            this._logger.LogCritical(
                                "测点分组(分组={grp},通道={channel})错误处理又抛出了错误，这破坏了错误处理不能再抛出异常的假设。err={errMsg}\r\nStackTrace={strace}",
                                entry.TagName(),
                                channel?.ChannelName() ?? "null",
                                handlingError.Message,
                                handlingError.StackTrace
                                );
                            throw;
                        }
                    }
                }
                finally
                {
                    try
                    {
                        // 注意：这里必须使用 CancellationToken.None 而非 ct。
                        // 本 catch 块的触发原因可能是 轮询循环中的 Task.Delay(ct) 抛 OCE。
                        // 若沿用已取消的 ct，通道内部（如 S7 的 ExecuteOneByOneAsync 中 _rw.WaitAsync(ct)）会立即抛
                        // OperationCanceledException，导致 DisconnectAsync 根本没执行——连接资源泄漏，
                        // 下次 StartAsync 时旧连接可能仍处于异常状态。
                        // 断开连接是清理动作，应尽力完成，不应受取消影响。
                        //
                        // 等待策略：断开是异步的，但 PLC/Modbus 设备多有连接数限制：
                        // - Smart200/S1200 同一时刻只允许有几个连接)，
                        // - 串口设备更是只能独占。
                        // 若 fire-and-forget 后立刻重连（失败路径）或立刻重启（取消路径），
                        // 上一连接可能还没断开，导致"连接数超限"失败。因此这里委托给断开策略（默认有限超时等待），
                        // 由策略决定如何等待——正常情况毫秒级完成；超时则放弃等待、立即退出，后台仍会继续清理。
                        //
                        // 多通道：这里断开的是本轮涉及的全部通道（不只主通道），
                        // 否则辅通道的连接会一直残留、占用设备连接数。
                        await this.DisconnectAllAsync(channels);
                    }
                    catch
                    {
                        // ignore all the error thrown by the Channel's DisconnectAsync() method
                    }
                }
            }
            finally
            {
                // 成功迭代则重置连续失败计数器；失败则使用策略等待
                if (!failed)
                {
                    _consecutiveFailures = 0;
                }

                var delay = _consecutiveFailures > 0
                    ? _retryStrategy.GetDelay(_consecutiveFailures)
                    : TimeSpan.FromMilliseconds(entry.SearchScanInterval() ?? 0);
                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// 断开本轮涉及的所有通道。<br/>
    /// 先<b>全部发起</b>断开（让各通道并发执行断开），再交给 <see cref="ITagGrpRunnerDisconnectStrategy"/>
    /// 逐一等待——这样多个通道的总等待时长不会被简单叠加。参考选型与超时语义见各驱动
    /// <c>DisconnectAsync</c> 及 <see cref="DefaultTagGrpRunnerDisconnectStrategy"/>。
    /// <para>
    /// 断开是清理动作，必须尽力完成：即便轮询循环是因为取消而退出，这里也使用
    /// <see cref="CancellationToken.None"/>（理由见调用点注释）；断开过程中抛出的异常一律吞掉。
    /// </para>
    /// </summary>
    /// <param name="channels">本轮涉及的通道；空集合表示无需断开，此时仍会按既有契约调用一次策略（参数均为 null）</param>
    protected virtual async Task DisconnectAllAsync(IReadOnlyList<ITagChannel> channels)
    {
        var pending = new List<(ITagChannel Channel, Task? Disconnect)>(channels.Count);
        foreach (var ch in channels)
        {
            Task? disconnect = null;
            try
            {
                disconnect = ch.DisconnectAsync(CancellationToken.None);
            }
            catch
            {
                // 发起断开即失败：忽略，继续处理其它通道
            }
            pending.Add((ch, disconnect));
        }

        if (pending.Count == 0)
        {
            await this._disconnectStrategy.WaitDisconnectAsync(null, null);
            return;
        }

        foreach (var (ch, disconnect) in pending)
        {
            try
            {
                await this._disconnectStrategy.WaitDisconnectAsync(ch, disconnect);
            }
            catch
            {
                // 策略等待失败不应阻断其它通道的清理
            }
        }
    }

    /// <summary>
    /// 处理写入意图队列，直到队列为空或者达到容量上限。<br/>
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    protected virtual async Task DrainWriteIntentsAsync(ITagGrp entry, CancellationToken ct)
    {
        var reader = this._project.GetIntentReader(entry.TagName());
        if (reader is null)
        {
            return;
        }

        var count = 0;
        while (reader.TryRead(out var item))
        {
            var intent = item.Intent;
            var tcs = item.Completion;
            try
            {
                await intent(entry, ct);
                tcs.TrySetResult(count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            count++;
            if (count >= this._project.IntentCapacity)
            {
                break;
            }
        }
    }
}

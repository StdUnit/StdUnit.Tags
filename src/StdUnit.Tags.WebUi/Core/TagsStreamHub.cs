using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StdUnit.Tags.WebUi.Internal;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// 只读观测流的核心实现（对外只暴露 <see cref="ITagsStreamHub"/>）：<br/>
/// - 跟踪 <see cref="ITagsProjectCtrl"/> 的启停，维护每次启动的 <c>epoch</c> 与结构索引；<br/>
/// - 为每个入口用 <see cref="ITagsProject.SetTurnProcessHandler"/> 挂一个"轮次完成"回调，做值 diff；<br/>
/// - 把结果序列化成 SSE 帧，扇出给各订阅者（每帧只序列化一次）。<br/>
/// 同时是订阅者集合的持有者：订阅相关的成员只出现在内部的
/// <see cref="ITagsStreamHub"/> 上，只服务同一程序集里的端点与 <see cref="SseFramesPiper"/>。
/// <para>
/// <b>启动时机</b>：由 <c>MapTagsStream()</c>（<see cref="TagsStreamRegistry.StartAll"/>）启动——
/// 映射端点必然发生在任何请求之前，也早于 <c>StartPollAsync</c>，因此<b>不依赖 Generic Host 的
/// <c>IHostedService</c> 机制</b>；启动后运行期的状态迁移一律以 <see cref="ITagsProjectCtrl.StartedOrStopped"/> 为准
/// （见 <see cref="OnStartedOrStopped"/>）。
/// </para>
/// <para>
/// <b>线程模型</b>：轮次回调在<b>轮询线程</b>上执行，只做 O(改动的点) 的字典写入 + 一次 <c>TryWrite</c>，
/// 不做序列化、不做 IO、不阻塞；序列化与扇出在单独的发送循环里，每个订阅者另有自己的写循环与有界队列。
/// 任何一环满了都只丢帧并触发重同步，不会反压到轮询。
/// </para>
/// </summary>
internal sealed class TagsStreamHub : ITagsStreamHub, IDisposable
{
    private readonly ITagsProjectCtrl _ctrl;
    private readonly TagsStreamOptions _options;
    private readonly ILogger<TagsStreamHub>? _logger;

    /// <summary>订阅者集合与状态发布。<c>_transitionGate → _gate</c> 是唯一允许的加锁顺序。</summary>
    private readonly object _gate = new();

    /// <summary>启动/停止的状态迁移串行化（避免事件与对账线程同时迁移）。</summary>
    private readonly object _transitionGate = new();

    private readonly HashSet<SseFramesPiper> _pipers = new();
    private readonly Channel<PendingBatch> _pending;

    private volatile ProjectIndex? _index;
    private volatile ITagsProject? _project;
    private volatile StateDto _state = new(0, false, null);

    private long _epoch;
    private int _pipersCount;
    private int _overflow;
    private int _started;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private Task? _heartbeatTask;

    /// <summary>
    /// c'tor <br/>
    /// 有意保持 internal；
    /// 不应该让DI容器自动构建本类的实例
    /// </summary>
    /// <param name="ctrl">测点项目控制器（需由 <c>AddTagsProjectServices</c> 注册）</param>
    /// <param name="options">配置</param>
    /// <param name="logger">日志</param>
    internal TagsStreamHub(ITagsProjectCtrl ctrl, TagsStreamOptions options, ILogger<TagsStreamHub>? logger = null)
    {
        this._ctrl = ctrl ?? throw new ArgumentNullException(nameof(ctrl));
        this._options = options ?? throw new ArgumentNullException(nameof(options));
        this._logger = logger;

        if (options.ReconcileInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ReconcileInterval, "ReconcileInterval 必须大于 0");
        }
        if (options.HeartbeatInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.HeartbeatInterval, "HeartbeatInterval 必须大于 0");
        }
        if (options.PiperQueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.PiperQueueCapacity, "SubscriberQueueCapacity 必须大于 0");
        }
        if (options.PendingQueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.PendingQueueCapacity, "PendingQueueCapacity 必须大于 0");
        }

        this._pending = Channel.CreateBounded<PendingBatch>(new BoundedChannelOptions(options.PendingQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // 配合 TryWrite：满时返回 false，而不是静默丢弃
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc/>
    public long Epoch => this._state.Epoch;

    /// <summary>
    /// 幂等启动：订阅 <see cref="ITagsProjectCtrl.StartedOrStopped"/>、拉起发送循环与心跳循环，
    /// 并按 <see cref="ITagsProjectCtrl.Project"/> 对账（补上"启动之前项目就已经在跑"的索引与轮次回调）。<br/>
    /// 由 <c>MapTagsStream()</c> 统一调用（<see cref="TagsStreamRegistry.StartAll"/>）；
    /// 启动之后一直订阅到 <see cref="StopAsync"/>/<see cref="Dispose"/>——项目停止时不能退订，
    /// 否则接不到下一次"已启动"。
    /// <para>
    /// <b>同步</b>：内部只有"订阅事件 + 起两个后台循环 + 一次对账"，没有 IO，全部同步完成；
    /// 所以刻意不返回 <see cref="Task"/>——调用方（同步的 <c>MapTagsStream</c>）不必也不需要阻塞等待，
    /// 从而不给"同步等待异步"留下死锁口子。<see cref="StopAsync"/> 不同，它要等循环真正退出。
    /// </para>
    /// </summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref this._started, 1) != 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        this._cts = cts;
        this._ctrl.StartedOrStopped += this.OnStartedOrStopped;
        this._pumpTask = Task.Run(() => this.PumpAsync(cts.Token), CancellationToken.None);
        this._heartbeatTask = Task.Run(() => this.HeartbeatAsync(cts.Token), CancellationToken.None);

        // 订阅之前项目可能就已经在跑（hub 启动得晚）：先对一次账。
        this.SyncWithController();
    }

    /// <summary>
    /// 停止观测：退订事件、取消并等待发送/心跳循环退出。<br/>
    /// 正常停机由 DI 容器释放单例时的 <see cref="Dispose"/> 兜底（只取消、不等待）。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this._ctrl.StartedOrStopped -= this.OnStartedOrStopped;

        var cts = Interlocked.Exchange(ref this._cts, null);
        if (cts is not null)
        {
            cts.Cancel();
        }

        var tasks = new[] { this._pumpTask, this._heartbeatTask };
        foreach (var task in tasks)
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常停机
            }
        }

        cts?.Dispose();
        this._pumpTask = null;
        this._heartbeatTask = null;
    }

    /// <inheritdoc/>
    public TreeDto GetTree()
    {
        var index = this._index;
        var state = this._state;
        return index is not null && state.IsStarted
            ? index.Tree
            : new TreeDto(state.Epoch, false, string.Empty, Array.Empty<TagNodeDto>());
    }


    /// <inheritdoc/>
    public SseFramesPiper Subscribe(IReadOnlySet<string>? entries)
    {
        var piper = new SseFramesPiper(this, entries, this._options.PiperQueueCapacity);

        lock (this._gate)
        {
            piper.Enqueue(SseFrames.State(this._state));

            var index = this._index;
            if (index is not null && this._state.IsStarted)
            {
                foreach (var stream in index.Entries)
                {
                    if (piper.ContainsEntry(stream.Key))
                    {
                        piper.Enqueue(this.BuildFullFrameLocked(index, stream));
                    }
                }
            }

            this._pipers.Add(piper);
            Interlocked.Increment(ref this._pipersCount);
        }

        return piper;
    }

    public void RemovePiper(SseFramesPiper piper)
    {
        lock (this._gate)
        {
            if (this._pipers.Remove(piper))
            {
                Interlocked.Decrement(ref this._pipersCount);
            }
        }
    }

    public IReadOnlyList<ReadOnlyMemory<byte>> BuildSyncFrames(SseFramesPiper piper)
    {
        lock (this._gate)
        {
            var frames = new List<ReadOnlyMemory<byte>> { 
                SseFrames.State(this._state) 
            };

            var index = this._index;
            if (index is not null && this._state.IsStarted)
            {
                foreach (var stream in index.Entries)
                {
                    if (piper.ContainsEntry(stream.Key))
                    {
                        frames.Add(this.BuildFullFrameLocked(index, stream));
                    }
                }
            }

            return frames;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._ctrl.StartedOrStopped -= this.OnStartedOrStopped;

        var cts = Interlocked.Exchange(ref this._cts, null);
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已停过
        }

        cts?.Dispose();
    }

    private void OnStartedOrStopped(ITagsProjectCtrl sender, TagsProjectEventArgs args)
    {
        try
        {
            if (args.IsStarted && args.Project is not null)
            {
                this.OnProjectStarted(args.Project);
            }
            else
            {
                this.OnProjectStopped();
            }
        }
        catch (Exception ex)
        {
            // ⚠ "已启动"是在 TagsProjectCtrl.StartPollAsync 的 try 里同步派发的：
            // 从这里抛出会被当作启动失败并让 StartPollAsync 抛错（项目随即被释放）。
            // 观测流的问题绝不允许影响项目启动。
            this._logger?.LogError(ex, "观测流处理 StartedOrStopped 时出错");
        }
    }

    private void OnProjectStarted(ITagsProject project)
    {
        lock (this._transitionGate)
        {
            if (ReferenceEquals(project, this._project))
            {
                return;
            }

            var epoch = Interlocked.Increment(ref this._epoch);
            ProjectIndex index;
            try
            {
                index = ProjectIndex.Build(epoch, project);
            }
            catch (Exception ex)
            {
                this._logger?.LogError(ex, "构建观测索引失败（项目根目录={ProjectRoot}）", project.ProjectRoot);
                return;
            }

            lock (this._gate)
            {
                this._project = project;
                this._index = index;
                this._state = new StateDto(epoch, true, project.ProjectRoot);
                this.Broadcast(SseFrames.State(this._state));
                foreach (var subscriber in this._pipers)
                {
                    subscriber.MarkResync();
                }
            }

            // 运行期也能挂（SetTurnProcessHandler 从下一轮生效）：hub 晚于轮询启动时，这里就是在补挂回调。
            foreach (var entry in index.Entries)
            {
                var entryKey = entry.Key;
                try
                {
                    project.SetTurnProcessHandler(entryKey, (_, _, _) =>
                    {
                        this.OnTurn(epoch, entryKey);
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    this._logger?.LogWarning(
                        ex,
                        "无法为入口 {Entry} 注册轮次回调（项目可能已释放）：该入口只能观测结构，观测不到值",
                        entryKey);
                }
            }
        }
    }

    private void OnProjectStopped()
    {
        lock (this._transitionGate)
        {
            lock (this._gate)
            {
                if (this._project is null)
                {
                    return;
                }

                this._project = null;
                this._index = null;
                this._state = new StateDto(this._state.Epoch, false, this._state.ProjectRoot);
                this.Broadcast(SseFrames.State(this._state));
            }
        }
    }

    /// <summary>
    /// 对账：以 <see cref="ITagsProjectCtrl.Project"/> 为准，纠正"未收到事件"的状态差异
    /// （例如 <c>RunAsync</c> 异常退出时项目被释放但不会派发"已停止"）。
    /// </summary>
    private void SyncWithController()
    {
        var project = this._ctrl.Project;
        if (project is null)
        {
            this.OnProjectStopped();
            return;
        }

        if (!ReferenceEquals(project, this._project))
        {
            this.OnProjectStarted(project);
        }
    }

    /// <summary>
    /// 轮次完成回调。<b>运行在轮询线程上</b>：只做 diff + 一次非阻塞入队。
    /// </summary>
    private void OnTurn(long epoch, string entryKey)
    {
        // 没有订阅者时零开销（这是"随订阅者来去动态开关"的正确做法：一次集合判空）。
        if (Volatile.Read(ref this._pipersCount) == 0)
        {
            return;
        }

        var index = this._index;
        if (index is null || index.Epoch != epoch)
        {
            return;
        }

        if (!index.ByKey.TryGetValue(entryKey, out var stream))
        {
            return;
        }

        var delta = stream.Collector.Diff();
        if (delta is null)
        {
            return;
        }

        if (!this._pending.Writer.TryWrite(new PendingBatch(epoch, entryKey, DateTime.UtcNow, delta)))
        {
            // 待处理队列满：丢弃这一批，让所有订阅者重新同步（值不会永久丢失，下一次全量会带回）。
            Interlocked.Exchange(ref this._overflow, 1);
        }
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            while (await this._pending.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (this._pending.Reader.TryRead(out var batch))
                {
                    if (Interlocked.Exchange(ref this._overflow, 0) == 1)
                    {
                        this.MarkAllResync();
                    }

                    try
                    {
                        this.Publish(batch);
                    }
                    catch (Exception ex)
                    {
                        this._logger?.LogError(ex, "发送观测帧失败（入口={Entry}）", batch.EntryKey);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    private async Task HeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(this._options.ReconcileInterval);
        var nextPing = DateTime.UtcNow + this._options.HeartbeatInterval;

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    this.SyncWithController();
                }
                catch (Exception ex)
                {
                    this._logger?.LogError(ex, "观测流对账失败");
                }

                if (DateTime.UtcNow >= nextPing)
                {
                    nextPing = DateTime.UtcNow + this._options.HeartbeatInterval;
                    this.Broadcast(SseFrames.Ping);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    private void Publish(in PendingBatch batch)
    {
        var index = this._index;
        if (index is null || index.Epoch != batch.Epoch)
        {
            return;
        }

        if (!index.ByKey.TryGetValue(batch.EntryKey, out var stream))
        {
            return;
        }

        var values = batch.Delta.Values;
        // 容器标志为空时传 null，让它从 JSON 里省略（空数组会被序列化成 []）
        var containerFlags = batch.Delta.ContainerFlags.Count == 0 ? null : batch.Delta.ContainerFlags;

        if (values.Count == 0)
        {
            // 只有使能状态变化
            this.PublishLocked(batch.Epoch, stream, batch.SampledAtUtc, full: false, Array.Empty<TagValueDto>(), containerFlags);
            return;
        }

        var max = Math.Max(1, this._options.MaxValuesPerFrame);
        if (values.Count <= max)
        {
            this.PublishLocked(batch.Epoch, stream, batch.SampledAtUtc, full: false, values, containerFlags);
            return;
        }

        for (var offset = 0; offset < values.Count; offset += max)
        {
            var count = Math.Min(max, values.Count - offset);
            var chunk = values.GetRange(offset, count);
            // 拆帧时只有第一帧带容器标志。
            this.PublishLocked(batch.Epoch, stream, batch.SampledAtUtc, full: false, chunk, offset == 0 ? containerFlags : null);
        }
    }

    /// <summary>
    /// 分配序号、渲染、扇出。<b>序号与入队必须在同一把锁内</b>，否则订阅/重同步路径可能插在中间导致乱序。
    /// </summary>
    private void PublishLocked(
        long epoch,
        EntryStream stream,
        DateTime sampledAtUtc,
        bool full,
        IReadOnlyList<TagValueDto> values,
        IReadOnlyList<ContainerFlagDto>? containerFlags)
    {
        lock (this._gate)
        {
            if (this._index?.Epoch != epoch)
            {
                return;
            }

            var frame = SseFrames.Values(new EntryValuesDto(
                epoch,
                stream.Key,
                stream.IncrementSeq(),
                sampledAtUtc,
                full,
                values,
                containerFlags));

            foreach (var subscriber in this._pipers)
            {
                if (subscriber.ContainsEntry(stream.Key))
                {
                    subscriber.Enqueue(frame);
                }
            }
        }
    }

    /// <summary>构建某入口的全量基线帧（调用方必须持有 <see cref="_gate"/>）。</summary>
    private ReadOnlyMemory<byte> BuildFullFrameLocked(ProjectIndex index, EntryStream stream)
    {
        var values = stream.Collector.GetAll();
        return SseFrames.Values(new EntryValuesDto(
            index.Epoch,
            stream.Key,
            stream.IncrementSeq(),
            DateTime.UtcNow,
            Full: true,
            Values: values,
            ContainerFlags: null));
    }

    private void Broadcast(ReadOnlyMemory<byte> frame)
    {
        lock (this._gate)
        {
            foreach (var subscriber in this._pipers)
            {
                subscriber.Enqueue(frame);
            }
        }
    }

    private void MarkAllResync()
    {
        lock (this._gate)
        {
            foreach (var subscriber in this._pipers)
            {
                subscriber.MarkResync();
            }
        }
    }

    private readonly record struct PendingBatch(long Epoch, string EntryKey, DateTime SampledAtUtc, EntryDelta Delta);
}

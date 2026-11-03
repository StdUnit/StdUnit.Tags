using StdUnit.Tags.Core.Projects;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Xml.Linq;

namespace StdUnit.Tags;


/// <summary>
/// 默认的实现
/// </summary>
internal class TagsProject : ITagsProject
{
    /// <summary>
    /// 承载项目根的内部测点组节点名（不是入口，仅用于挂载顶层测点组）。
    /// </summary>
    internal const string MainGroupName = "__main__";

    private readonly ITagChannelsLoader _channelsLoader;
    private readonly ITagsLoader _tagsLoader;
    private readonly ILogicetsLoader _logicetLoader;
    private readonly ITagGrpRunnerFactory _tagGrpRunnerFactory;
    private readonly IServiceProvider _sp;
    private readonly IReadOnlyList<ITagsProjectValidator> _validators;
    private readonly ConcurrentDictionary<string, Channel<IntentCompletion>> _entryWriteIntentChannels = new();

    private List<IDisposable> _disposables = new List<IDisposable>();

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagGrpRunnerFactory"></param>
    /// <param name="channelsLoader"></param>
    /// <param name="tagsLoader"></param>
    /// <param name="logicetLoader"></param>
    /// <param name="sp"></param>
    /// <param name="validators">可选的加载期校验器集合（可注册多个，各自独立功能，第三方可追加，
    /// 如 XSD 校验、channel 引用校验）；为空时不校验（见 <see cref="ITagsProjectValidator"/>）</param>
    public TagsProject(ITagGrpRunnerFactory tagGrpRunnerFactory, ITagChannelsLoader channelsLoader, ITagsLoader tagsLoader, ILogicetsLoader logicetLoader, IServiceProvider sp, IEnumerable<ITagsProjectValidator>? validators = null)
    {
        this._channelsLoader = channelsLoader;
        this._tagsLoader = tagsLoader;
        this._logicetLoader = logicetLoader;
        this._tagGrpRunnerFactory = tagGrpRunnerFactory;
        this._sp = sp;
        this._validators = validators?.ToArray() ?? Array.Empty<ITagsProjectValidator>();
    }


    /// <inheritdoc/>
    public string? ProjectRoot { get; private set; } = string.Empty;

    #region RootElement
    private XElement? _rootElement = null;

    /// <inheritdoc/>
    public XElement? GetRootElement() => this._rootElement;
    #endregion

    /// <summary>
    /// 从根元素中加载通道
    /// </summary>
    /// <returns></returns>
    protected virtual TagsProject LoadChannels(XElement root)
    {
        var descriptors = root.GetTagProjectChannelDescriptors();
        var channels = this._channelsLoader.LoadChannels(descriptors);
        this.AddChannels(channels);
        return this;
    }

    /// <summary>
    /// 加载测点
    /// </summary>
    /// <returns></returns>
    protected virtual TagsProject LoadTags(XElement root)
    {
        var main = new TagGrp(new TagGrpDescriptor { Name = MainGroupName, IsEntry = false }, null);
        var descriptors = root.GetTagProjectGrpDescriptors();
        foreach (var descriptor in descriptors)
        {
            this._tagsLoader.LoadTagGroup(main, descriptor, this.Channels);
        }
        this.Tags = main;
        return this;
    }


    /// <summary>
    /// 加载 Logicets
    /// </summary>
    /// <returns></returns>
    protected virtual TagsProject LoadLogicets(XElement root)
    {
        var elements = root.Elements("Logicet");
        var dlls = elements
            .Where(e => !string.IsNullOrEmpty(e.Value))
            .Select(e => string.IsNullOrEmpty(this.ProjectRoot) ? e.Value : Path.Combine(this.ProjectRoot, e.Value));
        var logicets = this._logicetLoader.LoadLogicets(this._sp, dlls, this);
        this.AddLogicets(logicets.Logicets);
        this._disposables.AddRange(logicets.Disposables);
        return this;
    }



    /// <inheritdoc/>
    public void Initialize(string projRoot, XElement? root = null)
    {
        this.ProjectRoot = projRoot;
        this._channels.Clear();
        this.Tags = null!;
        this._logicets.Clear();

        if (root is null)
        {
            var rootxmlPath = Path.Combine(projRoot, "index.xml");
            if (!File.Exists(rootxmlPath))
            {
                throw new FileNotFoundException(rootxmlPath);
            }
            root = XElement.Load(rootxmlPath);
        }

        this._rootElement = root;
        this.CompleteIntentChannels("项目正在初始化，未处理的意图已被丢弃");

        // 可选的加载期项目校验（可注册多个实现，按注册顺序逐个执行）
        foreach (var validator in this._validators)
        {
            validator.Validate(root);
        }

        this.LoadChannels(root);
        this.LoadTags(root);
        this.LoadLogicets(root);

        _ = this.GetEntries();
    }


    ////////////////////////////////////////////////////////////

    #region Channels
    private List<ITagChannel> _channels = new List<ITagChannel>();
    /// <summary>
    /// 通道
    /// </summary>
    public virtual IReadOnlyList<ITagChannel> Channels => _channels;

    /// <summary>
    /// 增加通道
    /// </summary>
    /// <param name="channels"></param>
    /// <returns></returns>
    protected virtual TagsProject AddChannels(IList<ITagChannel> channels)
    {
        this._channels.AddRange(channels);
        return this;
    }
    #endregion

    #region Tags
    /// <summary>
    /// 测点
    /// </summary>
    public virtual ITagGrp Tags { get; private set; } = null!;
    #endregion

    #region logicets
    private List<ILogicet> _logicets = new List<ILogicet>();

    /// <summary>
    /// 逻辑小组件
    /// </summary>
    public IList<ILogicet> Logicets => this._logicets;

    /// <summary>
    /// 批量增加逻辑组件
    /// </summary>
    /// <param name="logicets"></param>
    /// <returns></returns>
    protected TagsProject AddLogicets(IReadOnlyList<ILogicet> logicets)
    {
        this._logicets.AddRange(logicets);
        return this;
    }
    #endregion


    #region Entries;
    private IList<ITagGrp>? _entries = null;
    public IList<ITagGrp> GetEntries()
    {
        if (this._entries is not null)
        {
            return this._entries;
        }
        this._entries = this.Tags.ScanEntries();
        return this._entries;
    }
    #endregion


    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">项目未初始化（通道/测点/逻辑组件为空，或没有任何入口测点组）</exception>
    /// <exception cref="TagsProjectConfigurationException">
    /// 启动前为入口匹配逻辑组件（<see cref="ILogicet.MatchEntry"/> / <see cref="ILogicet.Order"/>）时抛出了异常。
    /// 这是<b>确定性</b>的配置错误（重试不会变好），因此不走 <see cref="RunnerCrashed"/> 的重试/重连恢复路径；
    /// 抛出时<b>没有任何入口</b>开始轮询（启动分两阶段：先算完匹配、再准备并启动 runner）。
    /// </exception>
    /// <remarks>
    /// 启动失败（含 <see cref="ITagGrpRunner.StartAsync"/> 抛出的异常）会先<b>取消并等待已启动的入口退出</b>，
    /// 再把原异常抛出——不会留下无人等待的孤儿轮询。
    /// </remarks>
    public virtual Task RunAsync(CancellationToken ct)
    {
        if (this.Channels == null || this.Channels.Count == 0)
        {
            throw new InvalidOperationException($"通道集为空");
        }
        if (this.Tags == null)
        {
            throw new InvalidOperationException("测点集为空");
        }
        if (this.Logicets == null)
        {
            throw new InvalidOperationException("逻辑组件集为空");
        }

        var entries = this.GetEntries();
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("未配置入口测点组");
        }

        // 阶段 1：纯计算（匹配 + 排序）。失败时"什么都还没发生"——没有 runner、没有轮询。
        var plans = new List<(ITagGrp Entry, List<ILogicet> Logicets)>(entries.Count);
        foreach (var entry in entries)
        {
            plans.Add((entry, this.SelectLogicets(entry)));
        }

        // 阶段 2a：准备（建意图通道 / 建 runner / 订阅事件）。仍可能有同步抛出（IntentCapacity 非法、第三方工厂），
        // 但都发生在任何轮询启动之前，所以失败时项目"一个轮次都没开始"。
        var prepared = this.PrepareEntries(plans);

        // 阶段 2b：启动 + 失败回滚（见 StartPreparedEntriesAsync）。
        return this.StartPreparedEntriesAsync(prepared, ct);
    }

    /// <summary>
    /// 阶段 1：为入口算出要执行哪些逻辑组件（<see cref="ILogicet.MatchEntry"/> 匹配 + 按 <see cref="ILogicet.Order"/> 排序）。<br/>
    /// <b>纯计算</b>：不建通道、不建 runner、不改任何状态，所以可以对全部入口先算完再启动。<br/>
    /// 逻辑组件是扩展点（用户代码）：匹配/排序属于<b>配置判定</b>而不是轮询工作，抛异常意味着"这条配置无法判定"，
    /// 重试不会变好，因此报成配置错误，而不是走 <see cref="RunnerCrashed"/> 的恢复路径。
    /// </summary>
    private List<ILogicet> SelectLogicets(ITagGrp entry)
    {
        var matched = new List<(ILogicet Logicet, int Order)>();
        foreach (var logicet in this.Logicets)
        {
            if (this.InvokeLogicet(entry, logicet, nameof(ILogicet.MatchEntry), () => logicet.MatchEntry(entry)))
            {
                matched.Add((logicet, this.InvokeLogicet(entry, logicet, nameof(ILogicet.Order), () => logicet.Order)));
            }
        }

        // 排序只操作已经取到的 Order，不再调用用户代码。
        return matched
            .OrderBy(x => x.Order)
            .Select(x => x.Logicet)
            .ToList();
    }

    /// <summary>
    /// 调用逻辑组件的启动期成员，把抛出的异常转成带定位上下文的 <see cref="TagsProjectConfigurationException"/>。
    /// </summary>
    private T InvokeLogicet<T>(ITagGrp entry, ILogicet logicet, string operation, Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var location = entry.GetLocationPath();
            throw new TagsProjectConfigurationException(
                $"逻辑组件 {logicet.GetType().FullName} 在入口 {location} 上执行 {operation} 发生异常（{ex.GetType().Name}: {ex.Message}）。" +
                $"{operation} 属于启动期的配置判定：必须无副作用、不阻塞、不得抛异常",
                location,
                ex);
        }
    }

    /// <summary>
    /// 阶段 2a：准备全部入口（建意图通道、建 runner、订阅事件），<b>但不启动轮询</b>。<br/>
    /// 这一步仍可能同步抛出（<see cref="IntentCapacity"/> 非法、第三方 runner 工厂失败），
    /// 但它发生在任何 <see cref="ITagGrpRunner.StartAsync"/> 之前，所以失败时一个轮次都没开始。
    /// </summary>
    private List<(ITagGrp Entry, ITagGrpRunner Runner)> PrepareEntries(List<(ITagGrp Entry, List<ILogicet> Logicets)> plans)
    {
        var prepared = new List<(ITagGrp Entry, ITagGrpRunner Runner)>(plans.Count);
        foreach (var (entry, logicets) in plans)
        {
            _ = this._entryWriteIntentChannels.GetOrAdd(entry.TagName(), _ => this.CreateIntentChannel());

            var runner = this._tagGrpRunnerFactory.Create(this);
            // runner 是单槽回调：把"多播 + 调用时取快照"收口在本类（项目层才是多个独立观察者的地方）。
            runner.RunnerStarted = (grp, ch, startedCt) => this.InvokeRunnerStartedAsync(grp, ch, startedCt);
            runner.TurnProcess = async (grp, ch, turnCt) =>
            {
                foreach (var l in logicets)
                {
                    if (!l.Enabled)
                    {
                        continue;
                    }
                    await l.ProcessAsync(grp, ch, turnCt);
                }

                // 公共订阅排在 Logicets 之后：订阅者看到的是逻辑处理完成后的值。
                // 有意不 catch：回调抛异常等价于本轮失败（与 Logicet 抛异常同义），并且按注册顺序
                // "首个异常即中断后续订阅者"（与 RunnerStarted / RunnerCrashed 的语义一致）。
                // 订阅表是写时复制的：这里读到的那份快照本轮内不会被改（运行期变更从下一轮生效）。
                if (this._entryTurnHandlers.TryGetValue(grp.TagName(), out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        await handler(grp, ch, turnCt);
                    }
                }
            };
            runner.RunnerCrashed = (grp, ch, ex, crashedCt) => this.InvokeRunnerCrashedAsync(grp, ch, ex, crashedCt);
            prepared.Add((entry, runner));
        }

        return prepared;
    }

    /// <summary>
    /// 阶段 2b：启动全部入口，并在任一入口启动失败时<b>回滚</b>——取消并等待此前已启动的入口退出。<br/>
    /// 回滚靠内部的 linked 令牌实现：同一次运行里各回调拿到的仍然是<b>同一个</b>令牌实例，
    /// 但不保证 <c>==</c> 传给 <see cref="RunAsync"/> 的那个（否则无法回滚已经启动的入口）。
    /// </summary>
    private async Task StartPreparedEntriesAsync(List<(ITagGrp Entry, ITagGrpRunner Runner)> prepared, CancellationToken ct)
    {
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new List<Task>(prepared.Count);
        try
        {
            foreach (var (entry, runner) in prepared)
            {
                tasks.Add(runner.StartAsync(entry, abort.Token));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // 回滚：同步抛出的第三方实现、外部停机、运行器任务故障，都在这里收口。
            // 清理路径的既有约定：回滚期间的异常不覆盖原始异常。
            abort.Cancel();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
            }
            throw;
        }
    }

    #region Intent Mgmt

    /// <inheritdoc/>
    public int IntentCapacity { get; set; } = 5;

    /// <inheritdoc/>
    public bool WriteIntent(string entry, TagGrpWriteIntent intent)
    {
        return this.WriteIntent(entry, intent, out _);
    }

    /// <inheritdoc/>
    public bool WriteIntent(string entry, TagGrpWriteIntent intent, out Task task)
    {
        // 校验 entry 是否真的存在，只允许向合法的入口写入意图
        var entries = this.GetEntries();
        if (!entries.Any(e => e.TagName() == entry))
        {
            throw new KeyNotFoundException($"未找到指定的入口测点组: {entry}");
        }
        var intentChannel = this._entryWriteIntentChannels.GetOrAdd(entry, _ => this.CreateIntentChannel());
        var writer = intentChannel.Writer;
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new IntentCompletion(intent, tcs);
        task = tcs.Task;
        var written = writer.TryWrite(item);
        if (!written)
        {
            tcs.TrySetException(new IntentWrittenException(entry, "写入意图失败"));
        }
        return written;
    }


    protected virtual Channel<IntentCompletion> CreateIntentChannel()
    {
        if (this.IntentCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.IntentCapacity), this.IntentCapacity, "IntentCapacity 必须大于 0");
        }

        return Channel.CreateBounded<IntentCompletion>(new BoundedChannelOptions(capacity: this.IntentCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    protected virtual void CompleteIntentChannels(string disposeMsg)
    {
        foreach (var kvp in this._entryWriteIntentChannels)
        {
            kvp.Value.Writer.TryComplete();
        }

        foreach (var kvp in this._entryWriteIntentChannels)
        {
            var reader = kvp.Value.Reader;
            while (reader.TryRead(out var item))
            {
                item.Completion.TrySetException(new IntentWrittenException(kvp.Key, disposeMsg));
            }
        }

        this._entryWriteIntentChannels.Clear();
    }

    /// <inheritdoc/>
    public ChannelReader<IntentCompletion>? GetIntentReader(string entry)
    {
        if (!this._entryWriteIntentChannels.TryGetValue(entry, out var intentChannel))
        {
            return null;
        }
        return intentChannel.Reader;
    }
    #endregion

    #region Runner events（多播）

    /// <summary>
    /// 订阅者列表（低频事件：每次建连/崩溃才触发），写侧用
    /// <see cref="_runnerEventsGate"/> 保护"读-改-写"，读侧用
    /// <see cref="Volatile"/> 读取一次快照。<br/>
    /// 这里刻意不做轮询热路径式的无锁设计（那是 <see cref="TurnProcess"/> 的诉求）：
    /// 转发给 runner 的委托<b>每次调用时才读快照</b>，所以运行期订阅/退订从下一次通知开始生效。
    /// </summary>
    private RunnerStarted? _runnerStartedHandlers;

    /// <summary>见 <see cref="_runnerStartedHandlers"/>。</summary>
    private RunnerCrashed? _runnerCrashedHandlers;

    /// <summary>保护上面两个委托字段的合并/移除。</summary>
    private readonly object _runnerEventsGate = new();

    /// <inheritdoc/>
    public event RunnerStarted? RunnerStarted
    {
        add
        {
            if (value is null) return;
            lock (this._runnerEventsGate)
            {
                this._runnerStartedHandlers += value;
            }
        }
        remove
        {
            if (value is null) return;
            lock (this._runnerEventsGate)
            {
                this._runnerStartedHandlers -= value;
            }
        }
    }

    /// <inheritdoc/>
    public event RunnerCrashed? RunnerCrashed
    {
        add
        {
            if (value is null) return;
            lock (this._runnerEventsGate)
            {
                this._runnerCrashedHandlers += value;
            }
        }
        remove
        {
            if (value is null) return;
            lock (this._runnerEventsGate)
            {
                this._runnerCrashedHandlers -= value;
            }
        }
    }

    /// <summary>
    /// 按注册顺序<b>串行</b>调用订阅者；<b>遇到第一个异常立即中断</b>并原样抛出（保留类型与堆栈）。<br/>
    /// 刻意不做"收集全部异常再抛"：收集了却只抛一个等于把其余静默丢弃，而 Core 没有 logger 可以留痕；
    /// 也不包装成 <see cref="AggregateException"/>——"谁抛谁负责、后续不再执行"更符合直觉，异常类型也可预测。
    /// </summary>
    private async Task InvokeRunnerStartedAsync(ITagGrp grp, ITagChannel? channel, CancellationToken ct)
    {
        var handlers = Volatile.Read(ref this._runnerStartedHandlers);
        if (handlers is null)
        {
            return;
        }

        foreach (var d in handlers.GetInvocationList())
        {
            await ((RunnerStarted)d)(grp, channel, ct);
        }
    }

    /// <summary>见 <see cref="InvokeRunnerStartedAsync"/>。</summary>
    private async Task InvokeRunnerCrashedAsync(ITagGrp grp, ITagChannel? channel, Exception ex, CancellationToken ct)
    {
        var handlers = Volatile.Read(ref this._runnerCrashedHandlers);
        if (handlers is null) return;

        foreach (var d in handlers.GetInvocationList())
        {
            await ((RunnerCrashed)d)(grp, channel, ex, ct);
        }
    }
    #endregion

    #region TurnProcess subscriptions
    /// <summary>
    /// 入口名 → 该入口的订阅者数组（每个入口 0..N 个）。<b>写时复制</b>：订阅/退订时在
    /// <see cref="_entryTurnHandlersGate"/> 下复制一份新表（和新的数组）再整体替换，已发布的实例永不再改，
    /// 所以轮询线程可以直接读、不需要任何同步，运行期订阅/退订自然就是"从下一轮开始生效"。
    /// </summary>
    private volatile Dictionary<string, TurnProcess[]> _entryTurnHandlers = new(StringComparer.Ordinal);

    /// <summary>保护 <see cref="_entryTurnHandlers"/> 的复制作业（只在订阅/退订时短暂持有）。</summary>
    private readonly object _entryTurnHandlersGate = new();

    /// <inheritdoc/>
    public IDisposable SubscribeTurnProcess(string entry, TurnProcess handler)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }
        if (handler is null)
        {
            throw new ArgumentNullException(
                nameof(handler),
                "轮次回调不能为 null；要退订请 Dispose 订阅时返回的句柄。");
        }
        if (this._disposed)
        {
            throw new ObjectDisposedException(
                this.GetType().Name,
                $"项目已释放，不能再订阅 TurnProcess 回调（入口={entry}）：订阅的作用域就是项目实例。");
        }

        // 与 WriteIntent 的入口校验保持一致：只允许向合法的入口订阅
        if (!this.GetEntries().Any(e => e.TagName() == entry))
        {
            throw new KeyNotFoundException($"未找到指定的入口测点组: {entry}");
        }

        var subscription = new TurnProcessSubscription(this, entry, handler);
        lock (this._entryTurnHandlersGate)
        {
            var current = this._entryTurnHandlers;
            var updated = new Dictionary<string, TurnProcess[]>(current, StringComparer.Ordinal);
            if (current.TryGetValue(entry, out var handlers) && handlers.Length > 0)
            {
                var appended = new TurnProcess[handlers.Length + 1];
                handlers.CopyTo(appended, 0);
                appended[appended.Length - 1] = handler;
                updated[entry] = appended;
            }
            else
            {
                updated[entry] = new[] { handler };
            }

            this._entryTurnHandlers = updated;
        }

        return subscription;
    }

    /// <summary>
    /// 退订（由 <see cref="TurnProcessSubscription.Dispose"/> 调用）。只摘掉<b>那一个</b>订阅实例：
    /// 同一个委托实例订阅多次时，每次 Dispose 摘掉其中一次。
    /// </summary>
    private void UnsubscribeTurnProcess(TurnProcessSubscription subscription)
    {
        lock (this._entryTurnHandlersGate)
        {
            var current = this._entryTurnHandlers;
            if (!current.TryGetValue(subscription.Entry, out var handlers))
            {
                return;
            }

            var index = Array.FindIndex(handlers, h => ReferenceEquals(h, subscription.Handler));
            if (index < 0)
            {
                return;
            }

            var updated = new Dictionary<string, TurnProcess[]>(current, StringComparer.Ordinal);
            if (handlers.Length == 1)
            {
                updated.Remove(subscription.Entry);
            }
            else
            {
                var removed = new TurnProcess[handlers.Length - 1];
                Array.Copy(handlers, 0, removed, 0, index);
                Array.Copy(handlers, index + 1, removed, index, handlers.Length - index - 1);
                updated[subscription.Entry] = removed;
            }

            this._entryTurnHandlers = updated;
        }
    }

    /// <summary>
    /// 订阅句柄。<b>幂等</b>：Dispose 只生效一次；项目已释放之后再 Dispose 是无操作（不抛）。
    /// </summary>
    private sealed class TurnProcessSubscription : IDisposable
    {
        private TagsProject? _owner;

        internal TurnProcessSubscription(TagsProject owner, string entry, TurnProcess handler)
        {
            this._owner = owner;
            this.Entry = entry;
            this.Handler = handler;
        }

        internal string Entry { get; }

        internal TurnProcess Handler { get; }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref this._owner, null);
            owner?.UnsubscribeTurnProcess(this);
        }
    }
    #endregion


    #region
    private volatile bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                this.CompleteIntentChannels("项目正在释放，未处理的意图已被丢弃");
                foreach (var d in this._disposables)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch {  /*  */  }
                }
            }

            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

/// <summary>
/// 意图写入异常
/// </summary>
public sealed class IntentWrittenException : Exception
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="message"></param>
    public IntentWrittenException(string entry, string message)
        : base(message)
    {
        this.Entry = entry;
    }

    /// <summary>
    /// 入口
    /// </summary>
    public string Entry { get; }
}
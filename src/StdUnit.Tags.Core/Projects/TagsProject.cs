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

        // 进入运行期：此后不再接受 TurnProcess 回调注册（见 AddTurnProcessHandler）。
        this._running = true;

        var tasks = new ConcurrentBag<Task>();
        Parallel.ForEach(entries, entry =>
        {
            var writeIntentChannel = this._entryWriteIntentChannels.GetOrAdd(entry.TagName(), _ => this.CreateIntentChannel());
            var logicets = this.Logicets
                .Where(l => l.MatchEntry(entry))
                .OrderBy(l => l.Order)
                .ToList();
            var runner = this._tagGrpRunnerFactory.Create(this);
            runner.RunnerStarted += RunnerStarted;
            runner.TurnProcess += async (entry, ch, turnCt) =>
            {
                foreach (var l in logicets)
                {
                    if (!l.Enabled)
                    {
                        continue;
                    }
                    await l.ProcessAsync(entry, ch, turnCt);
                }

                // 公共回调排在 Logicets 之后：订阅者看到的是逻辑处理完成后的值。
                // 有意不 catch：回调抛异常等价于本轮失败（与 Logicet 抛异常同义），见 ITagsProject.SetTurnProcessHandler 的文档。
                // 回调表在运行期只读（设置在运行前完成），因此这里不需要任何同步。
                if (this._entryTurnHandlers.TryGetValue(entry.TagName(), out var handler))
                {
                    await handler(entry, ch, turnCt);
                }
            };
            runner.RunnerCrashed += RunnerCrashed;
            Task task = runner.StartAsync(entry, ct);
            tasks.Add(task);
        });

        return Task.WhenAll(tasks);
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

    /// <inheritdoc/>
    public event RunnerCrashed? RunnerCrashed;
    /// <inheritdoc/>
    public event RunnerStarted? RunnerStarted;

    #region TurnProcess handlers
    /// <summary>
    /// 入口名 → 该入口的回调（每个入口最多一个）。<b>只在运行前写入、运行期只读</b>
    /// （见 <see cref="SetTurnProcessHandler"/> 的"只能在运行前设置"约定），因此轮询线程读取时不需要任何同步。
    /// </summary>
    private readonly Dictionary<string, TurnProcess> _entryTurnHandlers = new(StringComparer.Ordinal);

    /// <summary>
    /// 是否已进入运行期（<see cref="RunAsync"/> 已开始）。运行期不再接受设置。
    /// </summary>
    private volatile bool _running;

    /// <inheritdoc/>
    public void SetTurnProcessHandler(string entry, TurnProcess? handler)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }
        if (this._running)
        {
            throw new InvalidOperationException(
                $"TurnProcess 回调只能在项目运行之前设置（入口={entry}）：请在启动 hook 或 StartedOrStopped 的\"已启动\"事件里设置，" +
                "运行期如需动态开关观测，请在已设置的回调内部判断。");
        }

        // 与 WriteIntent 的入口校验保持一致：只允许向合法的入口设置
        if (!this.GetEntries().Any(e => e.TagName() == entry))
        {
            throw new KeyNotFoundException($"未找到指定的入口测点组: {entry}");
        }

        // 替换语义：需要组合多个观察者时，由调用方自己组装成一个 TurnProcess。
        if (handler is null)
        {
            this._entryTurnHandlers.Remove(entry);
        }
        else
        {
            this._entryTurnHandlers[entry] = handler;
        }
    }
    #endregion


    #region
    private bool _disposed;

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
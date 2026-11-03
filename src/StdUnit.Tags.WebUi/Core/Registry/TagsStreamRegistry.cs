using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// 所有观测流的注册表：key（不具名用 <c>null</c>）→ 一路独立的 hub + 控制器 + 配置。<br/>
/// 每个 key 恰好创建一个 hub，端点与 hub 启动点共用同一实例。<br/>
/// <br/>
/// <b>生命周期</b>：本类持有 hub，所以也负责它们的收尾——它自己注册进 DI，容器释放时一并
/// <see cref="Dispose"/>；同时在构造时把自己挂到宿主的 <c>ApplicationStopping</c> 上：
/// 该回调早于"停 HostedService""停服务器"执行，因此 SSE 请求能在服务器开始等待关机超时之前就结束。
/// 这两条路径都是幂等的（<see cref="TagsStreamHub.Stop"/> 幂等），谁先到都不影响。
/// </summary>
internal sealed class TagsStreamRegistry : IDisposable
{
    private readonly List<TagsStreamEntry> _entries = new();
    private readonly Dictionary<string, TagsStreamEntry> _keyed = new(StringComparer.Ordinal);
    private readonly CancellationTokenRegistration _stoppingRegistration;
    private TagsStreamEntry? _default;
    private int _disposed;

    public TagsStreamRegistry(IServiceProvider services, IEnumerable<TagsStreamDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            var ctrl = ResolveCtrl(services, descriptor.Key);
            var hub = new TagsStreamHub(ctrl, descriptor.Options, services.GetService<ILogger<TagsStreamHub>>());
            var entry = new TagsStreamEntry(descriptor.Key, descriptor.Options, ctrl, hub);

            this._entries.Add(entry);
            if (descriptor.Key is null)
            {
                this._default = entry;
            }
            else
            {
                this._keyed.Add(descriptor.Key, entry);
            }
        }

        // 不把 IHostApplicationLifetime 做成构造参数：单元测试里直接从裸 ServiceCollection 解析本类时没有宿主。
        if (services.GetService<IHostApplicationLifetime>() is { } lifetime)
        {
            this._stoppingRegistration = lifetime.ApplicationStopping.Register(
                static state => ((TagsStreamRegistry)state!).StopAll(),
                this);
        }
    }

    /// <summary>按 key 取一路流；<c>null</c> 表示不具名的那一路。未注册返回 <c>null</c>。</summary>
    public TagsStreamEntry? Get(string? key) => key is null
        ? this._default
        : this._keyed.TryGetValue(key, out var entry) ? entry : null;

    /// <summary>按 key 取一路流；未注册则抛出（配置错误，不是请求错误）。</summary>
    public TagsStreamEntry GetRequired(string? key) => this.Get(key) ?? throw new InvalidOperationException(
        key is null
            ? $"未注册不具名观测流：请调用 AddTagsStream()。"
            : $"未注册 key = \"{key}\" 的观测流：请调用 AddKeyedTagsStream(\"{key}\")。");

    public IReadOnlyList<TagsStreamEntry> All => this._entries;

    /// <summary>
    /// 启动全部流的 hub（幂等）。<b>这是观测流的唯一启动点</b>，由 <c>MapTagsStream()</c> 在映射端点时调用：
    /// 映射必然早于任何请求，也早于 <c>StartPollAsync</c>，所以不需要 Generic Host 的 <c>IHostedService</c>。
    /// </summary>
    public void StartAll()
    {
        foreach (var entry in this._entries)
        {
            entry.Hub.Start();
        }
    }

    /// <summary>
    /// 停止全部流（幂等）：退订项目事件、取消后台循环、让在途 SSE 订阅者读到"流结束"。
    /// 由宿主的 <c>ApplicationStopping</c> 与 <see cref="Dispose"/> 各调一次。
    /// </summary>
    public void StopAll()
    {
        foreach (var entry in this._entries)
        {
            entry.Hub.Stop();
        }
    }

    /// <summary>容器释放时的兜底：正常停机时 <c>ApplicationStopping</c> 已经先停过一遍，这里是幂等的空转。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        // 先摘回调：容器释放之后宿主的停机令牌随时可能取消，那时再回来调 StopAll 已无意义。
        this._stoppingRegistration.Dispose();
        this.StopAll();
    }

    private static ITagsProjectCtrl ResolveCtrl(IServiceProvider services, string? key)
    {
        if (key is null)
        {
            return services.GetRequiredService<ITagsProjectCtrl>();
        }

        return services.GetKeyedService<ITagsProjectCtrl>(key)
            ?? throw new InvalidOperationException(
                $"未找到 key = \"{key}\" 的 {nameof(ITagsProjectCtrl)}："
                + $"请先用 AddKeyedTagsProjectCtrl(\"{key}\") 注册它，"
                + "或用 AddTagsProjectServices 注册不具名的那一个（并给 AddTagsStream 传 null）。");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// 所有观测流的注册表：key（不具名用 <c>null</c>）→ 一路独立的 hub + 控制器 + 配置。<br/>
/// 每个 key 恰好创建一个 hub，端点与 hub 启动点共用同一实例。
/// </summary>
internal sealed class TagsStreamRegistry
{
    private readonly List<TagsStreamEntry> _entries = new();
    private readonly Dictionary<string, TagsStreamEntry> _keyed = new(StringComparer.Ordinal);
    private TagsStreamEntry? _default;

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

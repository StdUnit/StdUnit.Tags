using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags.WebUi;

/// <summary>
/// 所有观测流的注册表：key（不具名用 <c>null</c>）→ 一路独立的 hub + 控制器 + 配置。<br/>
/// 每个 key 恰好创建一个 hub，hosted service 与端点共用同一实例。
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
            var hub = new TagsStreamHostedService(ctrl, descriptor.Options, services.GetService<ILogger<TagsStreamHostedService>>());
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

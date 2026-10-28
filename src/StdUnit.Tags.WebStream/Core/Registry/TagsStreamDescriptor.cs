namespace StdUnit.Tags.WebStream;

/// <summary>
/// 一次 <c>AddTagsStream</c> 的登记项。<br/>
/// 作为 DI 里的 <c>ImplementationInstance</c> 存在，所以注册期就能做 key 的格式/保留字/重复校验，
/// 解析期再由 <see cref="TagsStreamRegistry"/> 逐个创建 hub。
/// </summary>
internal sealed class TagsStreamDescriptor
{
    public TagsStreamDescriptor(string? key, TagsStreamOptions options)
    {
        this.Key = key;
        this.Options = options;
    }

    /// <summary>观测流的路由键；<c>null</c> 表示不具名（端点直接挂在前缀下）。</summary>
    public string? Key { get; }

    /// <summary>这一路流自己的配置（每次 <c>AddTagsStream</c> 一份，互不影响）。</summary>
    public TagsStreamOptions Options { get; }
}

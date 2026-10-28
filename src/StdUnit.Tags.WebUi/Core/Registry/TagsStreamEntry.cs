namespace StdUnit.Tags.WebUi;

/// <summary>
/// 一路观测流：它观测的控制器、配置与 hub。<br/>
/// 端点、命令、hosted service 都通过它拿到"同一个"hub —— 这是"接口服务 = hosted service = 端点看到的实例"的保证。
/// </summary>
internal sealed class TagsStreamEntry
{
    public TagsStreamEntry(string? key, TagsStreamOptions options, ITagsProjectCtrl ctrl, TagsStreamHostedService hub)
    {
        this.Key = key;
        this.Options = options;
        this.Ctrl = ctrl;
        this.HostedService = hub;
    }

    public string? Key { get; }

    public TagsStreamOptions Options { get; }

    /// <summary>这一路流观测的控制器（写命令也用它，保证"看哪个项目就改哪个项目"）。</summary>
    public ITagsProjectCtrl Ctrl { get; }

    internal TagsStreamHostedService HostedService { get; }
}

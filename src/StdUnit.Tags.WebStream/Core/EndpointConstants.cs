namespace StdUnit.Tags.WebStream;

/// <summary>
/// 端点常量。<br/>
/// <code>
/// /{prefix}
///     /ui       # 内置监控页（GET）
///     /tree     # 结构树（GET）
///     /stream   # 值流（GET，SSE）
///     /value    # 改测点值（POST）
///     /rescan   # 标记未扫描（POST）
/// </code>
/// </summary>
public static class EndpointConstants
{
    /// <summary>
    /// 默认前缀：所有端点都挂在这个前缀下（<c>/stdunit.tags/tree</c>、<c>/stdunit.tags/stream</c> …）。
    /// </summary>
    public const string DefaultPrefix = "/stdunit.tags";

    /// <summary>内置监控页（GET）。</summary>
    internal const string UiPath = "/ui";

    /// <summary>结构树（GET）。</summary>
    internal const string TreePath = "/tree";

    /// <summary>值流（GET，SSE）。</summary>
    internal const string StreamPath = "/stream";

    /// <summary>改测点值（POST）。</summary>
    internal const string WriteValuePath = "/value";

    /// <summary>标记未扫描（POST）。</summary>
    internal const string RescanPath = "/rescan";
}

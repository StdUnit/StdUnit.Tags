namespace StdUnit.Tags.WebUi;

/// <summary>
/// 端点常量。<br/>
/// <code>
/// /{prefix}                      # 不具名流（AddTagsStream()）
///     /ui       # 内置监控页（GET）
///     /designer # 内置设计器页（GET，通道 + 测点，导出项目 XML）
///     /tree     # 结构树（GET）
///     /stream   # 值流（GET，SSE）
///     /value    # 改测点值（POST）
///     /rescan   # 标记未扫描（POST）
///     /catalog  # 设计器元数据（GET，与具体流无关，只有这一份）
/// /{prefix}/{keyed}/…           # 每一路具名流（AddKeyedTagsStream(key)），路径与上面同形
/// </code>
/// </summary>
public static class EndpointConstants
{
    /// <summary>
    /// 默认前缀：所有端点都挂在这个前缀下（<c>/stdunit.tags/tree</c>、<c>/stdunit.tags/stream</c> …）。
    /// </summary>
    public const string DefaultPrefix = "/stdunit.tags";

    /// <summary>路由值名：具名流的 key（<c>/{prefix}/{keyed}/tree</c>）。</summary>
    internal const string KeyedRouteValue = "keyed";

    /// <summary>内置监控页（GET）。</summary>
    internal const string UiPath = "/ui";

    /// <summary>内置设计器页（GET）：通道 + 测点，导出项目 XML。</summary>
    internal const string DesignerPath = "/designer";

    /// <summary>结构树（GET）。</summary>
    internal const string TreePath = "/tree";

    /// <summary>值流（GET，SSE）。</summary>
    internal const string StreamPath = "/stream";

    /// <summary>改测点值（POST）。</summary>
    internal const string WriteValuePath = "/value";

    /// <summary>标记未扫描（POST）。</summary>
    internal const string RescanPath = "/rescan";

    /// <summary>
    /// 设计器元数据（GET）：核心 + 驱动 schema 内省成的 JSON（通道/测点设计器用）。
    /// 与具体观测流无关，因此<b>没有</b>具名版本。
    /// </summary>
    internal const string CatalogPath = "/catalog";

    /// <summary>具名流的同一套路径（<c>/{keyed}/…</c>）。</summary>
    internal static string Keyed(string path) => "/{" + KeyedRouteValue + "}" + path;
}

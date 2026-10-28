namespace WebMonitor.Tags;

/// <summary>
/// 入口名 / 观测流 key / 测点路径常量。与两个 <c>index.xml</c> 里的名字逐字一致。
/// </summary>
internal static class TagsPathDefines
{
    /// <summary>不具名流对应的项目入口名（Tags/main/index.xml）。</summary>
    public static readonly string MainEntry = "main";

    /// <summary>具名流的 key：同时是路由段 <c>/stdunit.tags/line-b/ui</c> 与控制器注册键。</summary>
    public const string LineBKey = "line-b";

    /// <summary>具名流对应的项目入口名（Tags/line-b/index.xml）。</summary>
    public static readonly string LineBEntry = "line-b";

    public static readonly string Counter = "counter";

    public static readonly string Level = "level";
}

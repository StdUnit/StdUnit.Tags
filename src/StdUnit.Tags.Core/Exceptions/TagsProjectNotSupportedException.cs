namespace StdUnit.Tags;

/// <summary>
/// 项目配置<b>本身合法</b>（XML 语法、配置语义、校验器都能通过），但<b>当前宿主运行时</b>不提供该配置所需的能力时抛出。<br/>
/// <br/>
/// 示例场景：
/// <list type="bullet">
/// <item>以 <b>NativeAOT</b> 发布的宿主遇到 <c>&lt;Logicet/&gt;</c> 动态插件（运行时没有动态加载能力）；</item>
/// <item>以 <b>NativeAOT / 裁剪</b>发布的宿主使用 SimpleFiles 的 JSON 测点，但没有登记源生成元数据
/// （这些宿主下 <c>System.Text.Json</c> 默认关闭反射序列化）。</item>
/// </list>
/// <br/>
/// 与 <see cref="TagsProjectConfigurationException"/> 的分工：后者是"配置语义不自洽"（换宿主也还是错的），
/// 本类型是"宿主能力不足"（同一份 XML 换到支持该能力的宿主即可运行）。
/// </summary>
public class TagsProjectNotSupportedException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">出错位置描述，可为 null</param>
    /// <param name="innerException">内部异常，可为 null</param>
    public TagsProjectNotSupportedException(string message, string? location = null, Exception? innerException = null)
        : base(message, location, innerException)
    {
    }
}
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="ITagsProjectValidator"/> 的一个实现：拒绝<b>嵌套在另一个入口内部</b>的 <c>isEntry="true"</c>。<br/>
/// <br/>
/// 为什么是错误配置：入口的识别由 <see cref="ITagGrpExtensions.ScanEntries"/> 完成，而它在遇到入口后会
/// <b>立即停止下探</b>（见其实现），所以<b>只有最外层入口才会被视为入口</b>。嵌套的 <c>isEntry="true"</c>
/// 是一个<b>完全不生效</b>的属性：它所在的子树仍然由外层入口的 runner 轮询。<br/>
/// 更隐蔽的是，入口身份决定了三件事（都以入口列表 <c>TagsProject.GetEntries()</c> 为键）：<br/>
/// 1. 独立轮询（自己的 runner、扫描周期、重试/断开策略）；<br/>
/// 2. 写入意图的路由（<see cref="ITagsProject.WriteIntent(string, TagGrpWriteIntent, out Task)"/> 按入口名找队列）；<br/>
/// 3. 逻辑组件（<see cref="ILogicet.MatchEntry"/>）的匹配。<br/>
/// 于是"嵌套入口"会静默地失去以上能力——用户以为划出了独立单元，实际是与外层合并成一个单元。<br/>
/// <br/>
/// 想要真正的独立入口，请把它放到最外层（与外层入口同级），或把它自己的通道/扫描周期改用子组表达
/// （子组可以有自己的 <c>channel</c>，见 <see cref="ITagGrpExtensions.CollectChannels"/>）。<br/>
/// <br/>
/// 本校验器随 <c>EnableCrossReferenceValidation()</c> 默认启用：它拒绝的是"属性无效果、
/// 配置不符合预期"的一类写法，且修复方式明确（去掉 isEntry 或把该组移到最外层）。
/// </summary>
public class NestedEntryValidator : ITagsProjectValidator
{
    /// <inheritdoc/>
    public void Validate(XElement root)
    {
        var errors = new List<string>();
        Walk(root, insideEntry: false, errors);

        if (errors.Count > 0)
        {
            throw new TagsProjectValidationException(errors);
        }
    }

    /// <summary>
    /// 递归查找嵌套入口。
    /// </summary>
    /// <param name="container">当前容器元素（项目根或 TagGrp）</param>
    /// <param name="insideEntry">当前是否已处于某个入口的子树内</param>
    /// <param name="errors">错误消息收集</param>
    private static void Walk(XElement container, bool insideEntry, List<string> errors)
    {
        foreach (var child in container.Elements("TagGrp"))
        {
            var isEntry = IsEntryElement(child);
            if (isEntry && insideEntry)
            {
                errors.Add(
                    $"入口 '{child.GetLocationPath()}' 嵌套在另一个入口内部，其 isEntry=\"true\" 不生效（入口识别遇到最外层入口即停止下探，该子树仍由外层入口的 runner 轮询，因此不会拥有独立的轮询周期、写入意图队列与逻辑组件匹配）：请删除该属性，或把它移到最外层（与外层入口同级）");
            }

            Walk(child, insideEntry || isEntry, errors);
        }
    }

    /// <summary>
    /// 元素是否是入口（与运行期解析一致：<c>isEntry</c> 忽略大小写地等于 "true"）。
    /// </summary>
    /// <param name="element"></param>
    private static bool IsEntryElement(XElement element)
        => string.Equals(element.Attribute("isEntry")?.Value, "true", StringComparison.OrdinalIgnoreCase);
}

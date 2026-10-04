using System.Collections.Generic;
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="ITagsProjectValidator"/> 的一个实现：校验<b>同一个通道不被多个入口共用</b>。<br/>
/// <br/>
/// 为什么需要这条约束：<b>一个通道就是一个轮询回路</b>。入口的运行器（<see cref="ITagGrpRunner"/>）
/// 既是通道的使用者，也是连接的拥有者——每轮 <see cref="ITagChannel.EnsureConnectedAsync"/>，
/// 异常/取消后的清理路径 <see cref="ITagChannel.DisconnectAsync"/> 掉该入口相关的全部通道。
/// 两个入口指向同一个 <see cref="ITagChannel"/> 实例，就破坏了"一个通道只有一个轮询回路"这个前提：<br/>
/// 1. <b>断开互相踩</b>：A 崩溃/取消时断开，B 正在用的连接随之失效，B 下一轮读写失败、进而也崩溃并再断一次，
///    两个入口交替抖动；<br/>
/// 2. <b>IO 交错</b>：两套扫描周期各自读写同一底层资源，缓存刷新与脏数据刷写在不同相位交错。
///    S7 通道内部有串行化，但 OpcUa/Modbus 等并无跨入口的并发保证（见 backlog 的"单通道多入口"条目）；<br/>
/// 3. <b>生命周期无法表达</b>：两个入口的使能/禁用、扫描周期、重试与断开策略本应各自独立，
///    共享通道后它们的实际时序被耦合在一起，配置与运行行为不再一一对应。<br/>
/// <br/>
/// <b>连接动作为空实现的通道（如 SimpleFiles）同样受此约束</b>：它虽然不会中第 1 条，
/// 但仍会中第 2、3 条；而且<b>要求分离的代价几乎为零</b>（再声明一个指向同一目录的
/// <c>&lt;Channel&gt;</c> 即可）。统一规则比"按驱动判断哪些通道可安全共享"更不容易出错——
/// 后者需要一个"本通道是否可共享"的新接口成员，还会在"看起来安全、实际不安全"的驱动上误导用户。<br/>
/// <br/>
/// 换句话说：入口多、通道多都不是问题，问题只是"<b>一个通道实例只能属于一个入口</b>"。
/// 需要多个入口访问同一台设备时，请为它们各声明一个 <c>&lt;Channel&gt;</c> 实例——
/// 连接数上限由设备承担，而不是由通道对象承担。<br/>
/// <br/>
/// 校验规则与运行期 <see cref="ITagGrpExtensions.CollectChannels"/> 保持一致（否则会误报/漏报）：<br/>
/// 1. 入口的识别与 <see cref="ITagGrpExtensions.ScanEntries"/> 完全一致——只认<b>最外层</b>入口，
///    一旦某个 <c>TagGrp</c> 是入口就不再在其子树中继续找入口；<br/>
/// 2. 以入口为单位收集其子树（含嵌套的 <c>isEntry</c> 子组）用到的通道名；<br/>
/// 3. <c>channel</c> 属性按运行期规则解析——取自身，否则向上冒泡到最近的祖先；<br/>
/// 4. 通道名相同即同一个通道实例（加载期按名称在 <c>&lt;Channel&gt;</c> 中检索，同名复用同一实例）。<br/>
/// <br/>
/// 本校验器随 <c>EnableCrossReferenceValidation()</c> <b>默认启用</b>：它拒绝的是一类
/// "在当前实现下必然出问题或语义无法表达"的配置，且修复方式明确（为每个入口各声明一个通道实例）。
/// </summary>
public class EntryChannelExclusivityValidator : ITagsProjectValidator
{
    /// <inheritdoc/>
    public void Validate(XElement root)
    {
        // 通道名 → 使用它的入口元素（按首次出现顺序）
        var usages = new Dictionary<string, List<XElement>>(StringComparer.Ordinal);

        foreach (var entry in FindEntries(root))
        {
            foreach (var channelName in CollectChannelNames(entry, root))
            {
                if (!usages.TryGetValue(channelName, out var entries))
                {
                    usages[channelName] = entries = new List<XElement>();
                }
                entries.Add(entry);
            }
        }

        var errors = new List<string>();
        foreach (var kvp in usages)
        {
            var entries = kvp.Value
                .Select(e => DescribePath(e, root))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (entries.Count <= 1)
            {
                continue;
            }

            errors.Add(
                $"通道 '{kvp.Key}' 被多个入口共用：{string.Join("、", entries)}。" +
                "入口的运行器拥有连接的生命周期（崩溃/取消时会断开该入口相关的全部通道），" +
                "跨入口共用会导致一方断开另一方正在使用的连接；请为每个入口配置独立的通道");
        }

        if (errors.Count > 0)
        {
            throw new TagsProjectSchemaException(errors);
        }
    }

    /// <summary>
    /// 找出项目中的所有入口，语义与运行期 <see cref="ITagGrpExtensions.ScanEntries"/> 一致：
    /// 只识别<b>最外层</b>的入口——一旦某个 <c>TagGrp</c> 是入口，就不再在其子树中继续找入口。
    /// 也就是说嵌套的 <c>isEntry="true"</c> 不会被当作入口（见 <see cref="NestedEntryValidator"/>），
    /// 其子树由最外层入口的 runner 轮询。
    /// </summary>
    /// <param name="root">项目根元素</param>
    private static IEnumerable<XElement> FindEntries(XElement root)
    {
        foreach (var child in root.Elements("TagGrp"))
        {
            if (IsEntryElement(child))
            {
                yield return child;
            }
            else
            {
                foreach (var nested in FindEntries(child))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>
    /// 收集一个入口子树用到的通道名（规则见类型注释），与运行期
    /// <see cref="ITagGrpExtensions.CollectChannels"/> 对齐。
    /// </summary>
    /// <param name="entry">入口元素</param>
    /// <param name="root">项目根元素（冒泡到它即截止，与运行期的 <c>__main__</c> 节点对齐）</param>
    private static ISet<string> CollectChannelNames(XElement entry, XElement root)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);

        // 入口主通道：自身声明，否则向上继承
        var entryChannel = ResolveChannel(entry, root);
        if (entryChannel is not null)
        {
            results.Add(entryChannel);
        }

        Walk(entry, entryChannel, root, results);
        return results;
    }

    /// <summary>
    /// 向下递归收集容器（入口/TagGrp/TagCbnt）子树中的通道名。
    /// </summary>
    /// <param name="container">当前容器元素</param>
    /// <param name="inherited">当前容器解析到的通道（供未声明 channel 的子孙继承）</param>
    /// <param name="root">项目根元素</param>
    /// <param name="results">结果集合</param>
    private static void Walk(XElement container, string? inherited, XElement root, ISet<string> results)
    {
        foreach (var child in container.Elements())
        {
            var name = child.Name.LocalName;
            if (name != "TagGrp" && name != "TagCbnt" && name != "Tag")
            {
                continue;
            }

            // 与运行期一致：Tag/TagCbnt 冒泡解析；TagGrp 只计自身声明的通道（祖先的已在上层计入）
            // 嵌套的 isEntry 子组不生效，其子树归本入口，同样要下探
            var own = ChannelNameOf(child);
            var effective = own ?? inherited;
            if (effective is not null)
            {
                results.Add(effective);
            }

            if (name != "Tag")
            {
                Walk(child, effective, root, results);
            }
        }
    }

    /// <summary>
    /// 冒泡解析元素的通道名：先取自身，再向上取最近的祖先（不含项目根元素本身，与运行期一致）。
    /// </summary>
    /// <param name="element">起始元素（含自身）</param>
    /// <param name="root">项目根元素</param>
    /// <returns>通道名；未配置时为 null</returns>
    private static string? ResolveChannel(XElement element, XElement root)
    {
        for (var cur = element; cur is not null && cur != root; cur = cur.Parent)
        {
            if (ChannelNameOf(cur) is string name)
            {
                return name;
            }
        }
        return null;
    }

    /// <summary>
    /// 读取元素自身的 <c>channel</c> 属性。
    /// </summary>
    /// <param name="element"></param>
    /// <returns>为空时返回 null</returns>
    private static string? ChannelNameOf(XElement element)
    {
        var name = element.Attribute("channel")?.Value;
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>
    /// 元素是否是入口（与运行期解析一致：<c>isEntry</c> 忽略大小写地等于 "true"）。
    /// </summary>
    /// <param name="element"></param>
    private static bool IsEntryElement(XElement element)
        => string.Equals(element.Attribute("isEntry")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 生成元素的定位描述（类型 + name 属性 + 父级路径），用于错误消息。
    /// </summary>
    /// <param name="element"></param>
    /// <param name="root">项目根元素</param>
    private static string DescribePath(XElement element, XElement root)
    {
        var parts = new List<string>();
        for (var cur = element; cur is not null && cur != root; cur = cur.Parent)
        {
            if (cur.Name.LocalName is "TagGrp" or "TagCbnt" or "Tag")
            {
                parts.Insert(0, $"{cur.Name.LocalName}({cur.Attribute("name")?.Value ?? "?"})");
            }
        }
        return string.Join(" → ", parts);
    }
}

using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// <see cref="ITagsProjectValidator"/> 的一个实现：校验 <c>channel</c> 属性
/// 必须指向已声明的 <c>&lt;Channel&gt;</c>。拼错的通道名在加载期报错（带完整路径上下文），
/// 而不是运行时才暴露。
/// <para>
/// 加载期校验支持注册多个实现（各自独立功能），如 XSD 校验、通道引用、driver 工厂、入口组等；
/// 第三方也可通过 DI 追加自己的校验器（见 <see cref="ITagsProjectValidator"/>）。
/// </para>
/// </summary>
public class ChannelCrossReferenceValidator : ITagsProjectValidator
{
    /// <inheritdoc/>
    public void Validate(XElement root)
    {
        // 已声明的通道名集合
        var channelNames = root.Elements("Channel")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        var errors = new List<string>();

        // 遍历所有测点（TagGrp / TagCbnt / Tag），检查 channel 引用
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name != "TagGrp" && element.Name != "TagCbnt" && element.Name != "Tag")
            {
                continue;
            }

            var channel = element.Attribute("channel")?.Value;
            if (string.IsNullOrEmpty(channel))
            {
                continue; // 省略时向上冒泡继承父级，无需检查
            }
            if (!channelNames.Contains(channel))
            {
                errors.Add($"引用了未声明的通道 '{channel}'（{DescribeLocation(element)}）");
            }
        }

        if (errors.Count > 0)
        {
            throw new TagsProjectSchemaException(errors);
        }
    }

    /// <summary>
    /// 生成测点元素的定位描述（类型 + name 属性 + 父级路径）。
    /// </summary>
    private static string DescribeLocation(XElement element)
    {
        var parts = new List<string>();
        for (var cur = element; cur is not null && cur.Name != "Project"; cur = cur.Parent)
        {
            if (cur.Name == "TagGrp" || cur.Name == "TagCbnt" || cur.Name == "Tag")
            {
                parts.Insert(0, $"{cur.Name.LocalName}({cur.Attribute("name")?.Value ?? "?"})");
            }
        }
        return string.Join(" → ", parts);
    }
}

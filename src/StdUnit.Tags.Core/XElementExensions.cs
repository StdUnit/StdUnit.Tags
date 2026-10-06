using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// extenions for XElement
/// </summary>
public static class XElementExensions
{

    #region
    /// <summary>
    /// 如果子元素存在则设置其值，否则添加新的子元素
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="childName"></param>
    /// <param name="v"></param>
    /// <returns></returns>
    public static XElement SetOrAddChild(this XElement parent, string childName, object v)
    {
        var child = parent.Element(childName);

        if (child != null)
        {
            child.SetValue(v);
        }
        else
        {
            parent.Add(new XElement(childName, v));
        }
        return parent;
    }
    #endregion

    #region helpers
    /// <summary>
    /// 生成元素的定位描述（类型 + name 属性 + 父级路径），用于加载期错误消息。<br/>
    /// 段之间用 <c>/</c> 连接（与 <see cref="ITagGrp.Descendant"/> 的路径语法一致），
    /// 例如 <c>TagGrp(产线1)/TagCbnt(输入)/Tag(bit)</c>；元素自身不属于测点元素时退化为
    /// <c>Channel(通道名)</c> 或 <c>&lt;元素名&gt;</c>。
    /// </summary>
    /// <param name="e"></param>
    internal static string GetLocationPath(this XElement e)
    {
        var parts = new List<string>();
        for (var cur = e; cur is not null; cur = cur.Parent)
        {
            if (cur.IsTagUnion())
            {
                parts.Insert(0, $"{cur.Name.LocalName}({cur.Attribute("name")?.Value ?? "?"})");
            }
        }

        if (parts.Count == 0)
        {
            var name = e.Attribute("name")?.Value;
            parts.Add(name is null ? $"<{e.Name.LocalName}>" : $"{e.Name.LocalName}({name})");
        }
        return string.Join("/", parts);
    }

    /// <summary>
    /// 获取测点元素的名称
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectXmlException">元素未配置 name 属性</exception>
    internal static string GetTagUnionName(this XElement e)
    {
        var tagName = (string?)e.Attribute("name") ?? throw new TagsProjectXmlException(
            $"测点元素 <{e.Name.LocalName}> 未配置 name 属性",
            e.GetLocationPath());
        return tagName;
    }

    /// <summary>
    /// 获取扫描间隔
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectXmlException">scanInterval 无法解析成整数</exception>
    internal static int? GetTagUnionScanInterval(this XElement e)
    {
        var interval = (string?)e.Attribute("scanInterval");
        if (string.IsNullOrEmpty(interval))
        {
            return null;
        }
        if (!int.TryParse(interval, out var parsed))
        {
            throw new TagsProjectXmlException(
                $"扫描周期 scanInterval='{interval}' 无法解析成整数，它应该是一个毫秒数量",
                e.GetLocationPath());
        }
        return parsed;
    }

    internal static string GetTagUnionAddress(this XElement e)
    {
        var address = (string?)e.Attribute("address") ?? "";// 允许为空，由具体驱动的地址解析器给出更具体的错误
        return address;
    }

    internal static string? GetTagUnionChannelName(this XElement e)
    {
        var channelName = (string?)e.Attribute("channel");
        return channelName;
    }

    internal static TagKinds GetTagUnionTagKind(this XElement e)
    {
        var type = (string?)e.Attribute("type");

        // 本来这个地方是 string.IsNullOrEmpty
        // 但是 net472 引用程序集无可空标注，编译器看不到 [NotNullWhen(false)]，无法收缩类型（会报 CS8603）
        if (type is null || type.Length == 0)
        {
            return BuiltinTagKinds.Unknown;
        }

        return type;
    }

    /// <exception cref="TagsProjectXmlException">endian 不是已知的字节序</exception>
    internal static EndianKinds GetTagUnionEndian(this XElement e)
    {
        var type = (string?)e.Attribute("endian");
        if (string.IsNullOrEmpty(type))
        {
            return EndianKinds.LittleEndian;
        }
        if (!Enum.TryParse<EndianKinds>(type, out var endian))
        {
            throw new TagsProjectXmlException(
                $"配置了未知的字节序 endian='{type}'（可选值：{string.Join(" | ", Enum.GetNames(typeof(EndianKinds)))}，大小写敏感）",
                e.GetLocationPath());
        }
        return endian;
    }


    /// <exception cref="TagsProjectXmlException">access 不是已知的访问模式</exception>
    internal static TagAccessMode? GetTagUnionAccess(this XElement e)
    {
        var modestr = (string?)e.Attribute("access");
        if (string.IsNullOrEmpty(modestr))
        {
            return null;
        }
        if (!Enum.TryParse<TagAccessMode>(modestr, out var access))
        {
            throw new TagsProjectXmlException(
                $"配置了未知的访问模式 access='{modestr}'（可选值：{string.Join(" | ", Enum.GetNames(typeof(TagAccessMode)))}，大小写敏感）",
                e.GetLocationPath());
        }
        return access;
    }

    internal static string? GetTagUnionNote(this XElement e)
    {
        var note = (string?)e.Attribute("note");
        return note;
    }

    /// <summary>
    /// 是否是测点元素(Tag, TagCbnt, TagGrp)
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static bool IsTagUnion(this XElement e)
    {
        if (e.Name == "Tag")
        {
            return true;
        }
        else if (e.Name == "TagCbnt")
        {
            return true;
        }
        else if (e.Name == "TagGrp")
        {
            return true;
        }

        return false;
    }
    #endregion

    #region
    /// <summary>
    /// 获取项目根节点下的所有测点组描述符
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static IEnumerable<TagGrpDescriptor> GetTagProjectGrpDescriptors(this XElement root)
    {
        var elements = root.Elements().Where(e => e.IsTagUnion()) ?? [];
        return elements.Select(ele => ele.ToTagGrpDescriptor());
    }

    /// <summary>
    /// 获取项目根节点下的所有通道描述符
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static IEnumerable<TagChannelDescriptor> GetTagProjectChannelDescriptors(this XElement root)
    {
        var elements = root.Elements("Channel") ?? [];
        var descriptors = elements.Select(e => e.ToTagChannelDescriptor());
        return descriptors;
    }
    #endregion
}



using System.Xml.Linq;

namespace Itminus.Tags;

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
    /// 获取测点元素的名称
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    internal static string GetTagUnionName(this XElement e)
    {
        var tagName = (string?)e.Attribute("name") ?? throw new Exception($"Tag 未配置名称");
        return tagName;
    }

    /// <summary>
    /// 获取扫描间隔
    /// </summary>
    /// <param name="e"></param>
    /// <param name="tagName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static int? GetTagUnionScanInterval(this XElement e, string tagName)
    {
        var interval = (string?)e.Attribute("scanInterval");
        if (string.IsNullOrEmpty(interval))
        {
            return null;
        }
        if (!int.TryParse(interval, out var parsed))
        {
            throw new ArgumentException($"{tagName}的扫描周期无法解析成整数，它应该是一个毫秒数量");
        }
        return parsed;
    }

    internal static string GetTagUnionAddress(this XElement e, string tagName)
    {
        var address = (string?)e.Attribute("address") ?? "";// throw new Exception($"Tag(Name={tagName})未配置地址");
        return address;
    }

    internal static string? GetTagUnionChannelName(this XElement e)
    {
        var channelName = (string?)e.Attribute("channel");
        return channelName;
    }

    internal static TagKinds GetTagUnionTagKind(this XElement e, string tagName)
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

    internal static EndianKinds GetTagUnionEndian(this XElement e, string tagName)
    {
        var type = (string?)e.Attribute("endian");
        if (string.IsNullOrEmpty(type))
        {
            return EndianKinds.LittleEndian;
        }
        if (!Enum.TryParse<EndianKinds>(type, out var endian))
        {
            throw new Exception($"Tag(Name={tagName}) 配置了未知的字节序={type}");
        }
        return endian;
    }


    internal static TagAccessMode? GetTagUnionAccess(this XElement e, string tagName)
    {
        var modestr = (string?)e.Attribute("access");
        if (string.IsNullOrEmpty(modestr))
        {
            return null;
        }
        if (!Enum.TryParse<TagAccessMode>(modestr, out var access))
        {
            throw new Exception($"Tag(Name={tagName}) 配置了未知的访问模式={modestr}");
        }
        return access;
    }

    internal static string? GetTagUnionNote(this XElement e, string tagName)
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



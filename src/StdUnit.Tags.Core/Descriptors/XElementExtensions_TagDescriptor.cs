using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// extensions for conversions between <see cref="XElement"/> and <see cref="TagDescriptor"/>
/// </summary>
public static class XElementExtensions_TagDescriptor
{
    #region
    private static readonly HashSet<string> TagDescriptorBuiltinAttrNames = new()
    {
        "name",
        "address",
        "type",
        "endian",
        "access",
        "channel",
        "note",
        "tagSize",
    };

    /// <summary>
    /// 构造 <see cref="TagDescriptor"/> 对象
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static TagDescriptor ToTagDescriptor(this XElement e)
    {
        var tagName = e.GetTagUnionName();
        var address = e.GetTagUnionAddress(tagName);
        var tagKind = e.GetTagUnionTagKind(tagName);
        var tagEndian = e.GetTagUnionEndian(tagName);
        var tagChannelName = e.GetTagUnionChannelName();
        var tagNote = e.GetTagUnionNote(tagName);

        var tagdescriptor = new TagDescriptor()
        {
            RawAddress = address,
            TagName = tagName,
            TagKind = tagKind,
            EndianKind = tagEndian,
            ChannelName = tagChannelName,
            Note = tagNote,
            Extras = e.Attributes()
                .Where(a => !TagDescriptorBuiltinAttrNames.Contains(a.Name.LocalName))
                .ToDictionary(attr => attr.Name.LocalName, attr => attr)
        };

        tagdescriptor.AccessMode = e.GetTagUnionAccess(tagName);

        var tagSize = (string?)e.Attribute("tagSize");
        if (!string.IsNullOrEmpty(tagSize))
        {
            if (!int.TryParse(tagSize, out var size))
            {
                throw new Exception($"Tag(Name={tagName}) 配置了非法大小={tagSize}");
            }
            tagdescriptor.TagSize = size;
        }
        return tagdescriptor;
    }

    /// <summary>
    /// 构造 XElement
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public static XElement ToXElement(this TagDescriptor descriptor)
    {
        var name = new XAttribute("name", descriptor.TagName);
        var address = new XAttribute("address", descriptor.RawAddress);
        var tagEndian = new XAttribute("endian", descriptor.EndianKind);
        var attrs = new List<XAttribute> {
            name,
            address,
            tagEndian,
        };
        if (descriptor.AccessMode.HasValue)
        {
            attrs.Add(new XAttribute("access", descriptor.AccessMode.Value));
        }
        if (descriptor.TagKind != BuiltinTagKinds.Unknown)
        {
            var tagKind = new XAttribute("type", descriptor.TagKind);
            attrs.Add(tagKind);
        }
        if (descriptor.TagSize != default)
        {
            var tagSize = new XAttribute("tagSize", descriptor.TagSize);
            attrs.Add(tagSize);
        }
        if (!string.IsNullOrEmpty(descriptor.ChannelName))
        {
            attrs.Add(new XAttribute("channel", descriptor.ChannelName));
        }

        if (!string.IsNullOrEmpty(descriptor.Note))
        {
            attrs.Add(new XAttribute("note", descriptor.Note));
        }

        if (descriptor.Extras != null)
        {
            foreach (var extra in descriptor.Extras)
            {
                if (TagDescriptorBuiltinAttrNames.Contains(extra.Key))
                {
                    continue;
                }
                attrs.Add(extra.Value);
            }
        }

        var ele = new XElement("Tag", attrs);
        return ele;
    }

    #endregion
}

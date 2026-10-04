using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// extensions for <see cref="XElement"/> to convert to/from <see cref="TagGrpDescriptor"/>
/// </summary>
public static class XElementExtensions_TagGrpDescriptor
{

    #region TagGrpDescriptors

    /// 转成 <see cref="XElement"/> 
    public static XElement ToXElement(this TagGrpDescriptor descriptor)
    {
        var elem = new XElement("TagGrp");
        elem.SetAttributeValue("name", descriptor.Name);
        elem.SetAttributeValue("isEntry", descriptor.IsEntry);
        if (!string.IsNullOrEmpty(descriptor.ChannelName))
        {
            elem.SetAttributeValue("channel", descriptor.ChannelName);
        }
        elem.SetAttributeValue("isEnabled", descriptor.IsEnabled);
        if (descriptor.ScanInterval.HasValue)
        {
            elem.SetAttributeValue("scanInterval", descriptor.ScanInterval);
        }
        if (descriptor.AccessMode.HasValue)
        {
            elem.SetAttributeValue("access", descriptor.AccessMode.Value.ToString());
        }
        foreach (var child in descriptor.Children)
        {
            if (child is TagDescriptor tagDesc)
            {
                elem.Add(tagDesc.ToXElement());
            }
            else if (child is TagCbntDescriptor cbntDesc)
            {
                elem.Add(cbntDesc.ToXElement());
            }
            else if (child is TagGrpDescriptor grpDesc)
            {
                elem.Add(grpDesc.ToXElement());
            }
        }
        return elem;
    }

    /// <summary>
    /// 转成 <see cref="TagGrpDescriptor"/>
    /// </summary>
    /// <param name="thisElement"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static TagGrpDescriptor ToTagGrpDescriptor(this XElement thisElement)
    {
        var thisTagName = thisElement.GetTagUnionName();
        var thisChannelName = thisElement.GetTagUnionChannelName();

        var isEnabled = !string.Equals(thisElement.Attribute("isEnabled")?.Value, "false", StringComparison.OrdinalIgnoreCase);
        int? scanInterval = thisElement.GetTagUnionScanInterval(thisTagName);
        var isEntry = string.Equals(thisElement.Attribute("isEntry")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        var grp = new TagGrpDescriptor
        {
            Name = thisTagName,
            ChannelName = thisChannelName,
            IsEnabled = isEnabled,
            ScanInterval = scanInterval,
            IsEntry = isEntry,
        };
        grp.AccessMode = thisElement.GetTagUnionAccess(thisTagName);

        // 处理额外特性
        foreach (var attr in thisElement.Attributes())
        {
            if (attr.Name == "name" ||
               attr.Name == "address" ||
               attr.Name == "channel" ||
               attr.Name == "address" ||
               attr.Name == "isEnabled" ||
               attr.Name == "scanInterval" ||
               attr.Name == "access" ||
               attr.Name == "isEntry")
            {
                continue;
            }
            grp.Extras[attr.Name.LocalName] = attr;
        }

        // 处理子节点
        foreach (var childElem in thisElement.Elements())
        {
            if (!childElem.IsTagUnion())
            {
                continue;
            }

            if (childElem.Name == "Tag")
            {
                var decriptor = childElem.ToTagDescriptor();
                grp.Children.Add(decriptor);
            }
            else if (childElem.Name == "TagCbnt")
            {
                var decriptor = childElem.ToTagCbntDescriptor();
                grp.Children.Add(decriptor);
            }
            else if (childElem.Name == "TagGrp")
            {
                var decriptor = childElem.ToTagGrpDescriptor();
                grp.Children.Add(decriptor);
            }
            else
            {
                throw new NotImplementedException($"不支持的 TagUnion 类型：{childElem.Name}");
            }
        }
        return grp;
    }
    #endregion
}

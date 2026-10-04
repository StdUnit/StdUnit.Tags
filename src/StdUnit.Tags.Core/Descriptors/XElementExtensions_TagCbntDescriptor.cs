using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// extensions for conversions between <see cref="XElement"/> and <see cref="TagCbntDescriptor"/>
/// </summary>
public static class XElementExtensions_TagCbntDescriptor
{
    #region TagCbntDescriptors

    /// <summary>
    /// XElement 转换为 <see cref="TagCbntDescriptor"/>
    /// </summary>
    /// <param name="thisElement"></param>
    /// <returns></returns>
    public static TagCbntDescriptor ToTagCbntDescriptor(this XElement thisElement)
    {
        var thisTagName = thisElement.GetTagUnionName();
        var thisChannelName = thisElement.GetTagUnionChannelName();
        var address = thisElement.GetTagUnionAddress(thisTagName);
        var isEnabled = !string.Equals(thisElement.Attribute("isEnabled")?.Value, "false", StringComparison.OrdinalIgnoreCase);

        var descriptor = new TagCbntDescriptor
        {
            Name = thisTagName,
            ChannelName = thisChannelName,
            StartAddress = address,
            IsEnabled = isEnabled,
        };

        descriptor.AccessMode = thisElement.GetTagUnionAccess(thisTagName);

        // 处理额外特性
        foreach (var attr in thisElement.Attributes())
        {
            if (
               attr.Name == "address" ||
               attr.Name == "name" ||
               attr.Name == "channel" ||
               attr.Name == "startAddress" ||
               attr.Name == "isEnabled" ||
               attr.Name == "scanInterval" ||
               attr.Name == "access")
            {
                continue;
            }
            descriptor.Extras[attr.Name.LocalName] = attr;
        }

        // 处理子节点
        foreach (var childElem in thisElement.Elements("Tag"))
        {
            var tagDescriptor = childElem.ToTagDescriptor();
            descriptor.Children.Add(tagDescriptor);
        }
        return descriptor;
    }


    /// <summary>
    /// 转成 <see cref="XElement"/> 
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public static XElement ToXElement(this TagCbntDescriptor descriptor)
    {
        var elem = new XElement("TagCbnt", descriptor.Extras);
        elem.SetAttributeValue("name", descriptor.Name);
        if (!string.IsNullOrEmpty(descriptor.ChannelName))
        {
            elem.SetAttributeValue("channel", descriptor.ChannelName);
        }
        if (!string.IsNullOrEmpty(descriptor.StartAddress))
        {
            elem.SetAttributeValue("address", descriptor.StartAddress);
        }
        elem.SetAttributeValue("isEnabled", descriptor.IsEnabled);
        if (descriptor.AccessMode.HasValue)
        {
            elem.SetAttributeValue("access", descriptor.AccessMode.Value.ToString());
        }

        foreach (var child in descriptor.Children)
        {
            elem.Add(child.ToXElement());
        }
        return elem;
    }
    #endregion


}

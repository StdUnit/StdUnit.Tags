using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// extensions for conversions between <see cref="XElement"/> and <see cref="TagChannelDescriptor"/>
/// </summary>
public static class XElementExtensions_TagChannelDescriptor
{
    /// <summary>
    /// 转换到 <see cref="TagChannelDescriptor"/>
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectXmlException">通道元素未配置 name 或 driver 属性</exception>
    public static TagChannelDescriptor ToTagChannelDescriptor(this XElement e)
    {
        var name = e.Attribute("name")?.Value ?? throw new TagsProjectXmlException(
            $"通道元素 <{e.Name.LocalName}> 未配置 name 属性",
            e.GetLocationPath());
        var driver = e.Attribute("driver")?.Value ?? throw new TagsProjectXmlException(
            $"通道元素 <{e.Name.LocalName}> 未配置 driver 属性",
            e.GetLocationPath());

        var descriptor = new TagChannelDescriptor()
        {
            Name = name,
            Driver = driver,
            Extras = e.Elements().ToDictionary(child => child.Name.LocalName, child => child)
        };
        return descriptor;
    }
}

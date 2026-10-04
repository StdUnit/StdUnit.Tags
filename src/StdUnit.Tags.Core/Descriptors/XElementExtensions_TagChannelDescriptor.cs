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
    /// <exception cref="Exception"></exception>
    public static TagChannelDescriptor ToTagChannelDescriptor(this XElement e)
    {
        var name = e.Attribute("name")?.Value ?? throw new Exception($"通道元素未配置元素名({e.Name.LocalName})");
        var driver = e.Attribute("driver")?.Value ?? throw new Exception($"通道元素未配置驱动({e.Name.LocalName})"); ;

        var descriptor = new TagChannelDescriptor()
        {
            Name = name,
            Driver = driver,
            Extras = e.Elements().ToDictionary(child => child.Name.LocalName, child => child)
        };
        return descriptor;
    }
}

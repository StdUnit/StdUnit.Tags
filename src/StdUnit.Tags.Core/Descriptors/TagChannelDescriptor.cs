using System.Xml.Linq;
using System.Xml.Serialization;

namespace StdUnit.Tags;


/// <summary>
/// <see cref="ITagChannel"/> 描述符
/// </summary>
public class TagChannelDescriptor
{

    /// <summary>
    /// Channel Name
    /// </summary>
    [XmlAttribute("name")]
    public virtual string Name { get; set; } = string.Empty;


    /// <summary>
    /// Channel Driver
    /// </summary>
    [XmlAttribute("driver")]
    public virtual string Driver { get; set; } = string.Empty;

    /// <summary>
    /// 额外参数
    /// </summary>
    public virtual IDictionary<string, XElement> Extras { get; set; } = new Dictionary<string, XElement>();

    /// <summary>
    /// 拷贝一个新的实例
    /// </summary>
    /// <returns></returns>
    public virtual TagChannelDescriptor Copy()
    {
        var xml = this.ToXElement();
        var descriptor = xml.ToTagChannelDescriptor();
        return descriptor;
    }

    /// <summary>
    /// 转成XElement
    /// </summary>
    /// <returns></returns>
    public virtual XElement ToXElement()
    {
        var element = new XElement("Channel",
            new XAttribute("name", Name),
            new XAttribute("driver", Driver)
        );

        foreach (var kvp in Extras)
        {
            element.Add(new XElement(kvp.Value));
        }

        return element;
    }


}


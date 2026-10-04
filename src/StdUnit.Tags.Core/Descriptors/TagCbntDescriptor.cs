using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点组合描述符
/// </summary>
public class TagCbntDescriptor : ITagsDescriptor
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 通道名
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// 起始地址
    /// </summary>
    public string StartAddress { get; set; } = "";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 访问模式。<br/>
    /// null 表示未指定，在构建阶段会冒泡式向上检索父级配置。<br/>
    /// 如果所有层级均为 null，则会抛出异常。
    /// </summary>
    public TagAccessMode? AccessMode { get; set; }

    /// <summary>
    /// 额外特性
    /// </summary>
    public IDictionary<string, XAttribute> Extras { get; } = new Dictionary<string, XAttribute>();

    /// <summary>
    /// 子节点描述
    /// </summary>
    public IList<TagDescriptor> Children { get; } = new List<TagDescriptor>();
}


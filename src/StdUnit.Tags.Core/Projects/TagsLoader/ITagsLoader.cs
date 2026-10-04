
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点集加载器
/// </summary>
public interface ITagsLoader
{
    /// <summary>
    /// 把一个 XElement 加载为 TagGrp | TagCbnt | Tag，并添加到 parent的子元素
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="descriptor"></param>
    /// <param name="availableChannels"></param>
    void LoadTagGroup(ITagGrp parent, ITagsDescriptor descriptor, IReadOnlyList<ITagChannel> availableChannels);
}
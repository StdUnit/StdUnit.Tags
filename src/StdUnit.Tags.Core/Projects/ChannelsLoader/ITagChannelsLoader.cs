namespace StdUnit.Tags.Core.Projects;


/// <summary>
/// 根据一系列通道描述符，加载 <see cref="ITagChannel"/>
/// </summary>
public interface ITagChannelsLoader
{
    /// <summary>
    /// 加载通道
    /// </summary>
    /// <param name="descriptors"></param>
    /// <returns></returns>
    IList<ITagChannel> LoadChannels(IEnumerable<TagChannelDescriptor> descriptors);
}
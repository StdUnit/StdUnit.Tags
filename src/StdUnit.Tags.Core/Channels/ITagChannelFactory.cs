namespace StdUnit.Tags;

/// <summary>
/// 构建 <see cref="ITagChannel"/>
/// </summary>
public interface ITagChannelFactory
{
    /// <summary>
    /// 支持的驱动名称列表
    /// </summary>
    IReadOnlyList<string> GetAvailableDrivers();
    /// <summary>
    /// 创建实例
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    ITagChannel Create(TagChannelDescriptor descriptor);
}

namespace StdUnit.Tags;

/// <summary>
/// 组合通道工厂。<br/>
/// 使用组合模式，把支持的多个通道工厂合成一个通道工厂：在使用时，按顺序检索找到能支持支持当前驱动的第一个通道工厂，然后用之创建通道。
/// </summary>
public class CompositeTagChannelFactory : ITagChannelFactory
{
    #region
    /// <summary>
    /// 工厂列表 backing field
    /// </summary>
    protected IList<ITagChannelFactory> _factoryList = new List<ITagChannelFactory>();

    /// <summary>
    /// 工厂列表
    /// </summary>
    public virtual IList<ITagChannelFactory> FactoryList => _factoryList;
    #endregion

    /// <summary>
    /// 添加通道工厂
    /// </summary>
    /// <param name="factory"></param>
    /// <returns></returns>
    public virtual CompositeTagChannelFactory AddFactory(ITagChannelFactory factory)
    {
        this._factoryList.Add(factory);
        return this;
    }


    /// <inheritdoc/>
    public virtual IReadOnlyList<string> GetAvailableDrivers() =>
        _factoryList
            .SelectMany(f => f.GetAvailableDrivers())
            .ToList();


    /// <inheritdoc/>
    /// <exception cref="TagsProjectConfigurationException">descriptor 的 driver 未注册任何 <see cref="ITagChannelFactory"/> 实现</exception>
    public virtual ITagChannel Create(TagChannelDescriptor descriptor)
    {
        var factory = this._factoryList.FirstOrDefault(f => f.GetAvailableDrivers().Contains(descriptor.Driver))
            ?? throw new TagsProjectConfigurationException(
                $"通道 '{descriptor.Name}' 的 driver='{descriptor.Driver}' 未注册对应的 {nameof(ITagChannelFactory)} 实现（已注册: {this.DescribeAvailableDrivers()}）",
                location: $"Channel({descriptor.Name})");
        var channel = factory.Create(descriptor);
        return channel;
    }

    /// <summary>
    /// 把已注册的驱动名拼成一行，仅用于错误消息。
    /// </summary>
    private string DescribeAvailableDrivers()
        => string.Join(", ", this.GetAvailableDrivers().OrderBy(d => d, StringComparer.Ordinal));
}
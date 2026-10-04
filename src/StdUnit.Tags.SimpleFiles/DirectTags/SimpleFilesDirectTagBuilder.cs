
namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// 构建 SimpleFiles 直接测点的构建器。<br/>
/// 当通道的驱动为 <see cref="SimpleFilesNames.DriverName"/> 时，用于构建测点。
/// </summary>
public partial class SimpleFilesDirectTagBuilder : TagBuilderBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    public SimpleFilesDirectTagBuilder()
    {
    }


    #region WithFactory
    /// <summary>
    /// 使用自定义工厂创建测点。<br/>
    /// 与基类 <see cref="TagBuilderBase.WithFactory(StdUnit.Tags.TagBuilderBase.CreateTag)"/> 的区别：<br/>
    /// 1. 调用工厂前会先把通道解析为 <see cref="SimpleFilesTagChannel"/>（自身通道为空时冒泡获取），类型不匹配抛出 <see cref="InvalidOperationException"/>；<br/>
    /// 2. <see cref="TagDescriptor.NormalizedAddress"/> 已按通道的 BaseDir 完成归一化，工厂内无需重复处理；<br/>
    /// </summary>
    /// <typeparam name="TVal"></typeparam>
    /// <param name="factory"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">通道类型不是 <see cref="SimpleFilesTagChannel"/> 时抛出。</exception>
    public SimpleFilesDirectTagBuilder WithFactory<TVal>(CreateSimpleFilesDirectTag<TVal> factory)
    {
        base.WithFactory(
            (descriptor, thisChannel, container) =>
            {
                var channel = thisChannel ?? container.SearchRequiredChannel();
                var sfsChannel = channel as SimpleFilesTagChannel ?? throw new InvalidOperationException($"通道类型不匹配：{channel?.GetType().FullName}");
                descriptor.NormalizedAddress = sfsChannel.MakePath(descriptor.RawAddress);
                return factory(descriptor, thisChannel as SimpleFilesTagChannel, container);
            }
        );
        return this;
    }

    /// <summary>
    /// 创建 SimpleFile 测点的委托。<br/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="thisChannel"></param>
    /// <param name="container"></param>
    /// <returns></returns>
    public delegate SimpleFilesDirectTagBase<TVal> CreateSimpleFilesDirectTag<TVal>(
        TagDescriptor descriptor,
        SimpleFilesTagChannel? thisChannel,
        TagContainer container
        );


    /// <summary>
    /// 使用 JSON 测点工厂创建<see cref="JsonDirectTag{TVal}"/>型测点。<br/>
    /// </summary>
    /// <typeparam name="TVal"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public SimpleFilesDirectTagBuilder WithJsonTagFactory<TVal>()
    {
        this.WithFactory(
            (descriptor, thisChannel, container) =>
            {
                return new JsonDirectTag<TVal>(descriptor, thisChannel, container);
            }
        );
        return this;
    }
    #endregion

    /// <inheritdoc/>
    protected override ITag Fallback(ITagChannel ch)
    {
        var factory = new SimpleFilesDirectTagFactory(this.Parent.IntoTagContainer());
        var tag = factory.Create(this.TagDescriptor, this.Channel as SimpleFilesTagChannel);

        return tag;
    }
}
